namespace Tessera.Shared.Kernel.Providers.Health;

/// <summary>
/// Per-provider health status. <c>Tessera.Host</c> aggregates per-provider
/// reports into a composite endpoint response.
/// </summary>
public enum HealthStatus
{
    /// <summary>Provider is reachable and responding correctly.</summary>
    Healthy,

    /// <summary>Provider is reachable but reporting degraded state (e.g. partial data, slow responses).</summary>
    Degraded,

    /// <summary>Provider is unreachable or returning errors.</summary>
    Unhealthy,
}