namespace Tessera.Shared.Kernel.Analysis.Red;

/// <summary>
///     Origin of a <see cref="RedSnapshot" /> — PromQL metrics or a light
///     approximation from discovery span counters (ADR-0002 §4).
/// </summary>
public enum RedSource
{
    /// <summary>Computed from OTel HTTP metrics via PromQL.</summary>
    Metrics = 0,

    /// <summary>
    ///     Approximated from <c>ServiceSummary</c> span/error counts when the
    ///     metrics backend is missing or fails. No p95; rate may be null.
    /// </summary>
    SpanApprox = 1,
}
