using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using GhostTrace.Analysis;
using Xunit;

namespace GhostTrace.Tests;

public sealed class ScheduledTasksCorrelationComposerTests
{
    private readonly ScheduledTasksCorrelationComposer _composer = new();

    private const string RegPrefix = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\";

    private FakeScanResult CreateFakeResult(string moduleName, IReadOnlyList<ScanFinding> findings)
    {
        return new FakeScanResult(
            moduleName,
            ScanStatus.Success,
            DateTimeOffset.UtcNow,
            findings,
            new List<string>().AsReadOnly(),
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        );
    }

    [Fact]
    public void Compose_NormalCorrelation_ShouldAggregateMetadataCorrectly()
    {
        // Arrange
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };
        var comResult = CreateFakeResult("ScheduledTasksScanModule", comFindings.AsReadOnly());

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };
        var regResult = CreateFakeResult("TaskCacheScanModule", regFindings.AsReadOnly());

        // Act
        var correlationResult = _composer.Compose(comResult, regResult);

        // Assert
        Assert.NotNull(correlationResult);
        Assert.Equal("ScheduledTasksCorrelationComposer", correlationResult.ComponentName);
        Assert.Empty(correlationResult.Warnings);
        
        Assert.Single(correlationResult.CorrelatedFindings);
        var finding = correlationResult.CorrelatedFindings[0];
        Assert.Equal("GhostCandidate", finding.Label);

        // Assert Metadata formatting
        var meta = correlationResult.Metadata;
        Assert.True(meta.ContainsKey("TotalCorrelations"));
        Assert.Equal("1", meta["TotalCorrelations"]);
        
        Assert.Equal("1", meta["Severity_High"]);
        Assert.Equal("0", meta["Severity_Low"]);
        Assert.Equal("1", meta["Label_GhostCandidate"]);
    }

    [Fact]
    public void Compose_WithEmptyComResult_ShouldProduceWarningsAndInconclusiveResult()
    {
        // Arrange
        var comResult = CreateFakeResult("ScheduledTasksScanModule", new List<ScanFinding>().AsReadOnly());
        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };
        var regResult = CreateFakeResult("TaskCacheScanModule", regFindings.AsReadOnly());

        // Act
        var correlationResult = _composer.Compose(comResult, regResult);

        // Assert
        Assert.NotEmpty(correlationResult.Warnings);
        Assert.Contains(correlationResult.Warnings, w => w.Contains("COM findings are empty"));
        
        Assert.Single(correlationResult.CorrelatedFindings);
        var finding = correlationResult.CorrelatedFindings[0];
        Assert.Equal("Inconclusive", finding.Label);
        Assert.Equal("<GLOBAL>", finding.LogicalPath);
    }

    [Fact]
    public void Compose_WithInvalidCategories_ShouldFilterAndWarn()
    {
        // Arrange
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("FilesystemEntry", "BadFile", @"C:\BadFile", null, "File")
        };
        var comResult = CreateFakeResult("FilesystemScanModule", comFindings.AsReadOnly());

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("RegistryEntry", "BadReg", @"HKLM\Software\BadReg", null, "Key")
        };
        var regResult = CreateFakeResult("RegistryScanModule", regFindings.AsReadOnly());

        // Act
        var correlationResult = _composer.Compose(comResult, regResult);

        // Assert
        Assert.NotEmpty(correlationResult.Warnings);
        Assert.Contains(correlationResult.Warnings, w => w.Contains("Filtered out"));
        
        // As collections got filtered out to 0, it falls back to empty results.
        Assert.Empty(correlationResult.CorrelatedFindings);
    }
}
