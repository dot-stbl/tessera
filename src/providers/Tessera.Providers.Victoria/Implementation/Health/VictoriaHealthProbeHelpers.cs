using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Providers.Victoria.Implementation.Health;

/// <summary>
///     Probe helpers for the Victoria health provider.
///     All methods are <c>internal static</c> so they're not bound to an
///     instance and can be invoked in tests without DI bootstrap.
/// </summary>
internal static class VictoriaHealthProbeHelpers
{
    /// <summary>
    ///     Probe VictoriaTraces <c>/health</c> endpoint. Returns a
    ///     <see cref="ProviderHealthReport" /> with the per-provider name
    ///     and status. MVP-01 placeholder returns Healthy unconditionally —
    ///     the real implementation will wire into <c>IVictoriaTracesClient</c>
    ///     via a GET /health endpoint.
    /// </summary>
    public static Task<ProviderHealthReport> ProbeTracesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderHealthReport("victoria-traces", HealthStatus.Healthy));
    }

    /// <summary>
    ///     Probe VictoriaLogs <c>/health</c> endpoint. Returns a
    ///     <see cref="ProviderHealthReport" /> with the per-provider name
    ///     and status. MVP-01 placeholder returns Healthy unconditionally.
    /// </summary>
    public static Task<ProviderHealthReport> ProbeLogsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ProviderHealthReport("victoria-logs", HealthStatus.Healthy));
    }
}