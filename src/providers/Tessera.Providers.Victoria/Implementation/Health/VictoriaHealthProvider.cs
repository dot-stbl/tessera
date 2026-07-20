using Tessera.Shared.Kernel.Providers.Health;

namespace Tessera.Providers.Victoria.Implementation.Health;

/// <summary>
///     Composite <see cref="IHealthProvider" /> for VictoriaTraces + VictoriaLogs.
///     Aggregates per-backend probe results into a single composite status:
///     Healthy when both are healthy, Unhealthy when both are unreachable,
///     Degraded otherwise.
/// </summary>
public sealed class VictoriaHealthProvider : IHealthProvider
{

    /// <inheritdoc />
    public async Task<ProviderHealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        var tracesTask = VictoriaHealthProbeHelpers.ProbeTracesAsync(cancellationToken);
        var logsTask = VictoriaHealthProbeHelpers.ProbeLogsAsync(cancellationToken);
        await Task.WhenAll(tracesTask, logsTask);

        var traces = await tracesTask;
        var logs = await logsTask;

        var status = (traces.Status, logs.Status) switch
        {
            (HealthStatus.Healthy, HealthStatus.Healthy) => HealthStatus.Healthy,
            (HealthStatus.Unhealthy, HealthStatus.Unhealthy) => HealthStatus.Unhealthy,
            _ => HealthStatus.Degraded,
        };

        return new ProviderHealthReport(
            Provider: "victoria",
            Status: status,
            Detail: $"traces={traces.Status}; logs={logs.Status}");
    }
}
