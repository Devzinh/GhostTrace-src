using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using GhostTrace.CLI.Commands;
using GhostTrace.Core.Localization;
using Spectre.Console;

namespace GhostTrace.CLI.Tui;

[SupportedOSPlatform("windows")]
public static class MainMenu
{
    private enum MenuChoice
    {
        HuntTraces,
        About,
        Exit
    }

    private sealed record MenuItem(MenuChoice Choice, string Icon, string Title, string Description);

    private static MenuItem[] BuildItems()
    {
        var s = Loc.Current;
        return new[]
        {
            new MenuItem(MenuChoice.HuntTraces, "◎", s.MenuHuntTitle, s.MenuHuntDesc),
            new MenuItem(MenuChoice.About,      "ⓘ", s.MenuAboutTitle, s.MenuAboutDesc),
            new MenuItem(MenuChoice.Exit,       "⏻", s.MenuExitTitle, s.MenuExitDesc),
        };
    }

    public static async Task RunAsync()
    {
        while (true)
        {
            var s = Loc.Current;
            AnsiConsole.Clear();
            WelcomeScreen.Show();

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<MenuItem>()
                    .Title($"  [bold]{s.MenuMainTitle}[/]  [{WelcomeScreen.BrandDim}]· {s.MenuChooseAction}[/]")
                    .PageSize(10)
                    .HighlightStyle(Style.Parse($"black on {WelcomeScreen.BrandAccent}"))
                    .UseConverter(item =>
                        $"  [{WelcomeScreen.BrandAccent}]{item.Icon}[/]  " +
                        $"[bold]{item.Title,-28}[/] " +
                        $"[{WelcomeScreen.BrandDim}]{item.Description}[/]")
                    .AddChoices(BuildItems()));

            AnsiConsole.WriteLine();

            if (selected.Choice == MenuChoice.Exit)
            {
                Goodbye();
                return;
            }

            if (selected.Choice == MenuChoice.About)
            {
                AboutScreen.Show();
                continue;
            }

            await HuntTracesAsync();

            AnsiConsole.WriteLine();
            var another = AnsiConsole.Prompt(
                new TextPrompt<string>($"[bold]{s.PromptAnotherSearch}[/] [{WelcomeScreen.BrandDim}]({s.YesNoExitHint})[/]")
                    .AllowEmpty()
                    .DefaultValue(s.AffirmativeKey));

            if (!another.Equals(s.AffirmativeKey, StringComparison.OrdinalIgnoreCase))
            {
                Goodbye();
                return;
            }
        }
    }

    private static async Task HuntTracesAsync()
    {
        var s = Loc.Current;
        string term = string.Empty;
        while (string.IsNullOrWhiteSpace(term))
        {
            term = AnsiConsole.Ask<string>(
                $"[bold]{s.PromptSoftwareName}[/] [{WelcomeScreen.BrandDim}]({s.PromptSoftwareNameHint})[/]:");
            if (string.IsNullOrWhiteSpace(term))
                AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandAlert}]✗ {s.ErrorEmptyName}[/]");
        }

        string outputDir = PromptOutputDir();

        // The scan shows the traces, then offers interactive removal + an optional report.
        await ScanCommand.ExecuteScanAsync(
            filterTerm: term,
            outputDir: outputDir,
            quiet: false);
    }

    private static string PromptOutputDir()
    {
        var s = Loc.Current;
        // Default to a user-writable reports folder rather than the install directory
        // (the Start-Menu shortcut runs with CWD = Program Files\GhostTrace).
        string defaultDir = DefaultReportsDirectory();
        return AnsiConsole.Prompt(
            new TextPrompt<string>(
                $"[bold]{s.PromptOutputDir}[/] [{WelcomeScreen.BrandDim}]({s.EnterEquals} {Markup.Escape(defaultDir)})[/]")
                .AllowEmpty()
                .DefaultValue(defaultDir));
    }

    private static string DefaultReportsDirectory()
    {
        try
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(docs))
                return System.IO.Path.Combine(docs, "GhostTrace");
        }
        catch { /* fall through */ }
        return Environment.CurrentDirectory;
    }

    private static void Goodbye()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle(WelcomeScreen.BrandAccent));
        AnsiConsole.MarkupLine($"  [{WelcomeScreen.BrandMuted}]{Markup.Escape(Loc.Current.Goodbye)}[/]");
    }
}
