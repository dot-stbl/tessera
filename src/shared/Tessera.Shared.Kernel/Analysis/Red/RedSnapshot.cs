namespace Tessera.Shared.Kernel.Analysis.Red;

/// <summary>
///     Rate / Errors / Duration snapshot for a service (and optional operation).
///     Null fields mean "unknown" — e.g. SpanApprox has no p95 and often no rate.
/// </summary>
/// <param name="RequestRatePerSec">Requests per second (PromQL rate), or null.</param>
/// <param name="ErrorRatio">5xx share in [0, 1], or null when total is zero/unknown.</param>
/// <param name="DurationP95Ms">p95 latency in milliseconds, or null when unavailable.</param>
/// <param name="Source">Whether values came from metrics or span counters.</param>
public sealed record RedSnapshot(
    double? RequestRatePerSec,
    double? ErrorRatio,
    double? DurationP95Ms,
    RedSource Source);
