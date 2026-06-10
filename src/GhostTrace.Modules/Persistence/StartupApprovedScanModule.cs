using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Persistence;

/// <summary>
/// Reads the StartupApproved state — the enabled/disabled flag that Task Manager and
/// Settings write for each autostart entry. Correlating this with the Run keys reveals
/// items a user (or attacker) explicitly enabled, and disabled-but-still-present entries.
///
///   HKCU\...\Explorer\StartupApproved\{Run,Run32,StartupFolder}
///   HKLM\...\Explorer\StartupApproved\{Run,Run32,StartupFolder}
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class StartupApprovedScanModule : IScanModule
{
    public string Name => "StartupApprovedScanModule";

    private const string Base = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
    private static readonly string[] SubKeys = { "Run", "Run32", "StartupFolder" };

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int enabled = 0, disabled = 0;

        foreach (var (hive, label) in new[] { (RegistryHive.CurrentUser, "HKCU"), (RegistryHive.LocalMachine, "HKLM") })
        {
            foreach (var sub in SubKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var key = RegistryReader.OpenReadOnly(hive, $@"{Base}\{sub}");
                if (key == null) continue;

                foreach (var valueName in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(valueName)) continue;

                    var data = RegistryReader.TryGetBytes(key, valueName);
                    bool isEnabled = IsEnabled(data);
                    if (isEnabled) enabled++; else disabled++;

                    builder.AddFinding(
                        category: "StartupApproved",
                        description: valueName,
                        source: $"{label}\\...\\StartupApproved\\{sub}",
                        timestampUtc: null,
                        rawValue: $"State: {(isEnabled ? "ENABLED" : "DISABLED")} | Location: {sub}");
                }
            }
        }

        builder.SetMetadata("Enabled", enabled).SetMetadata("Disabled", disabled);
        return Task.FromResult(builder.Build());
    }

    // Byte 0: 0x02 / 0x06 = enabled, 0x03 = disabled (rest is the disable timestamp).
    private static bool IsEnabled(byte[]? data)
    {
        if (data == null || data.Length == 0) return true;
        return data[0] != 0x03;
    }
}
