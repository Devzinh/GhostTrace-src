using System;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Pipeline;
using GhostTrace.Modules.ScheduledTasks;

using System.Runtime.Versioning;

namespace GhostTrace.Analysis;

[SupportedOSPlatform("windows")]
public sealed class ScheduledTasksCorrelationOrchestrator
{
    private readonly ScheduledTasksCorrelationComposer _composer;

    public ScheduledTasksCorrelationOrchestrator(ScheduledTasksCorrelationComposer? composer = null)
    {
        _composer = composer ?? new ScheduledTasksCorrelationComposer();
    }

    /// <summary>
    /// Executes the full Scheduled Tasks correlation lifecycle.
    /// Overrides are provided to allow deterministic unit testing without executing real Windows COM/Registry APIs.
    /// </summary>
    public async Task<ScheduledTasksCorrelationResult> RunCorrelationAsync(
        IScanContext comContext,
        IScanContext registryContext,
        IScanModule? comModuleOverride = null,
        IScanModule? registryModuleOverride = null,
        CancellationToken cancellationToken = default)
    {
        // Usa os módulos reais arquitetados caso nenhuma injeção de teste seja fornecida
        var comModule = comModuleOverride ?? new ScheduledTasksScanModule();
        var regModule = registryModuleOverride ?? new TaskCacheScanModule();

        var comPipeline = new ScanPipeline([comModule]);
        var regPipeline = new ScanPipeline([regModule]);

        // Executa as coletas de forma limpa, isolada e tratada pela pipeline do GhostTrace Core
        var comResults = await comPipeline.ExecuteAsync(comContext, cancellationToken);
        var regResults = await regPipeline.ExecuteAsync(registryContext, cancellationToken);

        // O pipeline garante o retorno de um Array na mesma ordem dos módulos de entrada
        var comResult = comResults[0];
        var regResult = regResults[0];

        // Orquestra a fusão e emissão do laudo
        return _composer.Compose(comResult, regResult);
    }
}
