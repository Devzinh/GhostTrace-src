using System.Collections.ObjectModel;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Pipeline;
using GhostTrace.Modules;
using Xunit;

namespace GhostTrace.Tests;

public sealed class ScanPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WithEchoModule_ShouldReturnSuccessAndMetadata()
    {
        var module = new EchoScanModule();
        var pipeline = new ScanPipeline([module]);
        var context = new FakeScanContext("EchoScanModule", new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));

        var results = await pipeline.ExecuteAsync(context, CancellationToken.None);

        Assert.NotNull(results);
        Assert.Single(results);
        var result = results[0];
        Assert.Equal(ScanStatus.Success, result.Status);
        Assert.Equal("EchoModule", result.ModuleName);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        var module = new EchoScanModule();
        var pipeline = new ScanPipeline([module]);
        var context = new FakeScanContext("EchoScanModule", new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
        
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Already cancelled

        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(context, cts.Token));
    }
}
