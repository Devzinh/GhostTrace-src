using System.Collections.Generic;
using System.CommandLine;
using System.Runtime.Versioning;
using Microsoft.Win32;
using GhostTrace.Modules.Registry;
using GhostTrace.CLI.Runtime;

namespace GhostTrace.CLI.Commands;

/// <summary>
/// A CLI command to execute the RegistryScanModule and output a JSON report.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScanRegJsonCommand : Command
{
    public ScanRegJsonCommand()
        : base("scan-reg-json", "Runs a registry scan on a specific subkey and outputs a JSON report.")
    {
        var hiveArgument = new Argument<string>(
            name: "registryHive",
            description: "The base registry hive (e.g., CurrentUser, LocalMachine).");

        var subKeyArgument = new Argument<string>(
            name: "subKeyPath",
            description: "The subkey path to scan (e.g., Software\\Microsoft\\Windows\\CurrentVersion\\Run).");

        var outputArgument = new Argument<FileInfo>(
            name: "outputPath",
            description: "The path to the output JSON file.");

        AddArgument(hiveArgument);
        AddArgument(subKeyArgument);
        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, hiveArgument, subKeyArgument, outputArgument);
    }

    private async Task ExecuteAsync(string hiveName, string subKeyPath, FileInfo outputInfo)
    {
        if (!Enum.TryParse<RegistryHive>(hiveName, true, out _))
        {
            Console.WriteLine($"[ERROR] Invalid registry hive: '{hiveName}'. Use values like CurrentUser or LocalMachine.");
            return;
        }

        if (string.IsNullOrWhiteSpace(subKeyPath))
        {
            Console.WriteLine($"[ERROR] Subkey path cannot be empty.");
            return;
        }

        Console.WriteLine("[INFO] Starting registry scan...");
        Console.WriteLine($"[INFO] Hive:        {hiveName}");
        Console.WriteLine($"[INFO] SubKey:      {subKeyPath}");
        Console.WriteLine($"[INFO] Output File: {outputInfo.FullName}");

        await SingleModuleJsonRunner.RunAsync(
            new RegistryScanModule(), "Registry Scan Report", outputInfo,
            options: new Dictionary<string, string>
            {
                ["registryHive"] = hiveName,
                ["subKeyPath"] = subKeyPath
            });
    }
}
