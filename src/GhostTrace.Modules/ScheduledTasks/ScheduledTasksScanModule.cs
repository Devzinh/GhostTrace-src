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

            dynamic? rootFolder = null;
            try
            {
                rootFolder = taskService.GetFolder("\\");
                EnumerateFolder(rootFolder, builder, c, cancellationToken);
            }
            finally
            {
                ReleaseComObject(rootFolder);
            }
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
        try { currentFolderPath = folder.Path; }
        catch (Exception ex)
        {
            builder.AddError($"Failed to read a Task Scheduler folder path: {ex.Message}");
            c.WithError++;
        }

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

            try { stateStr = FormatTaskState((int)task.State); }
            catch (Exception ex) { builder.AddError($"Failed to read state for task '{path}': {ex.Message}"); }
            try { lastRun = task.LastRunTime; }
            catch (Exception ex) { builder.AddError($"Failed to read last run time for task '{path}': {ex.Message}"); }
            try { nextRun = task.NextRunTime; }
            catch (Exception ex) { builder.AddError($"Failed to read next run time for task '{path}': {ex.Message}"); }

            string userStr = "<unknown>";
            string actionStr = "<none>";
            dynamic? definition = null;
            try
            {
                definition = task.Definition;
                if (definition != null)
                {
                    userStr = ExtractUserStr(definition, builder, path);
                    actionStr = ExtractActionStr(definition, builder, path);
                }
            }
            catch (Exception ex)
            {
                builder.AddError($"Failed to read definition for task '{path}': {ex.Message}");
            }
            finally
            {
                ReleaseComObject(definition);
            }

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

    private string ExtractUserStr(dynamic definition, ScanResultBuilder builder, string taskPath)
    {
        dynamic? principal = null;
        try
        {
            principal = definition.Principal;
            string user = principal.UserId ?? principal.GroupId ?? "<unknown>";
            return user;
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to read principal for task '{taskPath}': {ex.Message}");
            return "<unknown>";
        }
        finally
        {
            ReleaseComObject(principal);
        }
    }

    private string ExtractActionStr(dynamic definition, ScanResultBuilder builder, string taskPath)
    {
        dynamic? actions = null;
        dynamic? firstAction = null;
        try
        {
            actions = definition.Actions;
            if (actions.Count == 0)
            {
                return "<none>";
            }

            firstAction = actions.Item(1);
            int actionType = (int)firstAction.Type;
            string actionStr = actionType switch
            {
                0 => $"{firstAction.Path ?? string.Empty} {firstAction.Arguments ?? string.Empty}".Trim(),
                5 => $"COM Handler: {firstAction.ClassId}",
                _ => $"ActionType: {actionType}"
            };

            return actionStr;
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to read action for task '{taskPath}': {ex.Message}");
            return "<unknown>";
        }
        finally
        {
            ReleaseComObject(firstAction);
            ReleaseComObject(actions);
        }
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
