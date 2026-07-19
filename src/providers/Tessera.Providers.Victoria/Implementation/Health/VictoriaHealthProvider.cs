using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Providers.Victoria.Implementation.Health;

/// <summary>
///     Composite <see cref="IHealthProvider" /> for VictoriaTraces + VictoriaLogs.
///     Probes each backend's <c>/health</c> endpoint and aggregates per-provider
///     status. Returns <see cref="HealthStatus.Degraded" /> if either backend is
///     reachable but reporting issues, <see cref="HealthStatus.Unhealthy" /> if both
///     are unreachable.
/// </summary>
public sealed class VictoriaHealthProvider : IHealthProvider
{
    /// <inheritdoc />
    public async Task<ProviderHealthReport> CheckAsync(CancellationToken ct)
    {
        var tracesTask = ProbeTracesAsync(ct);
        var logsTask = ProbeLogsAsync(ct);
        await Task.WhenAll(tracesTask, logsTask);

        var traces = await tracesTask;
        var logs = await logsTask;

        var status = (traces.Status, logs.Status) switch
        {
            (HealthStatus.Healthy, HealthStatus.Healthy) => HealthStatus.Healthy,
            (HealthStatus.Unhealthy, HealthStatus.Unhealthy) => HealthStatus.Unhealthy,
            _ => HealthStatus.Degraded,
        };

        var detail = $"traces={traces.Status}; logs={logs.Status}";
        return new ProviderHealthReport(Provider: "victoria", Status: status, Detail: detail);
    }

    // Placeholder probes — for MVP-01 both return Healthy. Real impl
    // wires into the Refit clients via a GET /health endpoint.
    private static async Task<ProviderHealthReport> ProbeTracesAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new ProviderHealthReport("victoria-traces", HealthStatus.Healthy);
    }

    private static async Task<ProviderHealthReport> ProbeLogsAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new ProviderHealthReport("victoria-logs", HealthStatus.Healthy);
    }
}