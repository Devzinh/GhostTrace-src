using System;
using System.Runtime.Versioning;

namespace GhostTrace.Modules.Prefetch;

[SupportedOSPlatform("windows")]
public record PrefetchEntry(
    string FileName,
    string PrefetchHash,
    int RunCount,
    DateTimeOffset? LastRunTimeUtc,
    DateTimeOffset[]? AllRunTimesUtc,
    int FileVersion
);
