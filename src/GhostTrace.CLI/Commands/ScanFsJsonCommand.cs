using System.Collections.Generic;
using System.CommandLine;
using System.Runtime.Versioning;
using GhostTrace.Modules.Filesystem;
using GhostTrace.CLI.Runtime;

namespace GhostTrace.CLI.Commands;

/// <summary>
/// A CLI command to execute the FilesystemScanModule and output a JSON report.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScanFsJsonCommand : Command
{
    public ScanFsJsonCommand()
        : base("scan-fs-json", "Runs a filesystem scan and outputs a JSON report.")
    {
        var targetArgument = new Argument<DirectoryInfo>(
            name: "targetPath",
            description: "The target directory to scan.");

        var outputArgument = new Argument<FileInfo>(
            name: "outputPath",
            description: "The path to the output JSON file.");

        AddArgument(targetArgument);
        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, targetArgument, outputArgument);
    }

    private async Task ExecuteAsync(DirectoryInfo targetInfo, FileInfo outputInfo)
    {
        if (!targetInfo.Exists)
        {
            Console.WriteLine($"[ERROR] Target directory does not exist: '{targetInfo.FullName}'.");
            return;
        }

        Console.WriteLine("[INFO] Starting filesystem scan...");
        Console.WriteLine($"[INFO] Target Directory: {targetInfo.FullName}");
        Console.WriteLine($"[INFO] Output File:      {outputInfo.FullName}");

        await SingleModuleJsonRunner.RunAsync(
            new FilesystemScanModule(), "Filesystem Scan Report", outputInfo,
            options: new Dictionary<string, string>
            {
                ["targetPath"] = targetInfo.FullName
            });
    }
}
