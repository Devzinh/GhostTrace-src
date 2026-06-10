using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.Shimcache;

/// <summary>
/// A read-only module that parses AppCompatCache (Shimcache) to recover execution evidence.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ShimcacheScanModule : IScanModule
{
    private const string ShimcacheKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache";
    private const string ShimcacheValueName = "AppCompatCache";

    /// <inheritdoc />
    public string Name => "ShimcacheScanModule";

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int collected = 0;
        int enumerated = 0;

        using var key = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, ShimcacheKeyPath);
        if (key == null)
        {
            return Task.FromResult(builder
                .AddError($"Registry key not found: HKLM\\{ShimcacheKeyPath}")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        object? value = key.GetValue(ShimcacheValueName);
        if (value is not byte[] data)
        {
            return Task.FromResult(builder
                .AddError(value == null
                    ? $"Registry value '{ShimcacheValueName}' not found."
                    : $"Registry value '{ShimcacheValueName}' is not REG_BINARY.")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        try
        {
            var entries = ShimcacheParser.Parse(data);
            enumerated = entries.Count;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var suspicion = SuspicionHeuristics.InspectPath(entry.ExecutablePath);
                string suffix = suspicion.Count > 0 ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}" : "";

                builder.AddFinding(
                    category: "ExecutionEvidence",
                    description: entry.ExecutablePath,
                    source: $"HKLM\\{ShimcacheKeyPath}\\{ShimcacheValueName}",
                    timestampUtc: entry.LastModifiedUtc,
                    rawValue: $"Index: {entry.Index:D3} | LastModified: {entry.LastModifiedUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "N/A"} | FileTime: {entry.FileTimeRaw}{suffix}");
                collected++;
            }
        }
        catch (ShimcacheFormatException ex)
        {
            builder.AddError($"Failed to parse Shimcache format: {ex.Message}").ForceStatus(ScanStatus.Failure);
        }
        catch (Exception ex)
        {
            builder.AddError($"Unexpected error parsing Shimcache binary block: {ex.Message}").ForceStatus(ScanStatus.Failure);
        }

        builder.SetMetadata("TotalEnumerated", enumerated)
               .SetMetadata("TotalCollected", collected);

        return Task.FromResult(builder.Build());
    }
}
