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
        // Arrange
        var comFindings = new List<ScanFinding>(); // COM is missing this task, but NOT empty globally
        comFindings.Add(new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe"));

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "GhostTask", $@"{RegPrefix}GhostTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD (Ghost Task Indicator)")
        };

        // Act
        var results = _engine.Correlate(comFindings, regFindings);

        // Assert
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
        // Arrange
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "IndexTask", $@"{RegPrefix}IndexTask", null, "Id: {...} | HasSD: True | ANOMALIES: MISSING_OR_ZERO_INDEX")
        };

        // Act
        var results = _engine.Correlate(comFindings, regFindings);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal(@"\indextask", result.LogicalPath);
        Assert.Equal("GhostCandidate", result.Label);
        Assert.Equal(CorrelationSeverity.High, result.Severity);
    }

    [Fact]
    public void Correlate_AnomalyWithCom_ShouldReturnMediumStructuralAnomaly()
    {
        // Arrange
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "BrokenTask", @"\BrokenTask", null, "COM sees this")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "BrokenTask", $@"{RegPrefix}BrokenTask", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD")
        };

        // Act
        var results = _engine.Correlate(comFindings, regFindings);

        // Assert
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
        // Arrange
        var comFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTask", "SafeTask", @"\SafeTask", null, "Safe")
        };

        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "OrphanTask", $@"{RegPrefix}OrphanTask", null, "Id: {...} | HasSD: True")
        };

        // Act
        var results = _engine.Correlate(comFindings, regFindings);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal(@"\orphantask", result.LogicalPath);
        Assert.Equal("StructuralOnly", result.Label);
        Assert.Equal(CorrelationSeverity.Medium, result.Severity);
        Assert.Null(result.ComSource);
    }

    [Fact]
    public void Correlate_GlobalEmptyCom_ShouldNotGenerateAvalancheOfGhosts()
    {
        // Arrange
        var comFindings = new List<ScanFinding>(); // Globally empty!
        
        var regFindings = new List<ScanFinding>
        {
            new ScanFinding("ScheduledTaskCacheEntry", "Task1", $@"{RegPrefix}Task1", null, "Id: {...} | HasSD: False | ANOMALIES: MISSING_SD"),
            new ScanFinding("ScheduledTaskCacheEntry", "Task2", $@"{RegPrefix}Task2", null, "Id: {...} | HasSD: True"),
        };

        // Act
        var results = _engine.Correlate(comFindings, regFindings);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("<GLOBAL>", result.LogicalPath);
        Assert.Equal("Inconclusive", result.Label);
        Assert.Equal(CorrelationSeverity.Info, result.Severity);
    }
}
