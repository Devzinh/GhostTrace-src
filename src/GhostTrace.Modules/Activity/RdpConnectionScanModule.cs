using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Activity;

/// <summary>
/// Recovers outbound RDP (Terminal Services Client) connection history per user — the
/// list of remote hosts a user connected TO, plus the username hint cached for each.
/// Useful for tracing lateral movement (MITRE T1021.001).
///
///   HKU\{SID}\Software\Microsoft\Terminal Server Client\Servers\{host}
///   HKU\{SID}\Software\Microsoft\Terminal Server Client\Default\MRU*
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RdpConnectionScanModule : IScanModule
{
    public string Name => "RdpConnectionScanModule";

    private const string ServersSubPath = @"Software\Microsoft\Terminal Server Client\Servers";
    private const string DefaultSubPath = @"Software\Microsoft\Terminal Server Client\Default";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new ScanResultBuilder(Name);

        using var users = RegistryReader.OpenReadOnly(RegistryHive.Users, string.Empty);
        if (users == null)
        {
            builder.AddError("Unable to open HKEY_USERS.").ForceStatus(Core.Enums.ScanStatus.Failure);
            return Task.FromResult(builder.Build());
        }

        int hosts = 0;
        int mruEntries = 0;

        foreach (var sid in users.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)) continue;

            // Per-host cached connections (with username hints).
            using (var servers = SafeOpen(users, $@"{sid}\{ServersSubPath}"))
            {
                if (servers != null)
                {
                    foreach (var host in servers.GetSubKeyNames())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using var hostKey = SafeOpen(servers, host);
                        string? hint = hostKey != null ? RegistryReader.TryGetString(hostKey, "UsernameHint") : null;

                        builder.AddFinding(
                            category: "RdpConnection",
                            description: host,
                            source: $"HKU\\{sid}\\...\\Terminal Server Client\\Servers",
                            timestampUtc: null,
                            rawValue: $"RemoteHost: {host} | UsernameHint: {hint ?? "-"}");
                        hosts++;
                    }
                }
            }

            // The MRU list captures hosts typed into the mstsc dialog (may include ones
            // not in Servers\ if the connection was never completed).
            using (var def = SafeOpen(users, $@"{sid}\{DefaultSubPath}"))
            {
                if (def != null)
                {
                    foreach (var valueName in def.GetValueNames())
                    {
                        if (!valueName.StartsWith("MRU", StringComparison.OrdinalIgnoreCase)) continue;
                        string? target = RegistryReader.TryGetString(def, valueName);
                        if (string.IsNullOrEmpty(target)) continue;

                        builder.AddFinding(
                            category: "RdpConnection",
                            description: target,
                            source: $"HKU\\{sid}\\...\\Terminal Server Client\\Default\\{valueName}",
                            timestampUtc: null,
                            rawValue: $"MRU target: {target}");
                        mruEntries++;
                    }
                }
            }
        }

        builder.SetMetadata("HostsCollected", hosts)
               .SetMetadata("MruEntries", mruEntries);

        return Task.FromResult(builder.Build());
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string sub)
    {
        try { return parent.OpenSubKey(sub, writable: false); }
        catch { return null; }
    }
}
