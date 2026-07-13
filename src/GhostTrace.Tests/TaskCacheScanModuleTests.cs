using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Pipeline;
using GhostTrace.Modules.ScheduledTasks;
using Xunit;

namespace GhostTrace.Tests;

[SupportedOSPlatform("windows")]
public sealed class TaskCacheScanModuleTests
{
    [Fact]
    public async Task RunAsync_WithEmptyOptions_ShouldReturnValidResultWithoutCrashing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var module = new TaskCacheScanModule();
        var options = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        var context = new FakeScanContext(module.Name, options);
        var pipeline = new ScanPipeline([module]);

        var results = await pipeline.ExecuteAsync(context, CancellationToken.None);

        Assert.NotNull(results);
        Assert.Single(results);
        
        var result = results[0];
        Assert.Equal(module.Name, result.ModuleName);
        Assert.True(result.Status is ScanStatus.Success or ScanStatus.PartialSuccess or ScanStatus.Failure);
        Assert.NotNull(result.Findings);
        
        Assert.True(result.Metadata.ContainsKey("TotalEnumeratedKeys"));
        Assert.True(result.Metadata.ContainsKey("TotalTaskLikeEntries"));
        Assert.True(result.Metadata.ContainsKey("TotalMissingSd"));
        Assert.True(result.Metadata.ContainsKey("TotalAnomalies"));
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var module = new TaskCacheScanModule();
        var options = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        var context = new FakeScanContext(module.Name, options);
        var pipeline = new ScanPipeline([module]);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel(); 

        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(context, cts.Token));
    }
}
