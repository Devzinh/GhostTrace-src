using System.Collections.Generic;
using System.CommandLine;
using System.Runtime.Versioning;
using GhostTrace.Modules.EventLogs;
using GhostTrace.CLI.Runtime;

namespace GhostTrace.CLI.Commands;

/// <summary>
/// A CLI command to execute the EventLogScanModule and output a JSON report.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScanEvtJsonCommand : Command
{
    public ScanEvtJsonCommand()
        : base("scan-evt-json", "Runs a Windows Event Log scan and outputs a JSON report.")
    {
        var logArgument = new Argument<string>(
            name: "logName",
            description: "The name of the event log to scan (e.g., Application, System).");

        var maxEntriesArgument = new Argument<int>(
            name: "maxEntries",
            description: "The maximum number of recent entries to collect.");

        var outputArgument = new Argument<FileInfo>(
            name: "outputPath",
            description: "The path to the output JSON file.");

        AddArgument(logArgument);
        AddArgument(maxEntriesArgument);
        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, logArgument, maxEntriesArgument, outputArgument);
    }

    private async Task ExecuteAsync(string logName, int maxEntries, FileInfo outputInfo)
    {
        if (!string.Equals(logName, "Application", StringComparison.OrdinalIgnoreCase) && 
            !string.Equals(logName, "System", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ERROR] Invalid log name: '{logName}'. Only 'Application' or 'System' are supported in v1.");
            return;
        }

        if (maxEntries <= 0)
        {
            Console.WriteLine($"[ERROR] Max entries must be a positive integer.");
            return;
        }

        Console.WriteLine("[INFO] Starting Event Log scan...");
        Console.WriteLine($"[INFO] Log:         {logName}");
        Console.WriteLine($"[INFO] Max Entries: {maxEntries}");
        Console.WriteLine($"[INFO] Output File: {outputInfo.FullName}");

        await SingleModuleJsonRunner.RunAsync(
            new EventLogScanModule(), "Event Log Scan Report", outputInfo,
            options: new Dictionary<string, string>
            {
                ["logName"] = logName,
                ["maxEntries"] = maxEntries.ToString()
            });
    }
}
