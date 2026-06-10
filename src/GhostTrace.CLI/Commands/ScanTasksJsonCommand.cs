using System.CommandLine;
using System.Runtime.Versioning;
using GhostTrace.Modules.ScheduledTasks;
using GhostTrace.CLI.Runtime;

namespace GhostTrace.CLI.Commands;

/// <summary>
/// A CLI command to execute the ScheduledTasksScanModule and output a JSON report.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScanTasksJsonCommand : Command
{
    public ScanTasksJsonCommand()
        : base("scan-tasks-json", "Runs a Windows Scheduled Tasks scan and outputs a JSON report.")
    {
        var outputArgument = new Argument<FileInfo>(
            name: "outputPath",
            description: "The path to the output JSON file.");

        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, outputArgument);
    }

    private async Task ExecuteAsync(FileInfo outputInfo)
    {
        Console.WriteLine("[INFO] Starting Scheduled Tasks scan...");
        Console.WriteLine("[INFO] Targets:     Local Machine Scheduled Tasks (Visible and Hidden)");
        Console.WriteLine($"[INFO] Output File: {outputInfo.FullName}");

        await SingleModuleJsonRunner.RunAsync(
            new ScheduledTasksScanModule(), "Scheduled Tasks Scan Report", outputInfo);
    }
}
