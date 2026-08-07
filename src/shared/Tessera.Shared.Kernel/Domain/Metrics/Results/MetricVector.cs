using Tessera.Shared.Kernel.Domain.Metrics.Series;

namespace Tessera.Shared.Kernel.Domain.Metrics.Results;

/// <summary>
///     PromQL vector result — multiple series, each with a single sample.
/// </summary>
/// <param name="Series">Single-sample series rows.</param>
public sealed record MetricVector(IReadOnlyList<MetricSeries> Series);
