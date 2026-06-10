using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.Registry;

/// <summary>
/// A read-only module that enumerates values within a single Windows Registry key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryScanModule : IScanModule
{
    /// <inheritdoc />
    public string Name => "RegistryScanModule";

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);

        var hiveName = context.GetOption("registryHive");
        var subKeyPath = context.GetOption("subKeyPath");

        if (string.IsNullOrWhiteSpace(hiveName) || string.IsNullOrWhiteSpace(subKeyPath))
        {
            return Task.FromResult(builder
                .AddError("Required options 'registryHive' and/or 'subKeyPath' are missing or empty in context.")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        builder.SetMetadata("RegistryHive", hiveName)
               .SetMetadata("SubKeyPath", subKeyPath);

        if (!Enum.TryParse<RegistryHive>(hiveName, true, out var hive))
        {
            return Task.FromResult(builder
                .AddError($"Invalid registry hive: '{hiveName}'")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        using var subKey = RegistryReader.OpenReadOnly(hive, subKeyPath, RegistryView.Default);
        if (subKey == null)
        {
            return Task.FromResult(builder
                .AddError($"Subkey does not exist or access denied: {hiveName}\\{subKeyPath}")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        string sourcePrefix = $"{hiveName}\\{subKeyPath}";

        foreach (var valueName in subKey.GetValueNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var kind = subKey.GetValueKind(valueName);
                var rawData = subKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var formattedData = RegistryValueFormatter.Format(kind, rawData);

                builder.AddFinding(
                    category: "RegistryValue",
                    description: RegistryValueFormatter.DisplayName(valueName),
                    source: sourcePrefix,
                    timestampUtc: null, // Registry values carry no per-value timestamp in the .NET API
                    rawValue: $"Kind: {kind} | Data: {formattedData}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                builder.AddError($"Error reading value '{valueName}': {ex.Message}");
            }
            catch (Exception ex)
            {
                builder.AddError($"Unexpected error reading value '{valueName}': {ex.Message}");
            }
        }

        builder.SetMetadata("ValuesFound", builder.FindingCount);
        return Task.FromResult(builder.Build());
    }
}
