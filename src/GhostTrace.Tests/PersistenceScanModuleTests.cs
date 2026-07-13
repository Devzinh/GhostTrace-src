using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Pipeline;
using GhostTrace.Modules.Persistence;
using Xunit;

namespace GhostTrace.Tests;

[SupportedOSPlatform("windows")]
public sealed class PersistenceScanModuleTests
{
    [Fact]
    public async Task RunAsync_WithEmptyOptions_ShouldReturnValidResultWithoutCrashing()
    {
        // Pula silenciosamente em ambientes não-Windows (como Linux no GitHub Actions)
        if (!OperatingSystem.IsWindows())
            return;

        var module = new PersistenceScanModule();
        
        // Dicionário imutável vazio, já que o módulo de persistência conhece os alvos internamente
        var options = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        var context = new FakeScanContext(module.Name, options);
        var pipeline = new ScanPipeline([module]);

        var results = await pipeline.ExecuteAsync(context, CancellationToken.None);

        Assert.NotNull(results);
        Assert.Single(results);
        
        var result = results[0];
        
        // A garantia de que Executou até o fim sem engasgar com exceções unhandled
        Assert.Equal(module.Name, result.ModuleName);
        Assert.True(result.Status is ScanStatus.Success or ScanStatus.PartialSuccess or ScanStatus.Failure);
        Assert.NotNull(result.Findings);
        
        // Verifica que o módulo produziu o metadado que prometeu em seu contrato interno
        Assert.True(result.Metadata.ContainsKey("RegistryFindings"));
        Assert.True(result.Metadata.ContainsKey("StartupFindings"));
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var module = new PersistenceScanModule();
        var options = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        var context = new FakeScanContext(module.Name, options);
        var pipeline = new ScanPipeline([module]);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancela antes de iniciar a pipeline

        // Garante que o módulo e a pipeline repeitam cooperativamente o Request de Aborto
        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(context, cts.Token));
    }
}
