using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.CLI.Runtime;
using GhostTrace.CLI.Tui;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Localization;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using GhostTrace.Core.Reports;
using GhostTrace.Modules.Activity;
using GhostTrace.Modules.EventLogs;
using GhostTrace.Modules.Execution;
using GhostTrace.Modules.Filesystem;
using GhostTrace.Modules.Persistence;
using GhostTrace.Modules.Prefetch;
using GhostTrace.Modules.Registry;
using GhostTrace.Modules.ScheduledTasks;
using GhostTrace.Modules.Shimcache;
using GhostTrace.Modules.SystemArtifacts;
using GhostTrace.Modules.WmiPersistence;
using Spectre.Console;

namespace GhostTrace.CLI.Commands;

[SupportedOSPlatform("windows")]
public class ScanCommand : Command
{


    public ScanCommand() : base("scan", "Hunt every forensic trace of a piece of software across the whole system.")
    {
        var nameOption = new Option<string?>(
            aliases: new[] { "--name", "--filter" },
            () => null,
            "Name of the software whose traces to hunt (e.g. 'nvidia'). Omit for a full triage.");

        var outputOption = new Option<string>(
            "--output",
            () => Environment.CurrentDirectory,
            "Directory where the optional .txt report and the cleanup log are written.");

        var quietOption = new Option<bool>(
            "--quiet",
            "Suppresses the interactive UI and writes a .txt report (for scripting).");

        AddOption(nameOption);
        AddOption(outputOption);
        AddOption(quietOption);

        this.SetHandler(
            (name, outputDir, quiet) => ExecuteScanAsync(name, outputDir, quiet),
            nameOption, outputOption, quietOption);
    }

