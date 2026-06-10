using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.SystemArtifacts;

/// <summary>
/// Collects two host-network artifacts:
///   · the hosts file — manual domain redirections are a common malware / adware tactic
///     (T1565.001) and are surfaced individually;
///   · the NetworkList profiles — every network the machine has joined, with first and
///     last connection timestamps (placing a device on a given network at a given time).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NetworkArtifactsScanModule : IScanModule
{
    public string Name => "NetworkArtifactsScanModule";

    private const string NetworkListPath =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Profiles";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new ScanResultBuilder(Name);

        InspectHostsFile(builder, cancellationToken);
        InspectNetworkProfiles(builder, cancellationToken);

        return Task.FromResult(builder.Build());
    }

    private static void InspectHostsFile(ScanResultBuilder builder, CancellationToken ct)
    {
        string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string hostsPath = Path.Combine(sys, "drivers", "etc", "hosts");

        int customEntries = 0;
        try
        {
            if (!File.Exists(hostsPath))
            {
                builder.AddError($"hosts file not found at {hostsPath}");
            }
            else
            {
                DateTimeOffset modified = new FileInfo(hostsPath).LastWriteTimeUtc;
                foreach (var rawLine in File.ReadLines(hostsPath))
                {
                    ct.ThrowIfCancellationRequested();
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    // A real mapping line: "<ip> <hostname...>". Default Windows hosts
                    // ships with only comments, so any active line is noteworthy.
                    builder.AddFinding(
                        category: "HostsEntry",
                        description: line,
                        source: hostsPath,
                        timestampUtc: modified,
                        rawValue: $"Active hosts mapping: {line}");
                    customEntries++;
                }
            }
        }
        catch (Exception ex)
        {
            builder.AddError($"Failed reading hosts file: {ex.Message}");
        }

        builder.SetMetadata("HostsCustomEntries", customEntries);
    }

    private static void InspectNetworkProfiles(ScanResultBuilder builder, CancellationToken ct)
    {
        using var profiles = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, NetworkListPath);
        if (profiles == null)
        {
            builder.AddError($"Cannot open HKLM\\{NetworkListPath}.");
            return;
        }

        int networks = 0;
        foreach (var guid in profiles.GetSubKeyNames())
        {
            ct.ThrowIfCancellationRequested();
            using var profile = SafeOpen(profiles, guid);
            if (profile == null) continue;

            string? profileName = RegistryReader.TryGetString(profile, "ProfileName");
            var created = ForensicTime.FromSystemTimeBytes(RegistryReader.TryGetBytes(profile, "DateCreated") ?? Array.Empty<byte>());
            var lastConnected = ForensicTime.FromSystemTimeBytes(RegistryReader.TryGetBytes(profile, "DateLastConnected") ?? Array.Empty<byte>());

            builder.AddFinding(
                category: "NetworkProfile",
                description: profileName ?? guid,
                source: $"HKLM\\{NetworkListPath}\\{guid}",
                timestampUtc: lastConnected ?? created,
                rawValue: $"Network: {profileName ?? "-"} | FirstConnected: {created?.ToString("o") ?? "N/A"} | " +
                          $"LastConnected: {lastConnected?.ToString("o") ?? "N/A"}");
            networks++;
        }

        builder.SetMetadata("NetworkProfiles", networks);
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, writable: false); }
        catch { return null; }
    }
}
