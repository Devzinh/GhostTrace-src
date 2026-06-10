using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Core.Abstractions;

/// <summary>
/// Generates a forensic report from one or more scan results.
/// Each implementation targets a single <see cref="ReportFormat"/>
/// (e.g., plain text, CSV, JSON, HTML).
/// <para>
/// Output is written to a caller-supplied <see cref="Stream"/>,
/// keeping the contract decoupled from the file system or any
/// specific I/O infrastructure.
/// </para>
/// </summary>
public interface IReportWriter
{
    /// <summary>
    /// The output format this writer produces.
    /// </summary>
    ReportFormat Format { get; }

    /// <summary>
    /// Serialises the provided scan results into the writer's format
    /// and writes the output to <paramref name="destination"/>.
    /// </summary>
    /// <param name="descriptor">
    /// Metadata envelope describing the report.
    /// </param>
    /// <param name="results">
    /// One or more scan results to include in the report.
    /// Must not be empty.
    /// </param>
    /// <param name="destination">
    /// The writable stream that will receive the formatted output.
    /// The caller owns the stream and is responsible for flushing/closing it.
    /// </param>
    /// <param name="cancellationToken">
    /// Allows cooperative cancellation of the write operation.
    /// </param>
    Task WriteAsync(
        ReportDescriptor descriptor,
        IReadOnlyList<IScanResult> results,
        Stream destination,
        CancellationToken cancellationToken = default);
}
