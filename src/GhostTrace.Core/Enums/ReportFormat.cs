namespace GhostTrace.Core.Enums;

/// <summary>
/// Identifies the output format produced by an <see cref="Abstractions.IReportWriter"/>.
/// </summary>
public enum ReportFormat
{
    /// <summary>Plain-text report.</summary>
    Text = 0,

    /// <summary>Comma-separated values.</summary>
    Csv = 1,

    /// <summary>JSON document.</summary>
    Json = 2,

    /// <summary>HTML document.</summary>
    Html = 3
}
