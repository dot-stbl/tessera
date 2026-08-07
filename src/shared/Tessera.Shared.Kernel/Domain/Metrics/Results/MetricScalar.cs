using Tessera.Shared.Kernel.Domain.Metrics.Samples;

namespace Tessera.Shared.Kernel.Domain.Metrics.Results;

/// <summary>
///     PromQL scalar result — a single unlabelled sample.
/// </summary>
/// <param name="Sample">The scalar sample.</param>
public sealed record MetricScalar(MetricSample Sample);
