using Tessera.Shared.Kernel.Analysis.Red;

namespace Tessera.Modules.Discovery.Contracts;

/// <summary>
///     HTTP body for service RED. Null fields mean unknown for that source.
/// </summary>
public sealed record ServiceRedResponse
{
    /// <summary>Requests per second, or null when unknown.</summary>
    public double? RequestRatePerSec { get; init; }

    /// <summary>Error ratio in [0, 1], or null when unknown.</summary>
    public double? ErrorRatio { get; init; }

    /// <summary>p95 duration in milliseconds, or null when unknown.</summary>
    public double? DurationP95Ms { get; init; }

    /// <summary>Whether values came from PromQL metrics or span counters.</summary>
    public required RedSource Source { get; init; }
}
