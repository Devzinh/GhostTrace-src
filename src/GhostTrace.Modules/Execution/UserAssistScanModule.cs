using System;
using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Execution;

/// <summary>
/// Parses UserAssist — Explorer's record of GUI program launches, kept per user. Value
/// names are ROT13-encoded paths; the 72-byte value data carries a run count (offset 4)
/// and the last-execution FILETIME (offset 60). Strong evidence of *interactive* execution.
///
///   HKU\{SID}\Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\{GUID}\Count
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UserAssistScanModule : IScanModule
{
    public string Name => "UserAssistScanModule";

    private const string UserAssistSubPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    private const int RunCountOffset = 4;
    private const int LastExecOffset = 60;
    private const int MinDataLength = 68; // need at least up to FILETIME end (60+8)

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int usersWithData = 0;
        int entries = 0;

        using var users = RegistryReader.OpenReadOnly(RegistryHive.Users, string.Empty);
        if (users == null)
        {
            builder.AddError("Unable to open HKEY_USERS.").ForceStatus(ScanStatus.Failure);
            return Task.FromResult(builder.Build());
        }

        foreach (var sid in users.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip the per-user _Classes hives — UserAssist lives in the main hive only.
            if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)) continue;

            using var uaRoot = SafeOpen(users, $@"{sid}\{UserAssistSubPath}");
            if (uaRoot == null) continue;

            bool userHadData = false;

            foreach (var guid in uaRoot.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var countKey = SafeOpen(uaRoot, $@"{guid}\Count");
                if (countKey == null) continue;

                foreach (var encodedName in countKey.GetValueNames())
                {
                    if (string.IsNullOrEmpty(encodedName)) continue;

                    string name = Rot13.Transform(encodedName);

                    // Control/aggregate entries are not program launches.
                    if (name.StartsWith("UEME_CTL", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        var data = RegistryReader.TryGetBytes(countKey, encodedName);
                        int runCount = 0;
                        DateTimeOffset? lastRun = null;

                        if (data != null && data.Length >= MinDataLength)
                        {
                            runCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(RunCountOffset, 4));
                            lastRun = ForensicTime.FromFileTimeBytes(data.AsSpan(LastExecOffset, 8));
                        }

                        // Skip registered-but-never-run entries: no run count and no
                        // execution timestamp carry no evidentiary value and only add noise.
                        if (runCount == 0 && lastRun == null) continue;

                        var suspicion = SuspicionHeuristics.InspectPath(name);
                        string suffix = suspicion.Count > 0
                            ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}"
                            : string.Empty;

                        builder.AddFinding(
                            category: "ExecutionEvidence",
                            description: name,
                            source: $"HKU\\{sid}\\...\\UserAssist\\{guid}",
                            timestampUtc: lastRun,
                            rawValue: $"RunCount: {runCount} | LastRun: {lastRun?.ToString("o") ?? "N/A"}{suffix}");
                        entries++;
                        userHadData = true;
                    }
                    catch (Exception ex)
                    {
                        builder.AddError($"Failed decoding UserAssist value under {sid}: {ex.Message}");
                    }
                }
            }

            if (userHadData) usersWithData++;
        }

        builder.SetMetadata("UsersWithData", usersWithData)
               .SetMetadata("EntriesCollected", entries);

        return Task.FromResult(builder.Build());
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string sub)
    {
        try { return parent.OpenSubKey(sub, writable: false); }
        catch { return null; }
    }
}
