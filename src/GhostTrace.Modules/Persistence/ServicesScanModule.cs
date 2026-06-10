using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Persistence;

/// <summary>
/// Enumerates Windows services and kernel drivers from the registry, surfacing those
/// whose ImagePath looks abnormal (auto-start services / drivers launched from user-
/// writable directories are a classic persistence and rootkit vector — MITRE T1543.003).
///
///   HKLM\SYSTEM\CurrentControlSet\Services\{name}
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServicesScanModule : IScanModule
{
    public string Name => "ServicesScanModule";

    private const string ServicesPath = @"SYSTEM\CurrentControlSet\Services";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);

        using var services = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, ServicesPath);
        if (services == null)
        {
            builder.AddError($"Unable to open HKLM\\{ServicesPath}.").ForceStatus(ScanStatus.Failure);
            return Task.FromResult(builder.Build());
        }

        int total = 0;
        int suspicious = 0;
        int autoStart = 0;

        foreach (var name in services.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var svc = SafeOpen(services, name);
            if (svc == null) continue;

            total++;

            string? imagePath = RegistryReader.TryGetString(svc, "ImagePath");
            if (string.IsNullOrEmpty(imagePath)) continue; // services without a binary (e.g. groups) are uninteresting here

            int? start = RegistryReader.TryGetInt(svc, "Start");
            int? type = RegistryReader.TryGetInt(svc, "Type");
            string? display = RegistryReader.TryGetString(svc, "DisplayName");
            string? account = RegistryReader.TryGetString(svc, "ObjectName");

            string startStr = StartTypeName(start);
            string typeStr = ServiceTypeName(type);
            if (start is 0 or 1 or 2) autoStart++;

            // Heuristic flagging on the binary path inside the (possibly quoted) ImagePath.
            string binary = ExtractBinary(imagePath);
            var reasons = SuspicionHeuristics.InspectPath(binary);

            bool flag = reasons.Count > 0;
            if (flag) suspicious++;

            // Only emit a finding for flagged services to keep the report focused;
            // everything else is counted in metadata.
            if (flag)
            {
                builder.AddFinding(
                    category: "ServicePersistence",
                    description: display ?? name,
                    source: $"HKLM\\{ServicesPath}\\{name}",
                    timestampUtc: null,
                    rawValue: $"Start: {startStr} | Type: {typeStr} | Account: {account ?? "-"} | " +
                              $"ImagePath: {imagePath} | SUSPICIOUS: {string.Join(", ", reasons)}");
            }
        }

        builder.SetMetadata("ServicesEnumerated", total)
               .SetMetadata("AutoStartServices", autoStart)
               .SetMetadata("SuspiciousServices", suspicious);

        // A clean machine legitimately yields zero suspicious services — that is Success,
        // which the builder already derives (no findings, no errors → Success).
        return Task.FromResult(builder.Build());
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, writable: false); }
        catch { return null; }
    }

    private static string ExtractBinary(string imagePath)
    {
        string p = imagePath.Trim();
        if (p.StartsWith("\""))
        {
            int end = p.IndexOf('"', 1);
            if (end > 1) return p.Substring(1, end - 1);
        }
        // Strip a leading NT path prefix and take up to the first space (arguments).
        if (p.StartsWith(@"\??\", StringComparison.Ordinal)) p = p.Substring(4);
        int space = p.IndexOf(' ');
        return space > 0 ? p.Substring(0, space) : p;
    }

    private static string StartTypeName(int? start) => start switch
    {
        0 => "Boot",
        1 => "System",
        2 => "Automatic",
        3 => "Manual",
        4 => "Disabled",
        _ => start?.ToString() ?? "?"
    };

    private static string ServiceTypeName(int? type) => type switch
    {
        1 => "KernelDriver",
        2 => "FileSystemDriver",
        16 => "OwnProcess",
        32 => "ShareProcess",
        272 => "OwnProcess(Interactive)",
        288 => "ShareProcess(Interactive)",
        _ => type.HasValue ? $"0x{type:X}" : "?"
    };
}
