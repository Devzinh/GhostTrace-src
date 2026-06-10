using System.Collections.Generic;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Modules.Common;

/// <summary>
/// A single reusable, immutable <see cref="IScanResult"/> implementation shared by
/// every scan module. Replaces the per-module private result records, removing a
/// large amount of duplicated boilerplate (and the bugs that came with it).
/// </summary>
public sealed record ScanResult(
    string ModuleName,
    ScanStatus Status,
    System.DateTimeOffset CompletedAtUtc,
    IReadOnlyList<ScanFinding> Findings,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> Metadata) : IScanResult;
