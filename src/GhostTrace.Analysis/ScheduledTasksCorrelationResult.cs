using System.Collections.ObjectModel;

namespace GhostTrace.Analysis;

public sealed record ScheduledTasksCorrelationResult(
    string ComponentName,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ScheduledTaskCorrelationFinding> CorrelatedFindings,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<string> Warnings
);
