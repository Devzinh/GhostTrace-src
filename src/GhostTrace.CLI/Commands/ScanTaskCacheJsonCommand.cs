using System.CommandLine;
using System.Runtime.Versioning;
using GhostTrace.Modules.ScheduledTasks;
using GhostTrace.CLI.Runtime;

namespace GhostTrace.CLI.Commands;

/// <summary>
/// A CLI command to execute the TaskCacheScanModule and output a JSON report.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScanTaskCacheJsonCommand : Command
{
    public ScanTaskCacheJsonCommand()
        : base("scan-taskcache-json", "Runs a Windows Task Cache Registry scan and outputs a JSON report.")
    {
        var outputArgument = new Argument<FileInfo>(
            name: "outputPath",
            description: "The path to the output JSON file.");

        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, outputArgument);
    }

    private async Task ExecuteAsync(FileInfo outputInfo)
    {
        Console.WriteLine("[INFO] Starting Task Cache scan...");
        Console.WriteLine("[INFO] Targets:     HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Schedule\\TaskCache\\Tree");
        Console.WriteLine($"[INFO] Output File: {outputInfo.FullName}");

        await SingleModuleJsonRunner.RunAsync(
            new TaskCacheScanModule(), "Task Cache Scan Report", outputInfo);
    }
}
