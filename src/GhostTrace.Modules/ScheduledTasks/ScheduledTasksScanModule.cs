using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.ScheduledTasks;

/// <summary>
/// A read-only module that enumerates local Windows Scheduled Tasks via Task Scheduler
/// COM late-binding.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScheduledTasksScanModule : IScanModule
{
    private const int TASK_ENUM_HIDDEN = 1;

    /// <inheritdoc />
    public string Name => "ScheduledTasksScanModule";

    private sealed class Counters
    {
        public int Enumerated;
        public int Collected;
        public int WithError;
    }

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        var c = new Counters();

        Type? type = Type.GetTypeFromProgID("Schedule.Service");
        if (type == null)
        {
            return Task.FromResult(builder
                .AddError("COM ProgID 'Schedule.Service' is not registered on this system.")
                .ForceStatus(ScanStatus.Failure)
                .SetMetadata("TotalEnumerated", 0)
                .SetMetadata("TotalCollected", 0)
                .SetMetadata("TotalWithError", 0)
                .Build());
        }

        dynamic? taskService = null;
        try
        {
            taskService = Activator.CreateInstance(type)
                          ?? throw new InvalidOperationException("Activator returned null for Schedule.Service.");
            taskService.Connect();

            dynamic rootFolder = taskService.GetFolder("\\");
            EnumerateFolder(rootFolder, builder, c, cancellationToken);
            ReleaseComObject(rootFolder);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to initialize Task Scheduler COM service: {ex.Message}")
                   .ForceStatus(ScanStatus.Failure);
        }
        finally
        {
            ReleaseComObject(taskService);
        }

        // Derive a precise status from the collection outcome (the builder otherwise
        // only sees findings/errors; we want collected==0 && errors>0 → Failure).
        if (c.Collected == 0 && c.WithError > 0)
        {
            builder.ForceStatus(ScanStatus.Failure);
        }

        builder.SetMetadata("TotalEnumerated", c.Enumerated)
               .SetMetadata("TotalCollected", c.Collected)
               .SetMetadata("TotalWithError", c.WithError);

        return Task.FromResult(builder.Build());
    }

    private void EnumerateFolder(dynamic folder, ScanResultBuilder builder, Counters c, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string currentFolderPath = "<unknown>";
        try { currentFolderPath = folder.Path; } catch { }

        try
        {
            dynamic tasks = folder.GetTasks(TASK_ENUM_HIDDEN);
            int taskCount = tasks.Count;
            c.Enumerated += taskCount;

            for (int i = 1; i <= taskCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                dynamic? task = null;
                try
                {
                    task = tasks.Item(i);
                    ExtractTaskDetails(task, builder);
                    c.Collected++;
                }
                catch (Exception ex)
                {
                    builder.AddError($"Failed to read task at index {i} in folder '{currentFolderPath}': {ex.Message}");
                    c.WithError++;
                }
                finally
                {
                    ReleaseComObject(task);
                }
            }
            ReleaseComObject(tasks);
        }
        catch (Exception ex)
        {
            builder.AddError($"Error retrieving tasks list for folder '{currentFolderPath}': {ex.Message}");
            c.WithError++;
        }

        try
        {
            dynamic subFolders = folder.GetFolders(0);
            int subCount = subFolders.Count;

            for (int i = 1; i <= subCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                dynamic? subFolder = null;
                try
                {
                    subFolder = subFolders.Item(i);
                    EnumerateFolder(subFolder, builder, c, ct);
                }
                catch (Exception ex)
                {
                    builder.AddError($"Error opening a subfolder inside '{currentFolderPath}': {ex.Message}");
                    c.WithError++;
                }
                finally
                {
                    ReleaseComObject(subFolder);
                }
            }
            ReleaseComObject(subFolders);
        }
        catch (Exception ex)
        {
            builder.AddError($"Error retrieving subfolders for '{currentFolderPath}': {ex.Message}");
            c.WithError++;
        }
    }

    private void ExtractTaskDetails(dynamic task, ScanResultBuilder builder)
    {
        string name = "<unknown>";
        string path = "<unknown>";
        string stateStr = "<unknown>";
        DateTime? lastRun = null;
        DateTime? nextRun = null;

        try
        {
            name = task.Name ?? name;
            path = task.Path ?? path;

            try { stateStr = FormatTaskState((int)task.State); } catch { }
            try { lastRun = task.LastRunTime; } catch { }
            try { nextRun = task.NextRunTime; } catch { }

            string userStr = "<unknown>";
            string actionStr = "<none>";
            try
            {
                dynamic? definition = task.Definition;
                if (definition != null)
                {
                    userStr = ExtractUserStr(definition);
                    actionStr = ExtractActionStr(definition);
                    ReleaseComObject(definition);
                }
            }
            catch { }

            if (lastRun?.Year <= 1899) lastRun = null;
            if (nextRun?.Year <= 1899) nextRun = null;

            string actionLower = actionStr.ToLowerInvariant();
            var suspicion = SuspicionHeuristics.InspectPath(actionLower);
            if (suspicion.Count == 0) suspicion = SuspicionHeuristics.InspectCommand(actionLower);
            string suffix = suspicion.Count > 0 ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}" : "";

            builder.AddFinding(
                category: "ScheduledTask",
                description: name,
                source: path,
                timestampUtc: lastRun?.ToUniversalTime(),
                rawValue: $"State: {stateStr} | User: {userStr} | Action: {actionStr} | NextRun: {(nextRun?.ToString("o") ?? "N/A")}{suffix}");
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to extract all metadata for task '{path}': {ex.Message}");
        }
    }

    private string ExtractUserStr(dynamic definition)
    {
        try
        {
            dynamic principal = definition.Principal;
            string user = principal.UserId ?? principal.GroupId ?? "<unknown>";
            ReleaseComObject(principal);
            return user;
        }
        catch { return "<unknown>"; }
    }

    private string ExtractActionStr(dynamic definition)
    {
        try
        {
            dynamic actions = definition.Actions;
            if (actions.Count == 0)
            {
                ReleaseComObject(actions);
                return "<none>";
            }

            dynamic firstAction = actions.Item(1);
            int actionType = (int)firstAction.Type;
            string actionStr = actionType switch
            {
                0 => $"{firstAction.Path ?? string.Empty} {firstAction.Arguments ?? string.Empty}".Trim(),
                5 => $"COM Handler: {firstAction.ClassId}",
                _ => $"ActionType: {actionType}"
            };

            ReleaseComObject(firstAction);
            ReleaseComObject(actions);
            return actionStr;
        }
        catch { return "<unknown>"; }
    }

    private static string FormatTaskState(int state) => state switch
    {
        0 => "Unknown",
        1 => "Disabled",
        2 => "Queued",
        3 => "Ready",
        4 => "Running",
        _ => state.ToString()
    };

    private static void ReleaseComObject(object? obj)
    {
        if (obj != null && Marshal.IsComObject(obj))
        {
            Marshal.ReleaseComObject(obj);
        }
    }
}
