namespace GhostTrace.Core.Abstractions;

/// <summary>
/// Represents a single forensic scan module that collects data in a read-only manner.
/// Each implementation addresses one specific area of evidence collection
/// (e.g., running processes, registry hives, network connections).
/// </summary>
public interface IScanModule
{
    /// <summary>
    /// Human-readable name that identifies this module (e.g., "ProcessScanner").
    /// Used for logging, reporting, and module selection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the read-only scan against the target described in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">
    /// Provides environment information and scan parameters.
    /// Must not be used to modify the target system.
    /// </param>
    /// <param name="cancellationToken">
    /// Allows cooperative cancellation of long-running scans.
    /// </param>
    /// <returns>
    /// An <see cref="IScanResult"/> containing the collected evidence and status metadata.
    /// </returns>
    Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default);
}
