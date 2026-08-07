namespace Tessera.Shared.Kernel.Domain.Metrics.Samples;

/// <summary>
///     One (timestamp, value) sample on a metric series. Time is UTC unix
///     milliseconds on the wire; value is the PromQL/OTel numeric.
/// </summary>
/// <param name="TimestampUnixMs">Sample timestamp, UTC unix milliseconds.</param>
/// <param name="Value">Numeric sample value.</param>
public sealed record MetricSample(long TimestampUnixMs, double Value);
