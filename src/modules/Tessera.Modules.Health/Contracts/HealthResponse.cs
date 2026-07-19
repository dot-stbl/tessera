using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Modules.Health.Contracts;

/// <summary>
///     Wire shape of the composite health endpoint response. Serialized to JSON
///     by the minimal-API pipeline; <see cref="Status" /> is emitted as the enum
///     name (<c>"Healthy"</c>, <c>"Degraded"</c>, <c>"Unhealthy"</c>) by the
///     default <c>JsonStringEnumConverter</c>.
/// </summary>
public sealed record HealthResponse
{
    /// <summary>Provider name (e.g. <c>"victoria"</c>).</summary>
    public required string Provider { get; init; }

    /// <summary>Aggregate health status across all probed backends.</summary>
    public required HealthStatus Status { get; init; }

    /// <summary>Optional per-backend detail (e.g. <c>"traces=Healthy; logs=Unhealthy"</c>).</summary>
    public string? Detail { get; init; }
}