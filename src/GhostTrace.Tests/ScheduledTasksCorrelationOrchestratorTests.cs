using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using GhostTrace.Analysis;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using Xunit;

namespace GhostTrace.Tests;

[SupportedOSPlatform("windows")]
public sealed class ScheduledTasksCorrelationOrchestratorTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task RunCorrelationAsync_WithMockedModules_ShouldProduceValidCorrelationResult()
    {
        var orchestrator = new ScheduledTasksCorrelationOrchestrator();

        var comContext = new FakeScanContext(
            "ComContext",
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        );
        var regContext = new FakeScanContext(
            "RegContext",
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        );

        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe"),
        };
        var comModule = new FakeScanModule("ScheduledTasksScanModule", comFindings.AsReadOnly());

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding(
                "ScheduledTaskCacheEntry",
                "GhostTask",
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\GhostTask",
                null,
                "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD"
            ),
        };
        var regModule = new FakeScanModule("TaskCacheScanModule", regFindings.AsReadOnly());

        var result = await orchestrator.RunCorrelationAsync(
            comContext,
            regContext,
            comModule,
            regModule,
            CancellationToken.None
        )!;

        Assert.NotNull(result);
        Assert.Equal("ScheduledTasksCorrelationComposer", result.ComponentName);
        Assert.Empty(result.Warnings);

        Assert.Single(result.CorrelatedFindings);
        Assert.Equal("GhostCandidate", result.CorrelatedFindings[0].Label);
        Assert.Equal("1", result.Metadata["TotalCorrelations"]);
    }
}
