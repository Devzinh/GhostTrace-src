using GhostTrace.Core.Enums;

namespace GhostTrace.Core.Models;

/// <summary>
/// Metadata describing a forensic report to be generated.
/// Immutable by design — all properties are init-only.
/// Contains no report content, no I/O handles, and no
/// physical persistence details.
/// </summary>
/// <param name="Format">
/// Output format the report should be rendered in.
/// </param>
/// <param name="Title">
/// Logical name for the report (e.g., "Incident-2024-0042 – Full Scan").
/// Used as a heading or file-name hint by writers.
/// </param>
/// <param name="ScanId">
/// Identifier of the scan session this report covers.
/// Enables traceability back to <see cref="Abstractions.IScanContext.ScanId"/>.
/// </param>
/// <param name="GeneratedAtUtc">
/// UTC timestamp marking when the report was requested.
/// </param>
public sealed record ReportDescriptor(
    ReportFormat Format,
    string Title,
    Guid ScanId,
    DateTimeOffset GeneratedAtUtc);
