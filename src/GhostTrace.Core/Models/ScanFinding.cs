namespace GhostTrace.Core.Models;

/// <summary>
/// Represents a single piece of evidence collected by a scan module.
/// Immutable by design — all properties are init-only.
/// </summary>
/// <param name="Category">
/// Logical grouping of the finding (e.g., "Process", "RegistryKey", "OpenPort").
/// Defined by the producing module.
/// </param>
/// <param name="Description">
/// Human-readable summary of what was found.
/// </param>
/// <param name="Source">
/// Where the evidence was collected from (e.g., a registry path, PID, file path).
/// </param>
/// <param name="TimestampUtc">
/// Optional UTC timestamp associated with the finding itself
/// (e.g., process start time, file last-write time).
/// <c>null</c> when not applicable.
/// </param>
/// <param name="RawValue">
/// Optional machine-readable value for serialization and downstream processing
/// (e.g., a hash, a numeric PID, a full command line).
/// <c>null</c> when not applicable.
/// </param>
public sealed record ScanFinding(
    string Category,
    string Description,
    string Source,
    DateTimeOffset? TimestampUtc = null,
    string? RawValue = null);
