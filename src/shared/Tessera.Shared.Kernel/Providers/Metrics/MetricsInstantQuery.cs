namespace Tessera.Shared.Kernel.Providers.Metrics;

/// <summary>
///     Parameters for a PromQL instant query (<c>/query</c>).
/// </summary>
/// <param name="Query">PromQL expression.</param>
/// <param name="TimeUnixMs">Evaluation timestamp, UTC unix milliseconds.</param>
public sealed record MetricsInstantQuery(string Query, long TimeUnixMs);
