using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Execution;

/// <summary>
/// Reads Shell MUICache — Explorer records an entry the first time it loads an
/// executable's resources, so presence here is corroborating execution evidence and
/// exposes the binary's embedded FriendlyAppName / company (useful for masquerading).
///
///   HKU\{SID}_Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MuiCacheScanModule : IScanModule
{
    public string Name => "MuiCacheScanModule";

    private const string MuiCacheSubPath =
        @"Local Settings\Software\Microsoft\Windows\Shell\MuiCache";

    private const string FriendlySuffix = ".FriendlyAppName";
    private const string CompanySuffix = ".ApplicationCompany";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int apps = 0;

        using var users = RegistryReader.OpenReadOnly(RegistryHive.Users, string.Empty);
        if (users == null)
        {
            builder.AddError("Unable to open HKEY_USERS.").ForceStatus(ScanStatus.Failure);
            return Task.FromResult(builder.Build());
        }

        foreach (var sid in users.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // MUICache lives in the per-user _Classes hive.
            if (!sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)) continue;

            using var mui = SafeOpen(users, $@"{sid}\{MuiCacheSubPath}");
            if (mui == null) continue;

            // Collapse the per-app value pairs (path.FriendlyAppName / path.ApplicationCompany)
            // back into one finding per executable path.
            var byPath = new Dictionary<string, (string? Friendly, string? Company)>(StringComparer.OrdinalIgnoreCase);

            foreach (var valueName in mui.GetValueNames())
            {
                if (string.IsNullOrEmpty(valueName)) continue;

                cancellationToken.ThrowIfCancellationRequested();

                string? path = null;
                bool isFriendly = false, isCompany = false;

                if (valueName.EndsWith(FriendlySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    path = valueName[..^FriendlySuffix.Length];
                    isFriendly = true;
                }
                else if (valueName.EndsWith(CompanySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    path = valueName[..^CompanySuffix.Length];
                    isCompany = true;
                }
                else
                {
                    path = valueName; // bare path entry
                }

                if (string.IsNullOrEmpty(path)) continue;

                string? value = RegistryReader.TryGetString(mui, valueName);
                byPath.TryGetValue(path, out var cur);
                if (isFriendly) cur.Friendly = value;
                else if (isCompany) cur.Company = value;
                byPath[path] = cur;
            }

            foreach (var kvp in byPath)
            {
                string path = kvp.Key;
                var (friendly, company) = kvp.Value;

                var suspicion = SuspicionHeuristics.InspectPath(path);
                string suffix = suspicion.Count > 0
                    ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}"
                    : string.Empty;

                builder.AddFinding(
                    category: "ExecutionEvidence",
                    description: path,
                    source: $"HKU\\{sid}\\...\\Shell\\MuiCache",
                    timestampUtc: null,
                    rawValue: $"Friendly: {friendly ?? "-"} | Company: {company ?? "-"}{suffix}");
                apps++;
            }
        }

        builder.SetMetadata("AppsCollected", apps);
        return Task.FromResult(builder.Build());
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string sub)
    {
        try { return parent.OpenSubKey(sub, writable: false); }
        catch { return null; }
    }
}
