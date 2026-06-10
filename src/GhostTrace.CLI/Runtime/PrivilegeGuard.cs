using System;
using System.Runtime.Versioning;
using System.Security.Principal;
using GhostTrace.Core.Localization;
using Spectre.Console;

namespace GhostTrace.CLI.Runtime;

[SupportedOSPlatform("windows")]
internal static class PrivilegeGuard
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void EnsureAdministrator()
    {
        if (IsRunningAsAdministrator())
        {
            return;
        }

        var s = Loc.Current;
        GhostTrace.CLI.Tui.WelcomeScreen.Show();
        AnsiConsole.MarkupLine($"  [red][[✗]][/] {Markup.Escape(s.PrivInsufficient)}");
        AnsiConsole.MarkupLine($"      {Markup.Escape(s.PrivRequiresAdmin)}");
        AnsiConsole.MarkupLine($"      {Markup.Escape(s.PrivRightClick)}");
        AnsiConsole.MarkupLine($"      {Markup.Escape(s.PrivRunAsAdmin)}");
        AnsiConsole.MarkupLine($"  {Markup.Escape(s.PrivPressKey)}");

        if (!Console.IsInputRedirected)
        {
            Console.ReadKey(intercept: true);
        }
        Environment.Exit(3);
    }
}
