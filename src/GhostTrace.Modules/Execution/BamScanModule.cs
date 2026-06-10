using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Execution;

/// <summary>
/// Reads the Background Activity Moderator (BAM) and Desktop Activity Moderator (DAM)
/// state from the registry. Each value under a user SID is a full executable path whose
/// data ends with an 8-byte FILETIME of the program's last execution — high-fidelity,
/// per-user execution evidence (MITRE ATT&amp;CK execution timeline).
///
///   HKLM\SYSTEM\CurrentControlSet\Services\bam\State\UserSettings\{SID}
///   HKLM\SYSTEM\CurrentControlSet\Services\dam\State\UserSettings\{SID}
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BamScanModule : IScanModule
{
    public string Name => "BamScanModule";

    private static readonly (string Provider, string Path)[] Roots =
    {
        ("BAM", @"SYSTEM\CurrentControlSet\Services\bam\State\UserSettings"),
        ("DAM", @"SYSTEM\CurrentControlSet\Services\dam\State\UserSettings"),
        // Older builds nested the data one level shallower.
        ("BAM", @"SYSTEM\CurrentControlSet\Services\bam\UserSettings"),
    };

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int users = 0;
        int entries = 0;
        bool anyRootFound = false;

        foreach (var (provider, path) in Roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var root = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, path);
            if (root == null) continue;
            anyRootFound = true;

            foreach (var sid in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                users++;

                using var sidKey = SafeOpen(root, sid);
                if (sidKey == null) continue;

                foreach (var valueName in sidKey.GetValueNames())
                {
                    if (string.IsNullOrEmpty(valueName)) continue; // skip default/sequence values

                    try
                    {
                        var bytes = RegistryReader.TryGetBytes(sidKey, valueName);
                        if (bytes == null || bytes.Length < 8) continue;

                        var lastRun = ForensicTime.FromFileTimeBytes(bytes);
                        string normalizedPath = NormalizeDevicePath(valueName);

                        var suspicion = SuspicionHeuristics.InspectPath(normalizedPath);
                        string suffix = suspicion.Count > 0
                            ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}"
                            : string.Empty;

                        builder.AddFinding(
                            category: "ExecutionEvidence",
                            description: normalizedPath,
                            source: $"{provider}\\{sid}",
                            timestampUtc: lastRun,
                            rawValue: $"LastRun: {lastRun?.ToString("o") ?? "N/A"} | SID: {sid}{suffix}");
                        entries++;
                    }
                    catch (Exception ex)
                    {
                        builder.AddError($"Failed reading {provider} value '{valueName}' under {sid}: {ex.Message}");
                    }
                }
            }
        }

        if (!anyRootFound)
        {
            builder.AddError("No BAM/DAM registry roots found (unexpected on Win10/11).")
                   .ForceStatus(ScanStatus.Failure);
        }

        builder.SetMetadata("UsersEnumerated", users)
               .SetMetadata("EntriesCollected", entries);

        return Task.FromResult(builder.Build());
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, writable: false); }
        catch { return null; }
    }

    /// <summary>
    /// BAM stores NT object paths like "\Device\HarddiskVolume3\Windows\..." — map the
    /// volume prefix to a readable "\\...\\" form so paths correlate with other modules.
    /// </summary>
    private static string NormalizeDevicePath(string raw)
    {
        const string devicePrefix = @"\Device\HarddiskVolume";
        if (raw.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
        {
            int slash = raw.IndexOf('\\', devicePrefix.Length);
            if (slash > 0 && slash < raw.Length - 1)
            {
                // Drop "\Device\HarddiskVolumeN", keep the rest as a rooted path.
                return raw.Substring(slash);
            }
        }
        return raw;
    }
}
