using GhostTrace.Core.Abstractions;

namespace GhostTrace.Tests;

public sealed record FakeScanContext(
    string ModuleName, 
    IReadOnlyDictionary<string, string> Options) : IScanContext
{
    public Guid ScanId => Guid.NewGuid();
    public DateTimeOffset StartedAtUtc => DateTimeOffset.UtcNow;
    
    public string? GetOption(string key)
    {
        return Options.TryGetValue(key, out var value) ? value : null;
    }
}
