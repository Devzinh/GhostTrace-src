using System;
using System.Reflection;
using GhostTrace.Core.Localization;
using Spectre.Console;

namespace GhostTrace.CLI.Tui;

public static class AboutScreen
{
    public static void Show()
    {
        var s = Loc.Current;
        AnsiConsole.Clear();
        WelcomeScreen.Show();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // Identity card
        var meta = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(2).Width(18))
            .AddColumn();

        meta.AddRow($"[{WelcomeScreen.BrandMuted}]{s.LblVersion}[/]",   $"[bold white]{version}[/]");
        meta.AddRow($"[{WelcomeScreen.BrandMuted}]{s.LblPlatform}[/]",  "[white]Windows · .NET 10[/]");
        meta.AddRow($"[{WelcomeScreen.BrandMuted}]{s.LblModules}[/]",   $"[white]{s.ModulesValue}[/]");
        meta.AddRow($"[{WelcomeScreen.BrandMuted}]{s.LblMode}[/]",      $"[white]{s.ModeValue}[/]");

        AnsiConsole.Write(new Panel(meta)
        {
            Header = new PanelHeader($" [bold {WelcomeScreen.BrandAccent}]{s.AboutHeader}[/] "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(WelcomeScreen.BrandAccent),
            Padding = new Padding(2, 1, 2, 1),
        });
        AnsiConsole.WriteLine();

        // Module catalog. Descriptions are concise technical references (artifact names),
        // kept in English so they read consistently regardless of the UI language.
        var modulesTable = new Table()
            .Border(TableBorder.Minimal)
            .BorderColor(Color.Grey39)
            .AddColumn(new TableColumn($"[{WelcomeScreen.BrandAccent}]{s.ColModule}[/]"))
            .AddColumn(new TableColumn($"[{WelcomeScreen.BrandAccent}]{s.ColCoverage}[/]"));

        void Row(string module, string coverage) =>
            modulesTable.AddRow(module, $"[{WelcomeScreen.BrandMuted}]{coverage}[/]");

        Row("FilesystemScanModule",        "File metadata in a directory (per-target)");
        Row("RegistryScanModule",          "Values under a registry key (per-target)");
        Row("EventLogScanModule",          "Recent Application/System events (per-target)");
        Row("PersistenceScanModule",       "Run keys + Startup folders");
        Row("ServicesScanModule",          "Services/drivers with suspicious ImagePath");
        Row("AsepScanModule",              "Winlogon/IFEO/AppInit/LSA/Active Setup");
        Row("ScheduledTasksScanModule",    "Scheduled Tasks via COM API");
        Row("TaskCacheScanModule",         "HKLM TaskCache\\Tree (ghost tasks)");
        Row("WmiPersistenceScanModule",    "Filter/Consumer/Binding in root\\subscription");
        Row("ShimcacheScanModule",         "AppCompatCache (execution)");
        Row("PrefetchScanModule",          ".pf files (XPRESS-HUFF, v26/30/31)");
        Row("BamScanModule",               "BAM/DAM — last execution per SID");
        Row("UserAssistScanModule",        "UserAssist (GUI execution, ROT13)");
        Row("MuiCacheScanModule",          "MUICache (execution + friendly names)");
        Row("PowerShellHistoryScanModule", "PSReadLine history (suspicious commands)");
        Row("RdpConnectionScanModule",     "Outbound RDP connection history");
        Row("RecentDocsScanModule",        "Recently opened documents");
        Row("UsbDeviceScanModule",         "USB device history (USBSTOR)");
        Row("NetworkArtifactsScanModule",  "Hosts file + joined networks (dates)");
        Row("UninstallEntriesScanModule",  "Installed programs (version/location/uninstall)");
        Row("StartupApprovedScanModule",   "Startup items enabled/disabled");
        Row("FileSystemTraceScanModule",   "On-disk file/folder search for the target");

        AnsiConsole.Write(modulesTable);
        AnsiConsole.WriteLine();

        // Guarantees panel
        AnsiConsole.Write(new Panel(
            $"[bold {WelcomeScreen.BrandOk}]{s.GuaranteesTitle}[/]\n" +
            $"[{WelcomeScreen.BrandMuted}]" +
            $"  · {s.Guarantee1}\n" +
            $"  · {s.Guarantee2}\n" +
            $"  · {s.Guarantee3}\n" +
            $"  · {s.Guarantee4}[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey39"),
            Padding = new Padding(2, 1, 2, 1),
        });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandDim}]{s.PressAnyKey}[/]");

        try
        {
            if (!Console.IsInputRedirected)
            {
                Console.ReadKey(intercept: true);
            }
        }
        catch (InvalidOperationException)
        {
            // No console attached (test harness, redirected I/O) — return silently.
        }
    }
}
