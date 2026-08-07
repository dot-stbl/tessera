using Tessera.Shared.Kernel.Domain.Metrics.Series;

namespace Tessera.Shared.Kernel.Domain.Metrics.Results;

/// <summary>
///     PromQL matrix result — multiple series, each with a range of samples.
/// </summary>
/// <param name="Series">Series rows returned by the query.</param>
public sealed record MetricMatrix(IReadOnlyList<MetricSeries> Series);
