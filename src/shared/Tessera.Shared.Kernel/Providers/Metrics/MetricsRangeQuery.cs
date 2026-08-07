namespace Tessera.Shared.Kernel.Providers.Metrics;

/// <summary>
///     Parameters for a PromQL range query (<c>/query_range</c>).
/// </summary>
/// <param name="Query">PromQL expression.</param>
/// <param name="StartUnixMs">Range start, UTC unix milliseconds.</param>
/// <param name="EndUnixMs">Range end, UTC unix milliseconds.</param>
/// <param name="StepSeconds">Evaluation step in seconds.</param>
public sealed record MetricsRangeQuery(
    string Query,
    long StartUnixMs,
    long EndUnixMs,
    int StepSeconds);
