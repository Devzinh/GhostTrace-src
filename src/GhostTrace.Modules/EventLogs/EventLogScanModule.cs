using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.EventLogs;

/// <summary>
/// A read-only module that enumerates recent entries from a local Windows Event Log.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EventLogScanModule : IScanModule
{
    /// <inheritdoc />
    public string Name => "EventLogScanModule";

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        var logName = context.GetOption("logName");
        var maxEntriesStr = context.GetOption("maxEntries");

        if (string.IsNullOrWhiteSpace(logName))
        {
            return Task.FromResult(builder
                .AddError("Required option 'logName' is missing or empty.")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        int maxEntries = 50;
        if (!string.IsNullOrWhiteSpace(maxEntriesStr) && (!int.TryParse(maxEntriesStr, out maxEntries) || maxEntries <= 0))
        {
            return Task.FromResult(builder
                .AddError($"Option 'maxEntries' must be a positive integer. Invalid value: '{maxEntriesStr}'")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        builder.SetMetadata("LogName", logName)
               .SetMetadata("MaxEntriesRequested", maxEntries);

        try
        {
            if (!EventLog.Exists(logName))
            {
                return Task.FromResult(builder
                    .AddError($"Event log '{logName}' does not exist on the local machine.")
                    .ForceStatus(ScanStatus.Failure)
                    .Build());
            }

            using var eventLog = new EventLog(logName);
            var entries = eventLog.Entries;
            int total = entries.Count;
            int startIndex = total - 1;
            int stopIndex = Math.Max(0, total - maxEntries);

            for (int i = startIndex; i >= stopIndex; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    EventLogEntry entry = entries[i];

                    string message = "(Message unavailable)";
                    try { message = entry.Message ?? string.Empty; } catch { /* provider DLL missing */ }

                    if (message.Length > 200) message = message.Substring(0, 197) + "...";
                    message = message.Replace("\r", "").Replace("\n", " ").Trim();

                    long eventId = entry.InstanceId & 0x3FFFFFFF;

                    builder.AddFinding(
                        category: "EventLogEntry",
                        description: $"[{entry.EntryType}] {entry.Source}",
                        source: $"EventLog:{logName}",
                        timestampUtc: entry.TimeGenerated.ToUniversalTime(),
                        rawValue: $"EventID: {eventId} | Type: {entry.EntryType} | Source: {entry.Source} | Machine: {entry.MachineName} | Message: {message}");
                }
                catch (Exception ex)
                {
                    builder.AddError($"Error reading entry at index {i}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            builder.AddError($"Access denied to event log '{logName}': {ex.Message}").ForceStatus(ScanStatus.Failure);
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed to open or enumerate event log '{logName}': {ex.Message}");
            if (builder.FindingCount == 0) builder.ForceStatus(ScanStatus.Failure);
        }

        builder.SetMetadata("EntriesCollected", builder.FindingCount);
        return Task.FromResult(builder.Build());
    }
}
