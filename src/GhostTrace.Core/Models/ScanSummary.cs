using GhostTrace.Core.Enums;

namespace GhostTrace.Core.Models;

/// <summary>
/// Numeric and temporal summary of a scan execution.
/// Immutable by design — all properties are init-only.
/// Intended for reports, dashboards, and quick audit review.
/// Module identity is not duplicated here; it is already
/// available on the <see cref="Abstractions.IScanResult"/> that
/// this summary is derived from.
/// </summary>
/// <param name="Status">
/// Final outcome of the scan execution.
/// </param>
/// <param name="FindingCount">
/// Total number of evidence items collected.
/// </param>
/// <param name="ErrorCount">
/// Total number of errors encountered.
/// </param>
/// <param name="CompletedAtUtc">
/// UTC timestamp marking when the scan finished.
/// </param>
/// <param name="Duration">
/// Wall-clock time elapsed during the scan execution.
/// </param>
public sealed record ScanSummary(
    ScanStatus Status,
    int FindingCount,
    int ErrorCount,
    DateTimeOffset CompletedAtUtc,
    TimeSpan Duration);
