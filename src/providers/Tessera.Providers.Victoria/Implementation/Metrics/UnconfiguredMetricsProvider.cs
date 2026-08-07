using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Providers.Metrics;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation.Metrics;

/// <summary>
///     Placeholder <see cref="IMetricsProvider" /> when VictoriaMetrics URL is
///     not configured. Surfaces a typed <see cref="ProviderException" /> so
///     Discovery RED can degrade without a null DI registration.
/// </summary>
public sealed class UnconfiguredMetricsProvider : IMetricsProvider
{
    private const string Code = "provider.metrics_not_configured";
    private const string Message = "metrics backend not configured";

    /// <inheritdoc />
    public Task<MetricMatrix> QueryRangeAsync(
        MetricsRangeQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new ProviderException(Code, Message);
    }

    /// <inheritdoc />
    public Task<MetricVector> QueryInstantAsync(
        MetricsInstantQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new ProviderException(Code, Message);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> LabelValuesAsync(
        string label,
        TimeRange range,
        CancellationToken cancellationToken = default)
    {
        throw new ProviderException(Code, Message);
    }
}
