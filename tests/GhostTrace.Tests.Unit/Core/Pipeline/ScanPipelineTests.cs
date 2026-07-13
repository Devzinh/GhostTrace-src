using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using GhostTrace.Core.Pipeline;
using Xunit;

namespace GhostTrace.Tests.Unit.Core.Pipeline;

public class ScanPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_RunsModulesSequentially_AndPreservesOrder()
    {
        var context = new EmptyScanContext();
        var module1 = new FakeScanModule("Module1", ScanStatus.Success);
        var module2 = new FakeScanModule("Module2", ScanStatus.PartialSuccess);
        var module3 = new FakeScanModule("Module3", ScanStatus.Success);

        var pipeline = new ScanPipeline(new IScanModule[] { module1, module2, module3 });

        var results = await pipeline.ExecuteAsync(context);

        Assert.Equal(3, results.Count);
        Assert.Equal(ScanStatus.Success, results[0].Status);
        Assert.Equal(ScanStatus.PartialSuccess, results[1].Status);
        Assert.Equal(ScanStatus.Success, results[2].Status);

        Assert.True(module1.RunAt < module2.RunAt);
        Assert.True(module2.RunAt < module3.RunAt);
    }

    private class EmptyScanContext : IScanContext
    {
        public string ModuleName => "Test";
        public Guid ScanId { get; } = Guid.NewGuid();
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
        public string? GetOption(string key) => null;
    }

    private class FakeScanResult : IScanResult
    {
        public FakeScanResult(ScanStatus status, string moduleName)
        {
            ModuleName = moduleName;
            Status = status;
            Findings = Array.Empty<ScanFinding>();
            Errors = Array.Empty<string>();
            CompletedAtUtc = DateTimeOffset.UtcNow;
            Metadata = new Dictionary<string, string>();
        }

        public string ModuleName { get; }
        public ScanStatus Status { get; }
        public DateTimeOffset CompletedAtUtc { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
        public IReadOnlyList<ScanFinding> Findings { get; }
        public IReadOnlyList<string> Errors { get; }
    }

    private class FakeScanModule : IScanModule
    {
        private readonly ScanStatus _status;

        public FakeScanModule(string name, ScanStatus status)
        {
            Name = name;
            _status = status;
        }

        public string Name { get; }
        public DateTimeOffset RunAt { get; private set; }

        public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
        {
            RunAt = DateTimeOffset.UtcNow;
            Thread.Sleep(5); // Ensure time moves forward slightly
            return Task.FromResult<IScanResult>(new FakeScanResult(_status, Name));
        }
    }
}
