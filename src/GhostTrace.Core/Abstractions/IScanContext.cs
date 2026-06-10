namespace GhostTrace.Core.Abstractions;

/// <summary>
/// Represents the read-only execution context supplied to an <see cref="IScanModule"/>
/// during a forensic scan. Implementations must not expose operations that modify
/// the target system.
/// </summary>
public interface IScanContext
{
    /// <summary>
    /// Name of the module currently being executed (e.g., "ProcessScanner").
    /// Useful for logging, auditing, and result correlation.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Unique identifier for this scan session, enabling traceability
    /// across modules and audit records.
    /// </summary>
    Guid ScanId { get; }

    /// <summary>
    /// UTC timestamp marking when the scan session was initiated.
    /// Anchors all collected evidence to a single point in time.
    /// </summary>
    DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// Retrieves an optional execution metadata value by key.
    /// Returns <c>null</c> when the key is not present.
    /// </summary>
    /// <param name="key">Case-insensitive metadata key.</param>
    /// <returns>The metadata value, or <c>null</c> if not found.</returns>
    string? GetOption(string key);
}
