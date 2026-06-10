using System;
using System.CommandLine;
using System.Linq;
using System.Text;
using GhostTrace.CLI.Commands;
using GhostTrace.CLI.Runtime;
using GhostTrace.CLI.Tui;
using GhostTrace.Core.Localization;

// Resolve the UI language from the OS, with an optional `--lang xx` override.
Loc.InitializeFromOs();
var langArg = Array.IndexOf(args, "--lang");
if (langArg >= 0 && langArg + 1 < args.Length)
{
    Loc.SetLanguage(args[langArg + 1]);
    args = args.Where((_, i) => i != langArg && i != langArg + 1).ToArray();
}

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine(Loc.Current.RequiresWindows);
    return 1;
}

// .NET defaults the console output encoding to the legacy OEM code page
// (e.g. CP850/437), which lacks glyphs like → ✓ ✗ and renders them as '?'.
// Force UTF-8 so the TUI symbols and box drawing render correctly in conhost.
try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (System.IO.IOException)
{
    // Output redirected to a file/pipe — encoding can't be changed; ignore.
}

PrivilegeGuard.EnsureAdministrator();

if (args.Length == 0)
{
    await MainMenu.RunAsync();
    return 0;
}

var rootCommand = new RootCommand("GhostTrace Forensic Scanner")
{
    new ScanTestJsonCommand(),
    new ScanFsJsonCommand(),
};

rootCommand.Add(new ScanRegJsonCommand());
rootCommand.Add(new ScanEvtJsonCommand());
rootCommand.Add(new ScanPersistJsonCommand());
rootCommand.Add(new ScanTasksJsonCommand());
rootCommand.Add(new ScanTaskCacheJsonCommand());
rootCommand.Add(ScanTasksCorrelateJsonCommand.Create());
rootCommand.Add(new ScanCommand());

return await rootCommand.InvokeAsync(args);
