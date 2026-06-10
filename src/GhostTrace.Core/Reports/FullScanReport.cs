using System;
using System.Collections.Generic;
using GhostTrace.Core.Enums;

namespace GhostTrace.Core.Reports;

public sealed record FullScanReport
{
    public DateTimeOffset ScanStartedAt { get; init; }
    public DateTimeOffset ScanFinishedAt { get; init; }
    public TimeSpan Duration => ScanFinishedAt - ScanStartedAt;
    public string MachineName { get; init; } = string.Empty;
    public string OsVersion { get; init; } = string.Empty;
    public string? FilterTerm { get; init; }
    public int TotalFindings { get; init; }
    public int TotalMatches { get; init; }
    public IReadOnlyList<ModuleScanResult> ModuleResults { get; init; } = Array.Empty<ModuleScanResult>();

    /// <summary>
    /// The most severe module status, ignoring <see cref="ScanStatus.Skipped"/> (a skipped
    /// module never worsens the overall outcome). Centralises the rule previously
    /// duplicated across the report writers and the CLI.
    /// </summary>
    public ScanStatus WorstStatus
    {
        get
        {
            var worst = ScanStatus.Success;
            foreach (var m in ModuleResults)
            {
                worst = m.Status switch
                {
                    ScanStatus.Failure => ScanStatus.Failure,
                    ScanStatus.PartialSuccess when worst != ScanStatus.Failure => ScanStatus.PartialSuccess,
                    _ => worst
                };
            }
            return worst;
        }
    }
}
