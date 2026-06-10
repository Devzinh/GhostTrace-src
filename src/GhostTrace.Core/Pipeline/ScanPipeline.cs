using GhostTrace.Core.Abstractions;

namespace GhostTrace.Core.Pipeline;

/// <summary>
/// Sequentially executes a set of <see cref="IScanModule"/> instances
/// against a shared <see cref="IScanContext"/> and collects their results.
/// <para>
/// This is a minimal orchestrator with no I/O, no parallelism, and no
/// infrastructure dependencies. Compatible with constructor-based DI
/// via <c>IEnumerable&lt;IScanModule&gt;</c>.
/// </para>
/// </summary>
public sealed class ScanPipeline
{
    private readonly IReadOnlyList<IScanModule> _modules;

    /// <summary>
    /// Initialises the pipeline with the modules to execute.
    /// </summary>
    /// <param name="modules">
    /// Ordered sequence of scan modules. Execution follows this order.
    /// Must not be <c>null</c> or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modules"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="modules"/> contains no elements.
    /// </exception>
    public ScanPipeline(IEnumerable<IScanModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        _modules = modules as IReadOnlyList<IScanModule>
                   ?? modules.ToList().AsReadOnly();

        if (_modules.Count == 0)
            throw new ArgumentException("At least one scan module is required.", nameof(modules));
    }

    /// <summary>
    /// Executes every registered module sequentially and returns their results.
    /// </summary>
    /// <param name="context">
    /// Shared execution context for all modules.
    /// </param>
    /// <param name="cancellationToken">
    /// Cooperative cancellation token. When triggered, no further modules
    /// are started and an <see cref="OperationCanceledException"/> is thrown.
    /// Results collected before cancellation are not returned.
    /// </param>
    /// <returns>
    /// A read-only list of <see cref="IScanResult"/>, one per executed module,
    /// in execution order.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Cancellation was requested via <paramref name="cancellationToken"/>.
    /// </exception>
    public async Task<IReadOnlyList<IScanResult>> ExecuteAsync(
        IScanContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var results = new List<IScanResult>(_modules.Count);

        foreach (var module in _modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await module.RunAsync(context, cancellationToken)
                                     .ConfigureAwait(false);
            results.Add(result);
        }

        return results.AsReadOnly();
    }
}
