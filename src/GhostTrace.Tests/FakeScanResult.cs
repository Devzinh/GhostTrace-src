using System;
using System.Collections.Generic;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Tests;

public sealed record FakeScanResult(
    string ModuleName,
    ScanStatus Status,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<ScanFinding> Findings,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> Metadata) : IScanResult;
