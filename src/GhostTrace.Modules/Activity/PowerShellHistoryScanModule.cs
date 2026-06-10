using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.Activity;

/// <summary>
/// Reads every user's PSReadLine console history. This plaintext file records the exact
/// PowerShell commands a user (or attacker) typed and is one of the highest-signal
/// artifacts on a host — it frequently captures download-cradles and encoded payloads
/// verbatim (MITRE T1059.001). Lines matching malicious markers are flagged.
///
///   C:\Users\{user}\AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PowerShellHistoryScanModule : IScanModule
{
    public string Name => "PowerShellHistoryScanModule";

    private const string RelativeHistoryPath =
        @"AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt";

    private const int MaxLinesPerFile = 5000; // safety bound against pathological files

    public async Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new ScanResultBuilder(Name);

        string usersRoot = Path.Combine(
            Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\",
            "Users");

        int files = 0;
        int totalLines = 0;
        int flagged = 0;

        DirectoryInfo usersDir;
        try
        {
            usersDir = new DirectoryInfo(usersRoot);
            if (!usersDir.Exists)
            {
                builder.AddError($"Users directory not found: {usersRoot}").ForceStatus(Core.Enums.ScanStatus.Failure);
                return builder.Build();
            }
        }
        catch (Exception ex)
        {
            builder.AddError($"Cannot access {usersRoot}: {ex.Message}").ForceStatus(Core.Enums.ScanStatus.Failure);
            return builder.Build();
        }

        foreach (var userDir in EnumerateSafely(usersDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string historyPath = Path.Combine(userDir.FullName, RelativeHistoryPath);
            if (!File.Exists(historyPath)) continue;

            files++;
            try
            {
                DateTimeOffset? modified = null;
                try { modified = new FileInfo(historyPath).LastWriteTimeUtc; } catch { }

                int lineNo = 0;
                foreach (var line in await File.ReadAllLinesAsync(historyPath, cancellationToken).ConfigureAwait(false))
                {
                    if (++lineNo > MaxLinesPerFile) break;
                    totalLines++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var markers = SuspicionHeuristics.InspectCommand(line);
                    if (markers.Count == 0) continue; // only surface noteworthy commands

                    flagged++;
                    builder.AddFinding(
                        category: "PowerShellCommand",
                        description: Truncate(line, 200),
                        source: $"{userDir.Name}:{Path.GetFileName(historyPath)}:{lineNo}",
                        timestampUtc: modified,
                        rawValue: $"MARKERS: {string.Join(", ", markers)} | {Truncate(line, 400)}");
                }
            }
            catch (Exception ex)
            {
                builder.AddError($"Failed reading history for '{userDir.Name}': {ex.Message}");
            }
        }

        builder.SetMetadata("HistoryFilesFound", files)
               .SetMetadata("LinesScanned", totalLines)
               .SetMetadata("FlaggedCommands", flagged);

        return builder.Build();
    }

    private static System.Collections.Generic.IEnumerable<DirectoryInfo> EnumerateSafely(DirectoryInfo root)
    {
        DirectoryInfo[] dirs;
        try { dirs = root.GetDirectories(); }
        catch { yield break; }
        foreach (var d in dirs) yield return d;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}
