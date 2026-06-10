namespace GhostTrace.Core.Localization;

/// <summary>
/// All user-facing strings for one language. English values are the defaults; the
/// pt-BR / es instances override them. Any property a translation leaves unset falls
/// back to its English default automatically.
/// </summary>
public sealed class LocaleStrings
{
    // ── Brand / welcome ──
    public string Tagline { get; init; } = "Forensic Trace Hunter";
    public string BadgeReadOnly { get; init; } = "READ-ONLY";
    public string BadgeOffline { get; init; } = "OFFLINE";
    public string BadgeNoMutations { get; init; } = "SCAN READ-ONLY";

    // ── Main menu ──
    public string MenuMainTitle { get; init; } = "Main menu";
    public string MenuChooseAction { get; init; } = "choose an action";
    public string MenuHuntTitle { get; init; } = "Search software traces";
    public string MenuHuntDesc { get; init; } = "Hunt a program across every forensic technique";
    public string MenuAboutTitle { get; init; } = "About GhostTrace";
    public string MenuAboutDesc { get; init; } = "Version, techniques, guarantees";
    public string MenuExitTitle { get; init; } = "Exit";
    public string MenuExitDesc { get; init; } = "End the session";
    public string PromptSoftwareName { get; init; } = "Software name";
    public string PromptSoftwareNameHint { get; init; } = "e.g. nvidia, adobe, steam";
    public string ErrorEmptyName { get; init; } = "Cannot be empty.";
    public string PromptOutputDir { get; init; } = "Output directory";
    public string EnterEquals { get; init; } = "Enter =";
    public string PromptAnotherSearch { get; init; } = "Search another program?";
    public string YesNoExitHint { get; init; } = "Y = yes · anything else = exit";
    public string AffirmativeKey { get; init; } = "Y";
    public string Goodbye { get; init; } = "Closing GhostTrace.";

    // ── About ──
    public string AboutHeader { get; init; } = "About GhostTrace";
    public string LblVersion { get; init; } = "Version";
    public string LblPlatform { get; init; } = "Platform";
    public string LblModules { get; init; } = "Modules";
    public string LblMode { get; init; } = "Mode";
    public string ModulesValue { get; init; } = "22 forensic";
    public string ModeValue { get; init; } = "Read-only scan · Opt-in cleanup · Offline";
    public string ColModule { get; init; } = "Module";
    public string ColCoverage { get; init; } = "Coverage";
    public string GuaranteesTitle { get; init; } = "Guarantees";
    public string Guarantee1 { get; init; } = "The scan is read-only — nothing is changed while scanning.";
    public string Guarantee2 { get; init; } = "Removal happens only after you select and confirm (YES), with a log.";
    public string Guarantee3 { get; init; } = "Forensic evidence (execution/history) is never deletable.";
    public string Guarantee4 { get; init; } = "No network calls are made.";
    public string PressAnyKey { get; init; } = "Press any key to return...";

    // ── Scan command: intro panel ──
    public string PanelHeaderHunter { get; init; } = "GhostTrace  v{0}  -  Trace Hunter";
    public string LblHost { get; init; } = "Host";
    public string LblOs { get; init; } = "OS";
    public string LblStart { get; init; } = "Start";
    public string LblTarget { get; init; } = "Target";
    public string LblOutput { get; init; } = "Output";
    public string FullTriage { get; init; } = "(full triage)";

    // ── Scan command: result + matches ──
    public string SummaryHeader { get; init; } = "Result";
    public string LblTechniques { get; init; } = "Techniques";
    public string LblTraces { get; init; } = "Traces";
    public string LblStatus { get; init; } = "Status";
    public string TracesOfFmt { get; init; } = "{0} of {1}";
    public string NoTracesOfFmt { get; init; } = "no traces of \"{0}\"";
    public string MatchesTitleFmt { get; init; } = "Traces of \"{0}\"";
    public string ColTechnique { get; init; } = "Technique";
    public string ColCategory { get; init; } = "Category";
    public string ColArtifact { get; init; } = "Artifact";
    public string ColWhen { get; init; } = "When";
    public string NoTracesFoundFmt { get; init; } = "No traces of {0} found.";
    public string MoreTracesFmt { get; init; } = "… and {0} more traces in the report";

