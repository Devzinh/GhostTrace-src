using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;

namespace GhostTrace.Tests;

public sealed class FakeScanModule : IScanModule
{
    private readonly string _name;
    private readonly IReadOnlyList<ScanFinding> _findings;

    public FakeScanModule(string name, IReadOnlyList<ScanFinding> findings)
    {
        _name = name;
        _findings = findings;
    }

    public string Name => _name;

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IScanResult>(new FakeScanResult(
            _name,
            ScanStatus.Success,
            System.DateTimeOffset.UtcNow,
            _findings,
            new List<string>().AsReadOnly(),
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        ));
    }
}
