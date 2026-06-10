using System.Reflection;
using Spectre.Console;

namespace GhostTrace.CLI.Tui;

public static class WelcomeScreen
{
    // Brand palette — kept consistent with HtmlReportWriter so CLI and HTML
    // reports look like they belong to the same product.
    internal const string BrandAccent = "#4f98a3";   // aqua / teal
    internal const string BrandMuted  = "#94a3b8";   // slate-400
    internal const string BrandDim    = "grey50";
    internal const string BrandAlert  = "#fc8181";
    internal const string BrandOk     = "#68d391";
    internal const string BrandWarn   = "#f6ad55";

    private static readonly Color AccentColor = new(0x4f, 0x98, 0xa3);

    public static void Show()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new FigletText("GhostTrace")
            .LeftJustified()
            .Color(AccentColor));

        var s = GhostTrace.Core.Localization.Loc.Current;

        // Tagline row
        AnsiConsole.MarkupLine(
            $"  [bold {BrandAccent}]{s.Tagline}[/]  " +
            $"[{BrandDim}]·[/]  [white]v{version}[/]  " +
            $"[{BrandDim}]·[/]  [{BrandMuted}].NET 10[/]  " +
            $"[{BrandDim}]·[/]  [{BrandMuted}]Windows x64[/]");

        // Trust badges — inverted so they read as labels
        AnsiConsole.MarkupLine(
            $"  [black on {BrandAccent}] {s.BadgeReadOnly} [/]  " +
            $"[black on {BrandAccent}] {s.BadgeOffline} [/]  " +
            $"[black on {BrandAccent}] {s.BadgeNoMutations} [/]");

        AnsiConsole.Write(new Rule().RuleStyle(BrandAccent));
        AnsiConsole.WriteLine();
    }
}
