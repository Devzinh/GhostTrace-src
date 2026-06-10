using System;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.ScheduledTasks;

/// <summary>
/// A read-only module that walks the HKLM Scheduled Tasks Cache tree to surface evasion
/// anomalies such as Ghost Tasks (registry entries with a missing Security Descriptor).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TaskCacheScanModule : IScanModule
{
    private const string TaskCacheTreePath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree";

    /// <inheritdoc />
    public string Name => "TaskCacheScanModule";

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        var counters = new Counters();

        using var treeKey = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, TaskCacheTreePath);
        if (treeKey == null)
        {
            builder.AddError($"Registry key not found: HKLM\\{TaskCacheTreePath}").ForceStatus(ScanStatus.Failure);
        }
        else
        {
            EnumerateTree(treeKey, TaskCacheTreePath, builder, counters, cancellationToken);
        }

        builder.SetMetadata("TotalEnumeratedKeys", counters.EnumeratedKeys)
               .SetMetadata("TotalTaskLikeEntries", counters.TaskLikeEntries)
               .SetMetadata("TotalMissingSd", counters.MissingSd)
               .SetMetadata("TotalAnomalies", counters.Anomalies);

        return Task.FromResult(builder.Build());
    }

    private sealed class Counters
    {
        public int EnumeratedKeys;
        public int TaskLikeEntries;
        public int MissingSd;
        public int Anomalies;
    }

    private void EnumerateTree(RegistryKey parentKey, string currentLogicalPath, ScanResultBuilder builder, Counters c, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string[] subKeyNames;
        try
        {
            subKeyNames = parentKey.GetSubKeyNames();
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to enumerate subkeys under '{currentLogicalPath}': {ex.Message}");
            return;
        }

        foreach (var subKeyName in subKeyNames)
        {
            ct.ThrowIfCancellationRequested();
            c.EnumeratedKeys++;
            string subLogicalPath = $"{currentLogicalPath}\\{subKeyName}";

            RegistryKey? subKey = null;
            try
            {
                subKey = parentKey.OpenSubKey(subKeyName, writable: false);
                if (subKey != null)
                {
                    EvaluateTaskNode(subKey, subKeyName, subLogicalPath, builder, c);
                    EnumerateTree(subKey, subLogicalPath, builder, c, ct);
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
            {
                builder.AddError($"Access denied opening subkey '{subLogicalPath}': {ex.Message}");
            }
            catch (Exception ex)
            {
                builder.AddError($"Unexpected error opening subkey '{subLogicalPath}': {ex.Message}");
            }
            finally
            {
                subKey?.Dispose();
            }
        }
    }

    private void EvaluateTaskNode(RegistryKey node, string nodeName, string fullPath, ScanResultBuilder builder, Counters c)
    {
        try
        {
            string? idStr = node.GetValue("Id")?.ToString();
            int? indexVal = node.GetValue("Index") is int idx ? idx : null;
            bool hasSd = node.GetValue("SD") != null;

            // A logical task cache entry has at least one of these markers; pure
            // organisational folders have none.
            if (idStr == null && indexVal == null && !hasSd) return;

            c.TaskLikeEntries++;
            var anomalies = new System.Collections.Generic.List<string>();

            if (!hasSd)
            {
                anomalies.Add("MISSING_SD (Ghost Task Indicator)");
                c.MissingSd++;
            }
            if (indexVal is null or 0)
            {
                anomalies.Add("MISSING_OR_ZERO_INDEX");
            }
            if (anomalies.Count > 0) c.Anomalies++;

            string anomaliesStr = anomalies.Count > 0 ? $" | ANOMALIES: {string.Join(", ", anomalies)}" : "";

            builder.AddFinding(
                category: "ScheduledTaskCacheEntry",
                description: nodeName,
                source: $"HKLM\\{fullPath}",
                timestampUtc: null,
                rawValue: $"Id: {idStr ?? "<missing>"} | Index: {indexVal?.ToString() ?? "<missing>"} | HasSD: {hasSd}{anomaliesStr}");
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to evaluate values for node '{fullPath}': {ex.Message}");
        }
    }
}
