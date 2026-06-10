using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GhostTrace.Modules.Common;

/// <summary>
/// Lightweight, path/command based heuristics used to flag artifacts that are
/// statistically more likely to be malicious. These are *hints* for an analyst, never
/// verdicts — GhostTrace is read-only and does not quarantine or score conclusively.
/// </summary>
public static class SuspicionHeuristics
{
    // Directories that legitimate, signed software rarely launches persistent binaries from.
    private static readonly string[] SuspiciousDirectories =
    {
        @"\appdata\local\temp\",
        @"\appdata\roaming\",
        @"\appdata\local\",
        @"\windows\temp\",
        @"\temp\",
        @"\programdata\",
        @"\users\public\",
        @"\$recycle.bin\",
        @"\perflogs\",
        @"\downloads\",
        @"\windows\fonts\",
        @"\windows\system32\tasks\",
    };

    // Living-off-the-land binaries frequently abused for execution / download.
    private static readonly string[] LolBins =
    {
        "powershell", "pwsh", "cmd.exe", "wscript", "cscript", "mshta", "rundll32",
        "regsvr32", "certutil", "bitsadmin", "msbuild", "installutil", "regasm",
        "regsvcs", "wmic", "forfiles", "schtasks", "curl.exe", "scrcons",
    };

    // Substrings that strongly indicate obfuscated / download-and-execute commands.
    private static readonly string[] MaliciousCommandMarkers =
    {
        "downloadstring", "downloadfile", "frombase64string", "-enc", "-e ",
        "-encodedcommand", "invoke-expression", "iex(", "iex ", "webclient",
        "invoke-webrequest", "bypass", "-nop", "-noprofile", "hidden",
        "start-bitstransfer", "reflection.assembly", "shellcode", "virtualalloc",
        "createremotethread", "::frombase64", "[convert]", "gzipstream",
    };

    private static readonly Regex DoubleExtension =
        new(@"\.(pdf|doc|docx|xls|xlsx|jpg|png|txt|rtf)\.(exe|scr|bat|cmd|com|pif|vbs|js)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns the list of reasons a path looks suspicious (empty when nothing matched).
    /// </summary>
    public static IReadOnlyList<string> InspectPath(string? path)
    {
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(path)) return reasons;

        string lower = path.ToLowerInvariant();

        foreach (var dir in SuspiciousDirectories)
        {
            if (lower.Contains(dir))
            {
                reasons.Add($"path under '{dir.Trim('\\')}'");
                break;
            }
        }

        foreach (var lol in LolBins)
        {
            if (lower.Contains(lol))
            {
                reasons.Add($"references LOLBin '{lol}'");
                break;
            }
        }

        if (DoubleExtension.IsMatch(lower))
        {
            reasons.Add("double extension (masquerading)");
        }

        return reasons;
    }

    /// <summary>
    /// Returns the malicious-command markers present in a command line / script line.
    /// </summary>
    public static IReadOnlyList<string> InspectCommand(string? command)
    {
        var hits = new List<string>();
        if (string.IsNullOrWhiteSpace(command)) return hits;

        string lower = command.ToLowerInvariant();
        foreach (var marker in MaliciousCommandMarkers)
        {
            if (lower.Contains(marker)) hits.Add(marker.Trim());
        }
        return hits;
    }

    /// <summary>True when the path/command has any suspicion marker.</summary>
    public static bool IsSuspiciousPath(string? path) => InspectPath(path).Count > 0;
    public static bool IsSuspiciousCommand(string? command) => InspectCommand(command).Count > 0;
}
