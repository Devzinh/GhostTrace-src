using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.Prefetch;

[SupportedOSPlatform("windows")]
public sealed class PrefetchScanModule : IScanModule
{
    public string Name => "PrefetchScanModule";

    // Resolved from the actual Windows directory (not hardcoded C:\) so systems with
    // the OS installed on another volume are still scanned correctly.
    private static readonly string PrefetchDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

    public async Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int totalFiles = 0, totalParsed = 0, totalSkipped = 0;

        if (!Directory.Exists(PrefetchDir))
        {
            return builder
                .AddError($"Prefetch directory not found at {PrefetchDir}")
                .ForceStatus(ScanStatus.Failure)
                .SetMetadata("TotalFiles", 0).SetMetadata("TotalParsed", 0)
                .SetMetadata("TotalErrors", 0).SetMetadata("TotalSkipped", 0)
                .Build();
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(PrefetchDir, "*.pf", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            return builder
                .AddError($"Error accessing Prefetch directory: {ex.Message}")
                .ForceStatus(ScanStatus.Failure)
                .SetMetadata("TotalFiles", 0).SetMetadata("TotalParsed", 0)
                .SetMetadata("TotalErrors", 1).SetMetadata("TotalSkipped", 0)
                .Build();
        }

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalFiles++;

            try
            {
                byte[] rawBytes = await File.ReadAllBytesAsync(filePath, cancellationToken)
                    .ConfigureAwait(false);
                var entry = PrefetchParser.Parse(filePath, rawBytes);

                int historyCount = entry.AllRunTimesUtc is { Length: > 0 } ? entry.AllRunTimesUtc.Length - 1 : 0;
                string lastRunStr = entry.LastRunTimeUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "N/A";

                builder.AddFinding(
                    category: "ExecutionEvidence",
                    description: entry.FileName,
                    source: filePath,
                    timestampUtc: entry.LastRunTimeUtc,
                    rawValue: $"Hash: {entry.PrefetchHash} | Ver: {entry.FileVersion} | RunCount: {entry.RunCount} | LastRun: {lastRunStr} | PrevRuns: {historyCount}");
                totalParsed++;
            }
            catch (UnsupportedPrefetchVersionException)
            {
                // Locked/running-process prefetch (zero header) or out-of-scope OS version.
                // Neither is a scan failure — count as skipped, not errored.
                totalSkipped++;
            }
            catch (PrefetchFormatException ex)
            {
                builder.AddError($"Parse failed for {Path.GetFileName(filePath)}: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                builder.AddError($"Unexpected error processing {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        builder.SetMetadata("TotalFiles", totalFiles)
               .SetMetadata("TotalParsed", totalParsed)
               .SetMetadata("TotalErrors", builder.ErrorCount)
               .SetMetadata("TotalSkipped", totalSkipped);

        if (totalFiles == 0)
        {
            builder.SetMetadata("Note", "Prefetch directory empty or feature disabled");
        }

        return builder.Build();
    }
}