    // ── Scan command: cleanup ──
    public string NoRemovable { get; init; } = "No removable traces (only execution evidence was found — not deletable).";
    public string PromptRemoveFmt { get; init; } = "Remove traces of \"{0}\"?";
    public string RemovableCountFmt { get; init; } = "({0} removable)";
    public string SelectToRemove { get; init; } = "Select what to remove";
    public string SelectHint { get; init; } = "forensic evidence is not listed here";
    public string MultiSelectInstr { get; init; } = "(<space> mark · <enter> confirm)";
    public string MoreChoices { get; init; } = "(scroll for more)";
    public string NothingSelected { get; init; } = "Nothing selected — no removal performed.";
    public string WarnDeleteFmt { get; init; } = "{0} item(s) will be permanently deleted.";
    public string Warning { get; init; } = "WARNING:";
    public string TypeToConfirmFmt { get; init; } = "Type {0} to confirm:";
    public string ConfirmWord { get; init; } = "YES";
    public string Cancelled { get; init; } = "Cancelled — nothing was removed.";
    public string TagRemoved { get; init; } = "removed";
    public string TagSkipped { get; init; } = "skipped";
    public string TagError { get; init; } = "error";
    public string CleanupSummaryFmt { get; init; } = "{0} removed · {1} skipped · {2} error(s)";
    public string LblLog { get; init; } = "Log";
    public string PromptExportReport { get; init; } = "Export report (.txt)?";
    public string LblReport { get; init; } = "Report";
    public string KindFolder { get; init; } = "Folder";
    public string KindFile { get; init; } = "File";
    public string KindRegistry { get; init; } = "Registry";
    public string NotFoundFmt { get; init; } = "not found: {0}";
    public string InvalidRegPathFmt { get; init; } = "invalid registry path: {0}";
    public string UnsupportedHiveFmt { get; init; } = "unsupported hive: {0}";
    public string KeyNotFoundFmt { get; init; } = "key not found: {0}";
    public string ValueNotFoundFmt { get; init; } = "value not found: {0}";

    // ── Cleanup log file ──
    public string LogTitle { get; init; } = "GhostTrace Cleanup Log";
    public string LogGenerated { get; init; } = "Generated";
    public string LogTarget { get; init; } = "Target";
    public string LogCounts { get; init; } = "Removed: {0} | Skipped: {1} | Errors: {2}";

    // ── Errors / misc ──
    public string RequiresWindows { get; init; } = "GhostTrace requires Windows.";
    public string NoModulesSelected { get; init; } = "No modules selected to run.";
    public string OutputDirErrorFmt { get; init; } = "Unable to create output directory '{0}': {1}";
    public string ScanCancelled { get; init; } = "Scan cancelled by user.";
    public string ReportWriteWarnFmt { get; init; } = "Warning: failed to write report: {0}";

    // ── Privilege guard ──
    public string PrivInsufficient { get; init; } = "Insufficient privileges.";
    public string PrivRequiresAdmin { get; init; } = "GhostTrace must be run as Administrator.";
    public string PrivRightClick { get; init; } = "Right-click the executable and choose";
    public string PrivRunAsAdmin { get; init; } = "\"Run as administrator\".";
    public string PrivPressKey { get; init; } = "Press any key to exit.";

    // ── TXT report ──
    public string RptTitle { get; init; } = "GhostTrace Forensic Report";
    public string RptHost { get; init; } = "Host";
    public string RptOs { get; init; } = "OS";
    public string RptStarted { get; init; } = "Started";
    public string RptFinished { get; init; } = "Finished";
    public string RptDuration { get; init; } = "Duration";
    public string RptFilter { get; init; } = "Filter";
    public string RptFindings { get; init; } = "Findings";
    public string RptMatches { get; init; } = "Matches";
    public string RptStatus { get; init; } = "Status";
    public string RptModules { get; init; } = "Modules";
    public string RptColName { get; init; } = "Name";
    public string RptColStatus { get; init; } = "Status";
    public string RptColFindings { get; init; } = "Findings";
    public string RptColMatches { get; init; } = "Matches";
    public string RptColErrors { get; init; } = "Errors";
    public string RptColDuration { get; init; } = "Duration";
    public string RptMatchedFindings { get; init; } = "Matched findings";
    public string RptDescription { get; init; } = "Description";
    public string RptSource { get; init; } = "Source";
    public string RptRawValue { get; init; } = "RawValue";
}
