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

    private FakeScanResult CreateFakeResult(
        string moduleName,
        IReadOnlyList<ScanFinding> findings,
        ScanStatus status = ScanStatus.Success,
        IReadOnlyList<string>? errors = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new FakeScanResult(
            moduleName,
            status,
            DateTimeOffset.UtcNow,
            findings,
            errors ?? new List<string>().AsReadOnly(),
            metadata ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        );
    }

    [Fact]
    public void Compose_NormalCorrelation_ShouldAggregateMetadataCorrectly()
    {
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

        var correlationResult = _composer.Compose(comResult, regResult);

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
        var comResult = CreateFakeResult("ScheduledTasksScanModule", new List<ScanFinding>().AsReadOnly());
        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };
        var regResult = CreateFakeResult("TaskCacheScanModule", regFindings.AsReadOnly());

        var correlationResult = _composer.Compose(comResult, regResult);

        Assert.NotEmpty(correlationResult.Warnings);
        Assert.Contains(correlationResult.Warnings, w => w.Contains("COM findings are empty"));
        
        Assert.Single(correlationResult.CorrelatedFindings);
        var finding = correlationResult.CorrelatedFindings[0];
        Assert.Equal("Inconclusive", finding.Label);
        Assert.Equal("<GLOBAL>", finding.LogicalPath);
    }

    [Fact]
    public void Compose_WithPartialComEnumerationFailure_ShouldDowngradeAbsenceConclusions()
    {
        // Arrange: COM returned some tasks but reported per-folder enumeration errors
        // (PartialSuccess) — classic partial COM failure.
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };
        var comResult = CreateFakeResult(
            "ScheduledTasksScanModule",
            comFindings.AsReadOnly(),
            status: ScanStatus.PartialSuccess,
            errors: new List<string> { "Error retrieving tasks list for folder '\\Microsoft': access denied" }.AsReadOnly());

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };
        var regResult = CreateFakeResult("TaskCacheScanModule", regFindings.AsReadOnly());

        var correlationResult = _composer.Compose(comResult, regResult);

        // Assert: missing coverage must not be labelled a high-severity ghost task.
        Assert.Contains(correlationResult.Warnings, w => w.Contains("COM collection is degraded"));
        var finding = Assert.Single(correlationResult.CorrelatedFindings);
        Assert.Equal("GhostCandidate-DegradedEvidence", finding.Label);
        Assert.Equal(CorrelationSeverity.Low, finding.Severity);
    }

    [Fact]
    public void Compose_WithComItemErrorsInMetadata_ShouldTreatAsDegraded()
    {
        // Arrange: status Success but the module counted per-item read failures.
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };
        var comResult = CreateFakeResult(
            "ScheduledTasksScanModule",
            comFindings.AsReadOnly(),
            metadata: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["TotalEnumerated"] = "10",
                ["TotalCollected"] = "8",
                ["TotalWithError"] = "2"
            }));

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };
        var regResult = CreateFakeResult("TaskCacheScanModule", regFindings.AsReadOnly());

        var correlationResult = _composer.Compose(comResult, regResult);

        var finding = Assert.Single(correlationResult.CorrelatedFindings);
        Assert.Equal("GhostCandidate-DegradedEvidence", finding.Label);
    }

    [Fact]
    public void Compose_WithPartialRegistryAccessFailure_ShouldWarnAndFlagCoverageGap()
    {
        // Arrange: registry walk hit access-denied subkeys (PartialSuccess + errors).
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };
        var comResult = CreateFakeResult("ScheduledTasksScanModule", comFindings.AsReadOnly());

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "SafeTask", $@"{RegPrefix}SafeTask", null, "Id: {...} | HasSD: True")
        };
        var regResult = CreateFakeResult(
            "TaskCacheScanModule",
            regFindings.AsReadOnly(),
            status: ScanStatus.PartialSuccess,
            errors: new List<string> { "Access denied opening subkey 'Tree\\Hidden': denied" }.AsReadOnly());

        var correlationResult = _composer.Compose(comResult, regResult);

        Assert.Contains(correlationResult.Warnings, w => w.Contains("Registry collection is degraded"));
        Assert.Contains(correlationResult.CorrelatedFindings, f => f.Label == "DegradedRegistryCoverage");
    }

    [Fact]
    public void Compose_WithFailedComCollection_ShouldReturnOverallInconclusive()
    {
        // Arrange: the COM source failed entirely (e.g. Schedule.Service unavailable).
        var comResult = CreateFakeResult(
            "ScheduledTasksScanModule",
            new List<ScanFinding>().AsReadOnly(),
            status: ScanStatus.Failure,
            errors: new List<string> { "COM ProgID 'Schedule.Service' is not registered on this system." }.AsReadOnly());

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };
        var regResult = CreateFakeResult("TaskCacheScanModule", regFindings.AsReadOnly());

        var correlationResult = _composer.Compose(comResult, regResult);

        // Assert: no per-task ghost labels — only a global inconclusive verdict.
        Assert.Contains(correlationResult.Warnings, w => w.Contains("failed entirely"));
        var finding = Assert.Single(correlationResult.CorrelatedFindings);
        Assert.Equal("Inconclusive", finding.Label);
        Assert.Equal("<GLOBAL>", finding.LogicalPath);
        Assert.Equal(CorrelationSeverity.Info, finding.Severity);
    }

    [Fact]
    public void Compose_WithInvalidCategories_ShouldFilterAndWarn()
    {
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

        var correlationResult = _composer.Compose(comResult, regResult);

        Assert.NotEmpty(correlationResult.Warnings);
        Assert.Contains(correlationResult.Warnings, w => w.Contains("Filtered out"));
        
        // As collections got filtered out to 0, it falls back to empty results.
        Assert.Empty(correlationResult.CorrelatedFindings);
    }
}
