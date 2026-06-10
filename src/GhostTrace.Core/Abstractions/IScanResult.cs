using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Core.Abstractions;

/// <summary>
/// Represents the standardised outcome of a single <see cref="IScanModule"/> execution.
/// All properties are read-only — a result is immutable once produced.
/// </summary>
public interface IScanResult
{
    /// <summary>
    /// Name of the module that generated this result (e.g., "ProcessScanner").
    /// Enables correlation with <see cref="IScanContext.ModuleName"/>.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Overall outcome of the scan execution.
    /// </summary>
    ScanStatus Status { get; }

    /// <summary>
    /// UTC timestamp marking when the module finished executing.
    /// Together with <see cref="IScanContext.StartedAtUtc"/> allows duration calculation.
    /// </summary>
    DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// Evidence items collected during the scan.
    /// Empty when no findings were produced (e.g., on <see cref="ScanStatus.Failure"/>).
    /// </summary>
    IReadOnlyList<ScanFinding> Findings { get; }

    /// <summary>
    /// Errors encountered during the scan.
    /// Empty when the scan completed without issues.
    /// </summary>
    IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Optional execution metadata (e.g., record counts, skipped items, scan scope).
    /// Keys are case-insensitive. Empty when no metadata is relevant.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
