namespace Tessera.Shared.Kernel.Providers.Health;

/// <summary>
///     Health probe result for a single provider.
/// </summary>
/// <param name="Provider">Provider name (e.g. <c>"victoria-traces"</c>).</param>
/// <param name="Status">Overall health status.</param>
/// <param name="Detail">Optional human-readable detail (e.g. error message from last probe).</param>
public sealed record ProviderHealthReport(
    string Provider,
    HealthStatus Status,
    string? Detail = null);
