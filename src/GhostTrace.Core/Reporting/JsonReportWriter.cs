using System.Text.Json;
using System.Text.Json.Serialization;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Core.Reporting;

/// <summary>
/// Writes a forensic report as indented JSON to a caller-supplied <see cref="Stream"/>.
/// The output envelope contains the <see cref="ReportDescriptor"/> metadata
/// followed by the serialised scan results.
/// </summary>
public sealed class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };



    /// <inheritdoc />
    public ReportFormat Format => ReportFormat.Json;

    /// <inheritdoc />
    public async Task WriteAsync(
        ReportDescriptor descriptor,
        IReadOnlyList<IScanResult> results,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(destination);

        var envelope = new JsonReport(
            Title: descriptor.Title,
            ScanId: descriptor.ScanId,
            Format: descriptor.Format,
            GeneratedAtUtc: descriptor.GeneratedAtUtc,
            Results: results.Select(r => new JsonResultEntry(
                ModuleName: r.ModuleName,
                Status: r.Status,
                CompletedAtUtc: r.CompletedAtUtc,
                Findings: r.Findings,
                Errors: r.Errors,
                Metadata: r.Metadata)).ToList());

        await JsonSerializer.SerializeAsync(destination, envelope, SerializerOptions, cancellationToken)
                            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes any generic payload (such as a correlation result) as a JSON envelope.
    /// </summary>
    public async Task WritePayloadAsync<T>(
        ReportDescriptor descriptor,
        T payload,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(destination);

        var envelope = new
        {
            Title = descriptor.Title,
            ScanId = descriptor.ScanId,
            Format = descriptor.Format,
            GeneratedAtUtc = descriptor.GeneratedAtUtc,
            Payload = payload
        };

        await JsonSerializer.SerializeAsync(destination, envelope, SerializerOptions, cancellationToken)
                            .ConfigureAwait(false);
    }

    /// <summary>
    /// Root JSON envelope. Private — not part of the public API.
    /// </summary>
    private sealed record JsonReport(
        string Title,
        Guid ScanId,
        ReportFormat Format,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<JsonResultEntry> Results);

    /// <summary>
    /// Flat projection of an <see cref="IScanResult"/> for serialization.
    /// Private — not part of the public API.
    /// </summary>
    private sealed record JsonResultEntry(
        string ModuleName,
        ScanStatus Status,
        DateTimeOffset CompletedAtUtc,
        IReadOnlyList<ScanFinding> Findings,
        IReadOnlyList<string> Errors,
        IReadOnlyDictionary<string, string> Metadata);
}
