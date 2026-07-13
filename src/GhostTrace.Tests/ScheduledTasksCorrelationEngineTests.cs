using System;
using System.Collections.Generic;
using GhostTrace.Core.Models;
using GhostTrace.Analysis;
using Xunit;

namespace GhostTrace.Tests;

public sealed class ScheduledTasksCorrelationEngineTests
{
    private readonly ScheduledTasksCorrelationEngine _engine = new();

    private const string ComPrefix = @"\";
    private const string RegPrefix = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\";

    [Fact]
    public void Correlate_MissingSdWithoutCom_ShouldReturnHighGhostCandidate()
    {
        var comFindings = new List<ScanFinding>(); // COM is missing this task, but NOT empty globally
        comFindings.Add(new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe"));

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD (Ghost Task Indicator)")
        };

        var results = _engine.Correlate(comFindings, regFindings);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal(@"\ghosttask", result.LogicalPath);
        Assert.Equal("GhostCandidate", result.Label);
        Assert.Equal(CorrelationSeverity.High, result.Severity);
        Assert.Null(result.ComSource);
        Assert.NotNull(result.RegistrySource);
    }

    [Fact]
    public void Correlate_ZeroIndexWithoutCom_ShouldReturnHighGhostCandidate()
    {
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "IndexTask", $@"{RegPrefix}IndexTask", null, "Id: {...} | HasSD: True | ANOMALIES: MISSING_OR_ZERO_INDEX")
        };

        var results = _engine.Correlate(comFindings, regFindings);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal(@"\indextask", result.LogicalPath);
        Assert.Equal("GhostCandidate", result.Label);
        Assert.Equal(CorrelationSeverity.High, result.Severity);
    }

    [Fact]
    public void Correlate_AnomalyWithCom_ShouldReturnMediumStructuralAnomaly()
    {
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "BrokenTask", @"\BrokenTask", null, "COM sees this")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "BrokenTask", $@"{RegPrefix}BrokenTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };

        var results = _engine.Correlate(comFindings, regFindings);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal(@"\brokentask", result.LogicalPath);
        Assert.Equal("StructuralAnomaly", result.Label);
        Assert.Equal(CorrelationSeverity.Medium, result.Severity);
        Assert.NotNull(result.ComSource);
        Assert.NotNull(result.RegistrySource);
    }

    [Fact]
    public void Correlate_TaskCacheNormalWithoutCom_ShouldReturnStructuralOnly()
    {
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "OrphanTask", $@"{RegPrefix}OrphanTask", null, "Id: {...} | HasSD: True")
        };

        var results = _engine.Correlate(comFindings, regFindings);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal(@"\orphantask", result.LogicalPath);
        Assert.Equal("StructuralOnly", result.Label);
        Assert.Equal(CorrelationSeverity.Medium, result.Severity);
        Assert.Null(result.ComSource);
    }

    [Fact]
    public void Correlate_MissingSdWithoutCom_WithDegradedComCollection_ShouldDowngradeToWarning()
    {
        // Arrange: COM enumeration was only partial — a folder failed to enumerate.
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD (Ghost Task Indicator)")
        };

        var results = _engine.Correlate(comFindings, regFindings, comCollectionDegraded: true, regCollectionDegraded: false);

        // Assert: absence from a partially-enumerated COM view is degraded evidence.
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("GhostCandidate-DegradedEvidence", result.Label);
        Assert.Equal(CorrelationSeverity.Low, result.Severity);
        Assert.Contains("partial", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Correlate_StructuralOnly_WithDegradedComCollection_ShouldDowngradeToInfo()
    {
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "OrphanTask", $@"{RegPrefix}OrphanTask", null, "Id: {...} | HasSD: True")
        };

        var results = _engine.Correlate(comFindings, regFindings, comCollectionDegraded: true, regCollectionDegraded: false);

        Assert.Single(results);
        Assert.Equal("StructuralOnly-DegradedEvidence", results[0].Label);
        Assert.Equal(CorrelationSeverity.Info, results[0].Severity);
    }

    [Fact]
    public void Correlate_AnomalyWithCom_WithDegradedCollections_ShouldKeepStructuralAnomaly()
    {
        // Arrange: a directly observed registry anomaly does not depend on absence,
        // so degraded collection must not suppress it.
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "BrokenTask", @"\BrokenTask", null, "COM sees this")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "BrokenTask", $@"{RegPrefix}BrokenTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };

        var results = _engine.Correlate(comFindings, regFindings, comCollectionDegraded: true, regCollectionDegraded: true);

        var anomaly = Assert.Single(results, r => r.Label == "StructuralAnomaly");
        Assert.Equal(CorrelationSeverity.Medium, anomaly.Severity);
    }

    [Fact]
    public void Correlate_WithDegradedRegistryCollection_ShouldEmitGlobalCoverageWarning()
    {
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };
        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "SafeTask", $@"{RegPrefix}SafeTask", null, "Id: {...} | HasSD: True")
        };

        var results = _engine.Correlate(comFindings, regFindings, comCollectionDegraded: false, regCollectionDegraded: true);

        var coverage = Assert.Single(results, r => r.Label == "DegradedRegistryCoverage");
        Assert.Equal("<GLOBAL>", coverage.LogicalPath);
        Assert.Equal(CorrelationSeverity.Info, coverage.Severity);
    }

    [Fact]
    public void Correlate_GlobalEmptyCom_ShouldNotGenerateAvalancheOfGhosts()
    {
        var comFindings = new List<ScanFinding>(); // Globally empty!
        
        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "Task1", $@"{RegPrefix}Task1", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD"),
            new ScanFinding("ScheduledTaskCacheEntry", "Task2", $@"{RegPrefix}Task2", null, "Id: {...} | HasSD: True"),
        };

        var results = _engine.Correlate(comFindings, regFindings);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal("<GLOBAL>", result.LogicalPath);
        Assert.Equal("Inconclusive", result.Label);
        Assert.Equal(CorrelationSeverity.Info, result.Severity);
    }
}
