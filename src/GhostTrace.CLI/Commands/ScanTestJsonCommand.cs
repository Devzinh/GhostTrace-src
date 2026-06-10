using System.CommandLine;
using GhostTrace.Modules;
using GhostTrace.CLI.Runtime;

namespace GhostTrace.CLI.Commands;

/// <summary>
/// A minimal command to test the end-to-end flow of the scan pipeline and JSON reporting.
/// </summary>
public sealed class ScanTestJsonCommand : Command
{
    public ScanTestJsonCommand()
        : base("scan-test-json", "Runs a test scan pipeline and outputs a JSON report.")
    {
        var outputArgument = new Argument<FileInfo>(
            name: "outputPath",
            description: "The path to the output JSON file.");

        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, outputArgument);
    }

    private async Task ExecuteAsync(FileInfo outputInfo)
    {
        Console.WriteLine("[INFO] Starting test scan pipeline...");
        Console.WriteLine($"[INFO] Output will be saved to: {outputInfo.FullName}");

        await SingleModuleJsonRunner.RunAsync(
            new EchoScanModule(), "CLI Test Scan", outputInfo);
    }
}
