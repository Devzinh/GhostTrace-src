using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using GhostTrace.Core.Pipeline;

namespace GhostTrace.CLI.Runtime;

/// <summary>
/// Centralises the boilerplate shared by every <c>scan-*-json</c> command: build a
/// single-module context, run it through the pipeline, and write the JSON report.
/// </summary>
internal static class SingleModuleJsonRunner
{
    public static async Task RunAsync(
        IScanModule module,
        string reportTitle,
        FileInfo outputInfo,
        IReadOnlyDictionary<string, string>? options = null,
        CancellationToken cancellationToken = default)
    {
        var scanId = Guid.NewGuid();

        var context = new CliScanContext(
            ModuleName: module.Name,
            ScanId: scanId,
            StartedAtUtc: DateTimeOffset.UtcNow,
            Options: options is null
                ? null
                : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(options)));

        var pipeline = new ScanPipeline(new[] { module });
        var results = await pipeline.ExecuteAsync(context, cancellationToken);

        Console.WriteLine("[INFO] Scan completed. Generating JSON report...");

        var descriptor = new ReportDescriptor(
            Format: ReportFormat.Json,
            Title: reportTitle,
            ScanId: scanId,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

        await JsonReportHelper.TryWriteReportAsync(outputInfo, descriptor, results);
    }
}
