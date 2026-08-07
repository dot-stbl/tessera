using Tessera.Shared.Kernel.Domain.Metrics.Samples;

namespace Tessera.Shared.Kernel.Domain.Metrics.Series;

/// <summary>
///     A labelled time series of metric samples (PromQL matrix/vector row).
/// </summary>
/// <param name="Labels">Label set identifying the series (metric name + dims).</param>
/// <param name="Samples">Ordered samples (instant vector has exactly one).</param>
public sealed record MetricSeries(
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<MetricSample> Samples);
