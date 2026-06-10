using System;
using System.Collections.Generic;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Core.Reports;

public sealed record ModuleScanResult
{
    public string ModuleName { get; init; } = string.Empty;
    public ScanStatus Status { get; init; }
    public int TotalFindings { get; init; }
    public int TotalMatches { get; init; }
    public TimeSpan ModuleDuration { get; init; }
    public IReadOnlyList<ScanFinding> Findings { get; init; } = Array.Empty<ScanFinding>();
    public IReadOnlyList<ScanFinding> MatchedFindings { get; init; } = Array.Empty<ScanFinding>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
