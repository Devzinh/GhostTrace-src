using System.Collections.ObjectModel;
using GhostTrace.Core.Abstractions;

namespace GhostTrace.CLI.Runtime;

/// <summary>
/// A minimal in-memory scan context for CLI testing and execution.
/// Honors the case-insensitive option contract from <see cref="IScanContext.GetOption(string)"/>.
/// </summary>
internal sealed record CliScanContext : IScanContext
{
    private readonly IReadOnlyDictionary<string, string> _options;

    public CliScanContext(
        string ModuleName,
        Guid ScanId,
        DateTimeOffset StartedAtUtc,
        IReadOnlyDictionary<string, string>? Options = null)
    {
        this.ModuleName = ModuleName;
        this.ScanId = ScanId;
        this.StartedAtUtc = StartedAtUtc;

        if (Options is null || Options.Count == 0)
        {
            _options = EmptyOptions;
            return;
        }

        // Promote to case-insensitive lookup; preserves callers that may have used
        // case-sensitive dictionaries inconsistently across the codebase.
        var caseInsensitive = new Dictionary<string, string>(Options.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in Options)
        {
            caseInsensitive[kvp.Key] = kvp.Value;
        }
        _options = new ReadOnlyDictionary<string, string>(caseInsensitive);
    }

    public string ModuleName { get; }
    public Guid ScanId { get; }
    public DateTimeOffset StartedAtUtc { get; }

    public string? GetOption(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return _options.TryGetValue(key, out var value) ? value : null;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyOptions =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
