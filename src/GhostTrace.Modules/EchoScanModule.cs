using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules;

/// <summary>
/// A deterministic, side-effect-free scan module used to exercise the pipeline and
/// downstream consumers without touching any external resource.
/// </summary>
public sealed class EchoScanModule : IScanModule
{
    /// <inheritdoc />
    public string Name => "EchoModule";

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name)
            .AddFinding(
                category: "Diagnostic",
                description: $"Echo module executed successfully for scan {context.ScanId}.",
                source: Name,
                timestampUtc: System.DateTimeOffset.UtcNow)
            .SetMetadata("ScanId", context.ScanId.ToString());

        return Task.FromResult(builder.Build());
    }
}
