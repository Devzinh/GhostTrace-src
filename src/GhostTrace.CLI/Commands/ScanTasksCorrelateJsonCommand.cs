using System;
using System.CommandLine;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using GhostTrace.Analysis;
using GhostTrace.CLI.Runtime;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.CLI.Commands;

[SupportedOSPlatform("windows")]
public static class ScanTasksCorrelateJsonCommand
{
    public static Command Create()
    {
        var command = new Command("scan-tasks-correlate-json", "Performs an advanced correlation of COM and TaskCache Registry to detect Ghost Tasks (T1053.005).");

        var outputOption = new Option<FileInfo>(
            name: "--output",
            description: "The path to the output JSON file."
        ) { IsRequired = true };
        
        command.AddOption(outputOption);

        command.SetHandler(async (FileInfo outputInfo) =>
        {
            Console.WriteLine("[INFO] Starting Scheduled Tasks correlation...");
            Console.WriteLine("[INFO] Coverage: COM API + TaskCache Registry");

            // Empty context wrappers - analysis module handles everything independently
            var comContext = new CliScanContext("ScheduledTasksCorrelation-COM", Guid.NewGuid(), DateTimeOffset.UtcNow);
            var regContext = new CliScanContext("ScheduledTasksCorrelation-Registry", Guid.NewGuid(), DateTimeOffset.UtcNow);

            var orchestrator = new ScheduledTasksCorrelationOrchestrator();

            var result = await orchestrator.RunCorrelationAsync(comContext, regContext, cancellationToken: CancellationToken.None);

            var descriptor = new ReportDescriptor(
                Title: "GhostTrace Scheduled Tasks Correlation Report",
                ScanId: Guid.NewGuid(),
                Format: ReportFormat.Json,
                GeneratedAtUtc: DateTimeOffset.UtcNow
            );

            Console.WriteLine($"[INFO] Correlation complete. Generating report at '{outputInfo.FullName}'...");
            await JsonReportHelper.TryWritePayloadAsync(outputInfo, descriptor, result);

        }, outputOption);

        return command;
    }
}
