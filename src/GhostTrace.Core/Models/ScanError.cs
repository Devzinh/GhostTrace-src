namespace GhostTrace.Core.Models;

/// <summary>
/// Represents an error encountered during a scan module execution.
/// Immutable by design — all properties are init-only.
/// This is a domain-level record for auditing and reporting,
/// not a replacement for .NET exception handling.
/// </summary>
/// <param name="ModuleName">
/// Name of the module that encountered the error (e.g., "ProcessScanner").
/// Enables correlation with <see cref="ScanFinding.Source"/> and scan results.
/// </param>
/// <param name="Message">
/// Human-readable description of what went wrong.
/// </param>
/// <param name="OccurredAtUtc">
/// UTC timestamp of when the error was observed.
/// </param>
/// <param name="Detail">
/// Optional additional context (e.g., exception type name, registry key that
/// could not be read, access-denied path). <c>null</c> when not applicable.
/// </param>
public sealed record ScanError(
    string ModuleName,
    string Message,
    DateTimeOffset OccurredAtUtc,
    string? Detail = null);