    /// <summary>Product version read from the entry assembly (single source of truth).</summary>
    private static string AppVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>
    /// Modules that require a per-run target (a path / key / log name) and therefore are
    /// not part of the standardised, all-techniques trace hunt. They remain reachable
    /// through their dedicated CLI commands.
    /// </summary>
    private static readonly HashSet<string> ModulesRequiringTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "FilesystemScanModule",
        "RegistryScanModule",
        "EventLogScanModule"
    };

    /// <summary>
    /// Runs the standardised forensic trace hunt across every self-sufficient module.
    /// In interactive mode it offers an opt-in, confirmed in-app removal of the leftovers
    /// and an optional .txt report; in quiet mode it just writes the .txt report.
    /// </summary>
    /// <param name="filterTerm">Software name to hunt; when null, all findings are reported.</param>
    /// <param name="outputDir">Where the optional .txt report and cleanup log are written.</param>
    /// <param name="quiet">Suppress the interactive UI (scripting mode).</param>
    public static async Task<int> ExecuteScanAsync(string? filterTerm, string outputDir, bool quiet)
    {
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            // Cooperative cancellation — never kill the process mid-scan and leave
            // a half-written report behind.
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        var ct = cts.Token;

        try
        {
            IScanModule[] allModules =
            [
                // Per-target (excluded from the standardised hunt)
                new FilesystemScanModule(),
                new RegistryScanModule(),
                new EventLogScanModule(),

                // Persistence
                new PersistenceScanModule(),
                new ServicesScanModule(),
                new AsepScanModule(),
                new ScheduledTasksScanModule(),
                new TaskCacheScanModule(),
                new WmiPersistenceScanModule(),

                // Execution evidence
                new ShimcacheScanModule(),
                new PrefetchScanModule(),
                new BamScanModule(),
                new UserAssistScanModule(),
                new MuiCacheScanModule(),

                // User activity & system artifacts
                new PowerShellHistoryScanModule(),
                new RdpConnectionScanModule(),
                new RecentDocsScanModule(),
                new UsbDeviceScanModule(),
                new NetworkArtifactsScanModule(),

                // Installed software & on-disk leftovers (find_trash core)
                new UninstallEntriesScanModule(),
                new StartupApprovedScanModule(),
                new FileSystemTraceScanModule()
            ];

            // Standardised: always every self-sufficient technique. No manual selection.
            IScanModule[] modules = allModules.Where(m => !ModulesRequiringTargets.Contains(m.Name)).ToArray();

            var startedAt = DateTimeOffset.UtcNow;
            var machineName = Environment.MachineName;
            var osVersion = Environment.OSVersion.VersionString;

            string sanitized = filterTerm is null ? "" : "-" + Regex.Replace(filterTerm.ToLowerInvariant(), "[^a-z0-9]", "");
            string stamp = startedAt.ToLocalTime().ToString("yyyyMMdd-HHmmss");
            string txtFilePath = Path.Combine(outputDir, $"ghosttrace-report{sanitized}-{stamp}.txt");
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] " + string.Format(Loc.Current.OutputDirErrorFmt, outputDir, ex.Message));
                return 2;
            }

        if (!quiet)
        {
            var s = Loc.Current;
            string targetValue = filterTerm ?? s.FullTriage;
            var panel = new Panel($@"  {s.LblHost,-7}: {machineName}
  {s.LblOs,-7}: {osVersion}
  {s.LblStart,-7}: {startedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zz}
  {s.LblTarget,-7}: {targetValue}
  {s.LblOutput,-7}: {outputDir}")
            {
                Header = new PanelHeader($"  {string.Format(s.PanelHeaderHunter, AppVersion)}  "),
                Border = BoxBorder.Double
            };
            GhostTrace.CLI.Tui.WelcomeScreen.Show();
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        var moduleResults = new List<ModuleScanResult>();
        int totalFindings = 0;
        int totalMatches = 0;
        int totalErrors = 0;
        ScanStatus worstStatus = ScanStatus.Success;

        var context = new HuntScanContext(startedAt, filterTerm);

        if (quiet)
        {
            foreach (var module in modules)
            {
                var sw = Stopwatch.StartNew();
                var result = await RunModuleSafelyAsync(module, context, ct);
                sw.Stop();

                var modResult = ProcessModuleResult(module.Name, result, sw.Elapsed, filterTerm);
                moduleResults.Add(modResult);

                totalFindings += modResult.TotalFindings;
                totalMatches += modResult.TotalMatches;
                totalErrors += modResult.Errors.Count;
            }
        }
        else
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .AutoRefresh(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    foreach (var module in modules)
                    {
                        var task = ctx.AddTask($"[yellow]{module.Name.PadRight(25)}[/]", maxValue: 1);

                        var sw = Stopwatch.StartNew();
                        var result = await RunModuleSafelyAsync(module, context, ct);
                        sw.Stop();

                        task.Increment(1);
                        task.StopTask();

                        var modResult = ProcessModuleResult(module.Name, result, sw.Elapsed, filterTerm);
                        moduleResults.Add(modResult);

                        totalFindings += modResult.TotalFindings;
                        totalMatches += modResult.TotalMatches;
                        totalErrors += modResult.Errors.Count;

                        string icon = result.Status switch
                        {
                            ScanStatus.Success => "[green][[\u2713]][/]",
                            ScanStatus.PartialSuccess => "[yellow][[\u26A0]][/]",
                            ScanStatus.Failure => "[red][[\u2717]][/]",
                            ScanStatus.Skipped => "[grey][[\u2013]][/]",
                            _ => "[grey][[?]][/]"
                        };

                        string matchCol = filterTerm != null ? $"{modResult.TotalMatches} {Loc.Current.LblTraces.ToLowerInvariant()}".PadRight(15) : "";
                        string collectCol = $"{modResult.TotalFindings} {Loc.Current.RptFindings.ToLowerInvariant()}".PadRight(18);
                        
                        AnsiConsole.MarkupLine($"  {icon}  {module.Name.PadRight(25)} {collectCol} {matchCol} {sw.Elapsed.TotalSeconds:F1}s");
                    }
                });
        }

        var finishedAt = DateTimeOffset.UtcNow;
        var duration = finishedAt - startedAt;

        var fullReport = new FullScanReport
        {
            ScanStartedAt = startedAt,
            ScanFinishedAt = finishedAt,
            MachineName = machineName,
            OsVersion = osVersion,
            FilterTerm = filterTerm,
            TotalFindings = totalFindings,
            TotalMatches = totalMatches,
            ModuleResults = moduleResults.AsReadOnly()
        };

        // Canonical worst-status (Skipped never worsens) lives on the report.
        worstStatus = fullReport.WorstStatus;

        if (quiet)
        {
            // Non-interactive (scripting): always write the .txt record, no cleanup.
            if (!TryWriteReport(fullReport, txtFilePath, quiet: true))
            {
                return 2;
            }
        }
        else
        {
            if (filterTerm != null)
            {
                RenderMatchesTable(moduleResults, filterTerm, totalMatches);
            }

            RenderSummary(modules.Length, duration, filterTerm, totalMatches, worstStatus);

            // Interactive, opt-in, confirmed & logged removal — the find_trash payoff.
            if (filterTerm != null)
            {
                RunInteractiveCleanup(moduleResults, outputDir, stamp, filterTerm);
            }

            // Report export is optional now (the table + cleanup are the main experience).
            bool exportReport = AnsiConsole.Prompt(
                new ConfirmationPrompt($"[bold]{Loc.Current.PromptExportReport}[/]") { DefaultValue = false });
            if (exportReport && TryWriteReport(fullReport, txtFilePath, quiet: false))
            {
                AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandDim}]{Loc.Current.LblReport}:[/] {txtFilePath}");
            }
        }

            return worstStatus switch
            {
                ScanStatus.Success => 0,
                ScanStatus.Skipped => 0,
                ScanStatus.PartialSuccess => 1,
                _ => 2
            };
        }
        catch (OperationCanceledException)
        {
            if (!quiet) AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(Loc.Current.ScanCancelled)}[/]");
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    /// <summary>
    /// Executes a module, converting any unhandled exception into a Failure result so a
    /// single faulty module can never abort the whole hunt. Cancellation still propagates.
    /// </summary>
    private static async Task<IScanResult> RunModuleSafelyAsync(IScanModule module, IScanContext context, CancellationToken ct)
    {
        try
        {
            return await module.RunAsync(context, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new FaultedScanResult(module.Name, $"Module crashed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Minimal Failure result used when a module throws unexpectedly.</summary>
    private sealed class FaultedScanResult : IScanResult
    {
        public FaultedScanResult(string moduleName, string error)
        {
            ModuleName = moduleName;
            Errors = new[] { error };
        }

        public string ModuleName { get; }
        public ScanStatus Status => ScanStatus.Failure;
        public DateTimeOffset CompletedAtUtc { get; } = DateTimeOffset.UtcNow;
        public IReadOnlyList<ScanFinding> Findings { get; } = Array.Empty<ScanFinding>();
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; } =
            new Dictionary<string, string>();
    }

    /// <summary>
    /// Renders the matched traces as a compact Spectre table — the find_trash payoff.
    /// </summary>
    private static void RenderMatchesTable(IReadOnlyList<ModuleScanResult> moduleResults, string filterTerm, int totalMatches)
    {
        var s = Loc.Current;
        if (totalMatches == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(string.Format(s.NoTracesFoundFmt, filterTerm))}[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey39)
            .Title($"[bold {WelcomeScreen.BrandAccent}]{Markup.Escape(string.Format(s.MatchesTitleFmt, filterTerm))}[/]")
            .AddColumn(new TableColumn($"[{WelcomeScreen.BrandAccent}]{s.ColTechnique}[/]"))
            .AddColumn(new TableColumn($"[{WelcomeScreen.BrandAccent}]{s.ColCategory}[/]"))
            .AddColumn(new TableColumn($"[{WelcomeScreen.BrandAccent}]{s.ColArtifact}[/]"))
            .AddColumn(new TableColumn($"[{WelcomeScreen.BrandAccent}]{s.ColWhen}[/]"));

        const int maxRows = 60; // keep the console readable; the .txt has the full set
        int shown = 0;

        foreach (var m in moduleResults)
        {
            foreach (var f in m.MatchedFindings)
            {
                if (shown >= maxRows) break;
                string module = m.ModuleName.Replace("ScanModule", "");
                string when = f.TimestampUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
                string artifact = f.Description.Length > 70 ? f.Description[..69] + "…" : f.Description;

                table.AddRow(
                    Markup.Escape(module),
                    Markup.Escape(f.Category),
                    Markup.Escape(artifact),
                    $"[grey]{when}[/]");
                shown++;
            }
            if (shown >= maxRows) break;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        if (totalMatches > shown)
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(string.Format(s.MoreTracesFmt, totalMatches - shown))}[/]");
        }
    }

    private static void RenderSummary(int techniques, TimeSpan duration, string? filterTerm, int totalMatches, ScanStatus worstStatus)
    {
        string color = worstStatus switch
        {
            ScanStatus.Success => "green",
            ScanStatus.PartialSuccess => "yellow",
            ScanStatus.Failure => "red",
            _ => "grey"
        };

        var s = Loc.Current;
        var summary = new Grid().AddColumn(new GridColumn().PadRight(2)).AddColumn();
        summary.AddRow($"[grey]{s.LblTechniques}[/]", $"{techniques}  ·  {duration.TotalSeconds:F1}s");
        if (filterTerm != null)
            summary.AddRow($"[grey]{s.LblTraces}[/]", totalMatches > 0
                ? string.Format(s.TracesOfFmt, $"[bold {WelcomeScreen.BrandAccent}]{totalMatches}[/]", $"[white]{Markup.Escape(filterTerm)}[/]")
                : $"[grey]{Markup.Escape(string.Format(s.NoTracesOfFmt, filterTerm))}[/]");
        summary.AddRow($"[grey]{s.LblStatus}[/]", $"[{color}]{worstStatus.ToString().ToUpper()}[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(summary)
        {
            Header = new PanelHeader($" [bold {WelcomeScreen.BrandAccent}]{s.SummaryHeader}[/] "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(WelcomeScreen.BrandAccent),
            Padding = new Padding(2, 1, 2, 1),
        });
    }

    private static bool TryWriteReport(FullScanReport report, string txtFilePath, bool quiet)
    {
        try
        {
            GhostTrace.Core.Reporting.TxtReportWriter.Write(report, txtFilePath);
            return true;
        }
        catch (Exception ex)
        {
            if (!quiet) AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(string.Format(Loc.Current.ReportWriteWarnFmt, ex.Message))}[/]");
            return false;
        }
    }

    /// <summary>
    /// Interactive, opt-in, confirmed in-app removal of the leftover traces, with a
    /// written action log. Replaces the old generated cleanup .ps1.
    /// </summary>
    private static void RunInteractiveCleanup(IReadOnlyList<ModuleScanResult> moduleResults, string outputDir, string stamp, string filterTerm)
    {
        var s = Loc.Current;
        var removable = TraceCleaner.Collect(moduleResults, filterTerm);

        AnsiConsole.WriteLine();
        if (removable.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandDim}]{Markup.Escape(s.NoRemovable)}[/]");
            return;
        }

        bool wantRemove = AnsiConsole.Prompt(
            new ConfirmationPrompt($"[bold]{Markup.Escape(string.Format(s.PromptRemoveFmt, filterTerm))}[/] [{WelcomeScreen.BrandDim}]{Markup.Escape(string.Format(s.RemovableCountFmt, removable.Count))}[/]")
            { DefaultValue = false });
        if (!wantRemove) return;

        var prompt = new MultiSelectionPrompt<TraceCleaner.RemovableTrace>()
            .Title($"  [bold]{s.SelectToRemove}[/] [{WelcomeScreen.BrandDim}]({s.SelectHint})[/]")
            .NotRequired()
            .PageSize(20)
            .HighlightStyle(Style.Parse($"black on {WelcomeScreen.BrandAccent}"))
            .MoreChoicesText($"  [{WelcomeScreen.BrandDim}]{s.MoreChoices}[/]")
            .InstructionsText($"  [{WelcomeScreen.BrandDim}]{s.MultiSelectInstr}[/]")
            .UseConverter(t =>
            {
                string kindColor = t.IsRegistry ? WelcomeScreen.BrandWarn : WelcomeScreen.BrandAccent;
                string kind = LocalizedKind(t, s);
                return $"[{kindColor}]{kind,-8}[/] {Markup.Escape(t.Display)}";
            });

        foreach (var t in removable) prompt.AddChoice(t); // nothing pre-selected (destructive)

        var selected = AnsiConsole.Prompt(prompt);
        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandDim}]{Markup.Escape(s.NothingSelected)}[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandAlert}]{Markup.Escape(s.Warning)}[/] {Markup.Escape(string.Format(s.WarnDeleteFmt, selected.Count))}");
        string confirm = AnsiConsole.Prompt(
            new TextPrompt<string>($"  {Markup.Escape(string.Format(s.TypeToConfirmFmt, s.ConfirmWord))}")
                .AllowEmpty().DefaultValue(string.Empty));
        if (!confirm.Trim().Equals(s.ConfirmWord, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandDim}]{Markup.Escape(s.Cancelled)}[/]");
            return;
        }

        int removed = 0, skipped = 0, errored = 0;
        var log = new List<string>();
        AnsiConsole.WriteLine();
        foreach (var t in selected)
        {
            var outcome = TraceCleaner.Remove(t);
            (string tag, string color) = outcome.Status switch
            {
                TraceCleaner.CleanupStatus.Removed => (s.TagRemoved, "green"),
                TraceCleaner.CleanupStatus.Skipped => (s.TagSkipped, "grey"),
                _ => (s.TagError, "red")
            };
            if (outcome.Status == TraceCleaner.CleanupStatus.Removed) removed++;
            else if (outcome.Status == TraceCleaner.CleanupStatus.Skipped) skipped++;
            else errored++;

            AnsiConsole.MarkupLine($"  [{color}][[{Markup.Escape(tag)}]][/] {Markup.Escape(outcome.Message)}");
            log.Add($"[{tag}] {outcome.Message}");
        }

        // Always record what was actually done — accountability for a destructive action.
        string logPath = Path.Combine(outputDir, $"ghosttrace-cleanup-log-{stamp}.txt");
        try
        {
            Directory.CreateDirectory(outputDir);
            var header = new[]
            {
                s.LogTitle,
                $"{s.LogGenerated} : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zz}",
                $"{s.LogTarget} : {filterTerm}",
                string.Format(s.LogCounts, removed, skipped, errored),
                new string('=', 60),
            };
            File.WriteAllLines(logPath, header.Concat(log));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(string.Format(s.ReportWriteWarnFmt, ex.Message))}[/]");
            logPath = string.Empty;
        }

        AnsiConsole.MarkupLine($"  [white]{Markup.Escape(string.Format(s.CleanupSummaryFmt, removed, skipped, errored))}[/]");
        if (!string.IsNullOrEmpty(logPath))
            AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandDim}]{s.LblLog}:[/] {logPath}");
    }

    private static string LocalizedKind(TraceCleaner.RemovableTrace t, LocaleStrings s)
    {
        if (t.IsRegistry) return s.KindRegistry;
        return t.IsDirectory ? s.KindFolder : s.KindFile;
    }

    private static ModuleScanResult ProcessModuleResult(string moduleName, IScanResult result, TimeSpan duration, string? filterTerm)
    {
        var unmatched = new List<ScanFinding>();
        var matched = new List<ScanFinding>();

        foreach (var f in result.Findings)
        {
            if (filterTerm != null && IsMatch(f, filterTerm))
            {
                matched.Add(f);
            }
            else
            {
                unmatched.Add(f);
            }
        }

        return new ModuleScanResult
        {
            ModuleName = moduleName,
            Status = result.Status,
            TotalFindings = result.Findings.Count,
            TotalMatches = matched.Count,
            ModuleDuration = duration,
            Findings = unmatched.AsReadOnly(),
            MatchedFindings = matched.AsReadOnly(),
            Errors = result.Errors
        };
    }

    private static bool IsMatch(ScanFinding f, string filter)
    {
        return (!string.IsNullOrEmpty(f.Description) && f.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(f.Source) && f.Source.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(f.RawValue) && f.RawValue.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Context for the standardised hunt. Exposes the hunted software name via
    /// <c>GetOption("targetName")</c> so name-aware modules (e.g. the filesystem trace
    /// search) can target their work instead of enumerating the entire disk.
    /// </summary>
    private sealed class HuntScanContext : IScanContext
    {
        private readonly string? _targetName;

        public HuntScanContext(DateTimeOffset startedAtUtc, string? targetName)
        {
            StartedAtUtc = startedAtUtc;
            _targetName = targetName;
        }

        public string ModuleName => "ScanCommand";
        public Guid ScanId { get; } = Guid.NewGuid();
        public DateTimeOffset StartedAtUtc { get; }

        public string? GetOption(string key) =>
            string.Equals(key, "targetName", StringComparison.OrdinalIgnoreCase) ? _targetName : null;
    }
}
