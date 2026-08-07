using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Shared.Kernel.Providers.Metrics;

/// <summary>
///     Abstraction over a metrics backend (VictoriaMetrics, Prometheus, …).
///     Modules depend on this interface; concrete providers live in
///     <c>Tessera.Providers.&lt;Name&gt;</c>. Pure contract — no HTTP types.
/// </summary>
public interface IMetricsProvider
{
    /// <summary>
    ///     Evaluate a PromQL range query and return a matrix of series.
    /// </summary>
    public Task<MetricMatrix> QueryRangeAsync(
        MetricsRangeQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Evaluate a PromQL instant query. Result is a vector (series with one
    ///     sample each) or a scalar depending on the expression; callers that
    ///     need a scalar use expressions that evaluate to one.
    /// </summary>
    public Task<MetricVector> QueryInstantAsync(
        MetricsInstantQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     List distinct values for a label name within the given time range
    ///     (<c>/label/{name}/values</c>).
    /// </summary>
    public Task<IReadOnlyList<string>> LabelValuesAsync(
        string label,
        TimeRange range,
        CancellationToken cancellationToken = default);
}
