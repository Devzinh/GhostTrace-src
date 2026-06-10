using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Persistence;

/// <summary>
/// Inspects high-value Autostart Extensibility Points (ASEPs) that legitimate software
/// rarely touches but attackers favour for persistence / privilege:
///   · Winlogon Userinit / Shell hijacks      (T1547.004)
///   · Image File Execution Options debuggers  (T1546.012)
///   · AppInit_DLLs injection                  (T1546.010)
///   · LSA security / authentication packages  (T1547.002 / T1556)
///   · Active Setup StubPath                    (T1547.014)
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AsepScanModule : IScanModule
{
    public string Name => "AsepScanModule";

    private const string WinlogonPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string WindowsPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows";
    private const string IfeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private const string LsaPath = @"SYSTEM\CurrentControlSet\Control\Lsa";
    private const string ActiveSetupPath = @"SOFTWARE\Microsoft\Active Setup\Installed Components";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new ScanResultBuilder(Name);

        CheckWinlogon(builder, cancellationToken);
        CheckAppInit(builder);
        CheckIfeoDebuggers(builder, cancellationToken);
        CheckLsaPackages(builder);
        CheckActiveSetup(builder, cancellationToken);

        return Task.FromResult(builder.Build());
    }

    private static void CheckWinlogon(ScanResultBuilder builder, CancellationToken ct)
    {
        using var key = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, WinlogonPath);
        if (key == null) { builder.AddError($"Cannot open HKLM\\{WinlogonPath}."); return; }

        string? userinit = RegistryReader.TryGetString(key, "Userinit");
        string? shell = RegistryReader.TryGetString(key, "Shell");

        // Default Userinit is "<sys>\userinit.exe," — anything extra is a hijack.
        if (!string.IsNullOrEmpty(userinit) &&
            !userinit.TrimEnd(',').EndsWith(@"\userinit.exe", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddFinding("AsepWinlogon", "Userinit modified", $"HKLM\\{WinlogonPath}",
                rawValue: $"Userinit: {userinit} | EXPECTED: userinit.exe");
        }

        // Default Shell is "explorer.exe".
        if (!string.IsNullOrEmpty(shell) && !shell.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddFinding("AsepWinlogon", "Shell modified", $"HKLM\\{WinlogonPath}",
                rawValue: $"Shell: {shell} | EXPECTED: explorer.exe");
        }

        foreach (var extra in new[] { "Taskman", "GinaDLL", "AppSetup" })
        {
            string? v = RegistryReader.TryGetString(key, extra);
            if (!string.IsNullOrEmpty(v))
            {
                builder.AddFinding("AsepWinlogon", $"{extra} present", $"HKLM\\{WinlogonPath}",
                    rawValue: $"{extra}: {v}");
            }
        }
    }

    private static void CheckAppInit(ScanResultBuilder builder)
    {
        using var key = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, WindowsPath);
        if (key == null) { builder.AddError($"Cannot open HKLM\\{WindowsPath}."); return; }

        string? appInit = RegistryReader.TryGetString(key, "AppInit_DLLs");
        int? load = RegistryReader.TryGetInt(key, "LoadAppInit_DLLs");

        if (!string.IsNullOrWhiteSpace(appInit))
        {
            builder.AddFinding("AsepAppInit", "AppInit_DLLs populated", $"HKLM\\{WindowsPath}",
                rawValue: $"AppInit_DLLs: {appInit} | LoadAppInit_DLLs: {load?.ToString() ?? "?"}");
        }
    }

    private static void CheckIfeoDebuggers(ScanResultBuilder builder, CancellationToken ct)
    {
        using var ifeo = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, IfeoPath);
        if (ifeo == null) { builder.AddError($"Cannot open HKLM\\{IfeoPath}."); return; }

        foreach (var exe in ifeo.GetSubKeyNames())
        {
            ct.ThrowIfCancellationRequested();
            using var sub = SafeOpen(ifeo, exe);
            if (sub == null) continue;

            string? debugger = RegistryReader.TryGetString(sub, "Debugger");
            if (!string.IsNullOrEmpty(debugger))
            {
                var reasons = SuspicionHeuristics.InspectPath(debugger);
                string suffix = reasons.Count > 0 ? $" | SUSPICIOUS: {string.Join(", ", reasons)}" : "";
                builder.AddFinding("AsepIFEO", $"Debugger set for {exe}", $"HKLM\\{IfeoPath}\\{exe}",
                    rawValue: $"Debugger: {debugger}{suffix}");
            }
        }
    }

    private static void CheckLsaPackages(ScanResultBuilder builder)
    {
        using var key = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, LsaPath);
        if (key == null) { builder.AddError($"Cannot open HKLM\\{LsaPath}."); return; }

        foreach (var valueName in new[] { "Security Packages", "Authentication Packages", "Notification Packages" })
        {
            if (key.GetValue(valueName) is string[] packages && packages.Length > 0)
            {
                foreach (var pkg in packages)
                {
                    if (string.IsNullOrWhiteSpace(pkg) || pkg == "\"\"") continue;
                    bool known = IsKnownLsaPackage(pkg);
                    builder.AddFinding(
                        "AsepLSA",
                        $"{valueName}: {pkg}",
                        $"HKLM\\{LsaPath}",
                        rawValue: known ? "known package" : "UNKNOWN package — review for LSA injection");
                }
            }
        }
    }

    private static void CheckActiveSetup(ScanResultBuilder builder, CancellationToken ct)
    {
        using var root = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, ActiveSetupPath);
        if (root == null) return; // Active Setup is optional

        foreach (var guid in root.GetSubKeyNames())
        {
            ct.ThrowIfCancellationRequested();
            using var sub = SafeOpen(root, guid);
            if (sub == null) continue;

            string? stub = RegistryReader.TryGetString(sub, "StubPath");
            if (string.IsNullOrEmpty(stub)) continue;

            var reasons = SuspicionHeuristics.InspectPath(stub);
            if (reasons.Count == 0 && !SuspicionHeuristics.IsSuspiciousCommand(stub)) continue;

            builder.AddFinding("AsepActiveSetup", $"StubPath {guid}", $"HKLM\\{ActiveSetupPath}\\{guid}",
                rawValue: $"StubPath: {stub} | SUSPICIOUS");
        }
    }

    private static bool IsKnownLsaPackage(string pkg)
    {
        string p = pkg.Trim().ToLowerInvariant();
        return p is "kerberos" or "msv1_0" or "schannel" or "wdigest" or "tspkg"
            or "pku2u" or "cloudap" or "negotiate" or "livessp" or "scram"
            or "rassfm" or "scecli" or "wsc_proxy";
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, writable: false); }
        catch { return null; }
    }
}
