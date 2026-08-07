using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Shared.Kernel.Domain.Metrics.Results;
using Tessera.Shared.Kernel.Providers.Metrics;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation.Metrics;

/// <summary>
///     VictoriaMetrics implementation of <see cref="IMetricsProvider" />.
///     Thin transport + mapping; RED assembly lives above the provider.
/// </summary>
public sealed class VictoriaMetricsProvider(
    IVictoriaMetricsClient client,
    VictoriaOptions options) : IMetricsProvider
{
    /// <inheritdoc />
    public async Task<MetricMatrix> QueryRangeAsync(
        MetricsRangeQuery query,
        CancellationToken cancellationToken = default)
    {
        var response = await client.QueryRangeAsync(
            options.Tenant,
            query.Query,
            VictoriaMetricsMapper.FormatUnixSeconds(query.StartUnixMs),
            VictoriaMetricsMapper.FormatUnixSeconds(query.EndUnixMs),
            query.StepSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken);

        return VictoriaMetricsMapper.ToMatrix(response);
    }

    /// <inheritdoc />
    public async Task<MetricVector> QueryInstantAsync(
        MetricsInstantQuery query,
        CancellationToken cancellationToken = default)
    {
        var response = await client.QueryAsync(
            options.Tenant,
            query.Query,
            VictoriaMetricsMapper.FormatUnixSeconds(query.TimeUnixMs),
            cancellationToken);

        return VictoriaMetricsMapper.ToVector(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LabelValuesAsync(
        string label,
        TimeRange range,
        CancellationToken cancellationToken = default)
    {
        var response = await client.LabelValuesAsync(
            options.Tenant,
            label,
            VictoriaMetricsMapper.FormatUnixSeconds(range.StartUnixMs),
            VictoriaMetricsMapper.FormatUnixSeconds(range.EndUnixMs),
            cancellationToken);

        return VictoriaMetricsMapper.ToLabelValues(response);
    }
}
