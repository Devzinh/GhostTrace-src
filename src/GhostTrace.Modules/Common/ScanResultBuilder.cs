using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Modules.Common;

/// <summary>
/// Accumulates findings, errors and metadata for a scan module and derives the final
/// <see cref="ScanStatus"/> with a single, well-tested rule. Centralising the status
/// computation here fixes the class of bug that was previously copy-pasted (and broken)
/// across multiple modules.
///
/// Default status rule:
/// <list type="bullet">
///   <item>findings &gt; 0, no errors  → <see cref="ScanStatus.Success"/></item>
///   <item>findings &gt; 0, with errors → <see cref="ScanStatus.PartialSuccess"/></item>
///   <item>no findings, with errors    → <see cref="ScanStatus.Failure"/></item>
///   <item>no findings, no errors       → <see cref="ScanStatus.Success"/> (clean system)</item>
/// </list>
/// A module may override the outcome with <see cref="ForceStatus"/> (e.g. a hard failure
/// opening a root key, or <see cref="ScanStatus.Skipped"/> when no target applies).
/// </summary>
public sealed class ScanResultBuilder
{
    private readonly string _moduleName;
    private readonly List<ScanFinding> _findings = new();
    private readonly List<string> _errors = new();
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private ScanStatus? _forcedStatus;

    public ScanResultBuilder(string moduleName)
    {
        _moduleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
    }

    public int FindingCount => _findings.Count;
    public int ErrorCount => _errors.Count;

    public ScanResultBuilder AddFinding(ScanFinding finding)
    {
        if (finding != null) _findings.Add(finding);
        return this;
    }

    public ScanResultBuilder AddFinding(
        string category,
        string description,
        string source,
        DateTimeOffset? timestampUtc = null,
        string? rawValue = null)
    {
        _findings.Add(new ScanFinding(category, description, source, timestampUtc, rawValue));
        return this;
    }

    public ScanResultBuilder AddError(string message)
    {
        if (!string.IsNullOrEmpty(message)) _errors.Add(message);
        return this;
    }

    public ScanResultBuilder SetMetadata(string key, string value)
    {
        if (!string.IsNullOrEmpty(key)) _metadata[key] = value ?? string.Empty;
        return this;
    }

    public ScanResultBuilder SetMetadata(string key, int value) => SetMetadata(key, value.ToString());

    /// <summary>
    /// Overrides the derived status. Used for hard failures (e.g. a required root key
    /// is missing) or to mark the module <see cref="ScanStatus.Skipped"/>.
    /// </summary>
    public ScanResultBuilder ForceStatus(ScanStatus status)
    {
        _forcedStatus = status;
        return this;
    }

    public IScanResult Build()
    {
        var status = _forcedStatus ?? Derive();
        return new ScanResult(
            ModuleName: _moduleName,
            Status: status,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Findings: _findings.AsReadOnly(),
            Errors: _errors.AsReadOnly(),
            Metadata: new ReadOnlyDictionary<string, string>(_metadata));
    }

    private ScanStatus Derive()
    {
        bool hasFindings = _findings.Count > 0;
        bool hasErrors = _errors.Count > 0;

        if (!hasFindings && hasErrors) return ScanStatus.Failure;
        if (hasFindings && hasErrors) return ScanStatus.PartialSuccess;
        return ScanStatus.Success;
    }
}
