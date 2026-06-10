using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.Persistence;

/// <summary>
/// A read-only module that aggregates basic persistence mechanisms —
/// Run / RunOnce registry keys and the per-user / common Startup folders.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PersistenceScanModule : IScanModule
{
    /// <inheritdoc />
    public string Name => "PersistenceScanModule";

    private static readonly (RegistryHive Hive, string Path)[] RegTargets =
    {
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        // 32-bit (WOW64) Run keys — a separate physical location on x64 systems that
        // 32-bit malware uses for persistence; invisible through the 64-bit view above.
        (RegistryHive.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce"),
    };

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);

        int regFindings = 0;
        int startupFindings = 0;

        foreach (var (hive, path) in RegTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            regFindings += ScanRegistryKey(builder, hive, path, cancellationToken);
        }

        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
                 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            startupFindings += ScanStartupFolder(builder, folder, cancellationToken);
        }

        builder.SetMetadata("RegistryFindings", regFindings)
               .SetMetadata("StartupFindings", startupFindings)
               .SetMetadata("TotalFindings", builder.FindingCount);

        return Task.FromResult(builder.Build());
    }

    private int ScanRegistryKey(ScanResultBuilder builder, RegistryHive hive, string subKeyPath, CancellationToken ct)
    {
        // A missing Run/RunOnce key is normal (RunOnce is deleted when emptied) — not an error.
        using var subKey = RegistryReader.OpenReadOnly(hive, subKeyPath, RegistryView.Default);
        if (subKey == null) return 0;

        string sourcePrefix = $"{hive}\\{subKeyPath}";
        int count = 0;

        foreach (var valueName in subKey.GetValueNames())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var kind = subKey.GetValueKind(valueName);
                var rawData = subKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var formatted = RegistryValueFormatter.Format(kind, rawData);

                var suspicion = SuspicionHeuristics.InspectPath(formatted);
                string suffix = suspicion.Count > 0 ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}" : "";

                builder.AddFinding(
                    category: "PersistenceRegistryRun",
                    description: RegistryValueFormatter.DisplayName(valueName),
                    source: sourcePrefix,
                    timestampUtc: null,
                    rawValue: $"Kind: {kind} | Data: {formatted}{suffix}");
                count++;
            }
            catch (Exception ex)
            {
                builder.AddError($"Error reading registry value '{valueName}' in {sourcePrefix}: {ex.Message}");
            }
        }

        return count;
    }

    private int ScanStartupFolder(ScanResultBuilder builder, string folderPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return 0;

        var dirInfo = new DirectoryInfo(folderPath);
        if (!dirInfo.Exists) return 0; // a missing Startup folder is normal, not an error

        int count = 0;
        try
        {
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    builder.AddFinding(
                        category: "PersistenceStartupFile",
                        description: file.Name,
                        source: file.FullName,
                        timestampUtc: file.LastWriteTimeUtc,
                        rawValue: $"Size: {file.Length} bytes | Created: {file.CreationTimeUtc:o} | Modified: {file.LastWriteTimeUtc:o}");
                    count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    builder.AddError($"Error reading startup file '{file.FullName}': {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            builder.AddError($"Error enumerating startup folder '{folderPath}': {ex.Message}");
        }

        return count;
    }
}
