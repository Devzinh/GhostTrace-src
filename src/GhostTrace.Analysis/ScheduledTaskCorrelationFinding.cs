namespace GhostTrace.Analysis;

public enum CorrelationSeverity
{
    Info,
    Low,
    Medium,
    High
}

public sealed record ScheduledTaskCorrelationFinding(
    string LogicalPath,
    string Label,
    CorrelationSeverity Severity,
    string Reason,
    string? ComSource,
    string? RegistrySource
);
