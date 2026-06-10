using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GhostTrace.Core.Abstractions;

namespace GhostTrace.Analysis;

public sealed class ScheduledTasksCorrelationComposer
{
    private readonly ScheduledTasksCorrelationEngine _engine;

    public ScheduledTasksCorrelationComposer(ScheduledTasksCorrelationEngine? engine = null)
    {
        _engine = engine ?? new ScheduledTasksCorrelationEngine();
    }

    public ScheduledTasksCorrelationResult Compose(IScanResult comResult, IScanResult registryResult)
    {
        var warnings = new List<string>();

        if (comResult.Findings.Count == 0)
        {
            warnings.Add("COM findings are empty. Correlation will be globally inconclusive or degraded.");
        }
        
        if (registryResult.Findings.Count == 0)
        {
            warnings.Add("Registry findings are empty. Correlation is degraded.");
        }

        // Foca explicitamente nas categorias esperadas para Scheduled Tasks
        var comFindings = comResult.Findings.Where(f => f.Category == "ScheduledTask").ToList();
        var regFindings = registryResult.Findings.Where(f => f.Category == "ScheduledTaskCacheEntry").ToList();

        if (comResult.Findings.Count > 0 && comFindings.Count == 0)
        {
            warnings.Add($"Filtered out {comResult.Findings.Count} findings from COM result because Category was not 'ScheduledTask'.");
        }

        if (registryResult.Findings.Count > 0 && regFindings.Count == 0)
        {
            warnings.Add($"Filtered out {registryResult.Findings.Count} findings from Registry result because Category was not 'ScheduledTaskCacheEntry'.");
        }

        var correlatedFindings = _engine.Correlate(comFindings, regFindings);

        // Agregando metadados de inteligência
        var metadata = new Dictionary<string, string>();
        metadata["TotalCorrelations"] = correlatedFindings.Count.ToString();
        
        var bySeverity = correlatedFindings.GroupBy(x => x.Severity).ToDictionary(g => g.Key, g => g.Count());
        foreach (var sev in Enum.GetValues<CorrelationSeverity>())
        {
            metadata[$"Severity_{sev}"] = bySeverity.GetValueOrDefault(sev, 0).ToString();
        }

        var byLabel = correlatedFindings.GroupBy(x => x.Label).ToDictionary(g => g.Key, g => g.Count());
        foreach (var kvp in byLabel)
        {
            metadata[$"Label_{kvp.Key}"] = kvp.Value.ToString();
        }

        return new ScheduledTasksCorrelationResult(
            ComponentName: "ScheduledTasksCorrelationComposer",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            CorrelatedFindings: correlatedFindings,
            Metadata: new ReadOnlyDictionary<string, string>(metadata),
            Warnings: warnings.AsReadOnly()
        );
    }
}
