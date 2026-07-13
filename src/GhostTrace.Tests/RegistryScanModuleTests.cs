using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Pipeline;
using GhostTrace.Modules.Registry;
using Xunit;

namespace GhostTrace.Tests;

[SupportedOSPlatform("windows")]
public sealed class RegistryScanModuleTests
{
    [Fact]
    public async Task RunAsync_WithValidEnvironmentKey_ShouldReturnValidResultWithoutCrashing()
    {
        // Skip if not Windows to avoid CI failures on non-Windows hosts
        if (!OperatingSystem.IsWindows())
            return;

        var module = new RegistryScanModule();
        
        // HKEY_CURRENT_USER\Environment is safe and normally accessible without Admin rights
        var options = new Dictionary<string, string>
        {
            ["registryHive"] = "CurrentUser",
            ["subKeyPath"] = "Environment"
        };
        
        var context = new FakeScanContext(module.Name, new ReadOnlyDictionary<string, string>(options));
        var pipeline = new ScanPipeline([module]);

        var results = await pipeline.ExecuteAsync(context, CancellationToken.None);

        Assert.NotNull(results);
        Assert.Single(results);
        var result = results[0];
        Assert.True(result.Status is ScanStatus.Success or ScanStatus.PartialSuccess);
        Assert.Equal(module.Name, result.ModuleName);
        Assert.NotNull(result.Findings);
    }
}
