using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.SystemArtifacts;

/// <summary>
/// Enumerates installed-program registry entries (the "Programs and Features" source).
/// This is the heart of a trace hunt: it ties a software name to its display version,
/// publisher, install location and uninstall command — everything needed to confirm
/// presence and plan removal.
///
///   HKLM\...\Uninstall, HKLM\WOW6432Node\...\Uninstall, HKCU\...\Uninstall
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UninstallEntriesScanModule : IScanModule
{
    public string Name => "UninstallEntriesScanModule";

    private static readonly (RegistryHive Hive, string Path, string Label)[] Roots =
    {
        (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM-WOW64"),
        (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU"),
    };

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int programs = 0;

        foreach (var (hive, path, label) in Roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var root = RegistryReader.OpenReadOnly(hive, path);
            if (root == null) continue;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var sub = SafeOpen(root, subKeyName);
                if (sub == null) continue;

                string? displayName = RegistryReader.TryGetString(sub, "DisplayName");
                if (string.IsNullOrWhiteSpace(displayName)) continue; // updates/components without a name

                // Skip the entries the OS marks as system components / updates.
                if (RegistryReader.TryGetInt(sub, "SystemComponent") == 1) continue;

                string? version = RegistryReader.TryGetString(sub, "DisplayVersion");
                string? publisher = RegistryReader.TryGetString(sub, "Publisher");
                string? location = RegistryReader.TryGetString(sub, "InstallLocation");
                string? uninstall = RegistryReader.TryGetString(sub, "UninstallString");
                DateTimeOffset? installDate = ForensicTime.FromCompactDate(RegistryReader.TryGetString(sub, "InstallDate"));

                builder.AddFinding(
                    category: "InstalledProgram",
                    description: displayName!,
                    source: $"{label}\\...\\Uninstall\\{subKeyName}",
                    timestampUtc: installDate,
                    rawValue: $"Version: {version ?? "-"} | Publisher: {publisher ?? "-"} | " +
                              $"InstallLocation: {location ?? "-"} | Uninstall: {uninstall ?? "-"}");
                programs++;
            }
        }

        builder.SetMetadata("ProgramsCollected", programs);
        return Task.FromResult(builder.Build());
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, writable: false); }
        catch { return null; }
    }
}
