using Microsoft.Extensions.Logging;
using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Providers.Discovery;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaTraces + VictoriaLogs implementation of <see cref="IDiscoveryProvider" />.
/// </summary>
public sealed class VictoriaDiscoveryProvider(
    IVictoriaTracesClient tracesClient,
    IVictoriaLogsClient logsClient,
    ILogger<VictoriaDiscoveryProvider> logger) : IDiscoveryProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken cancellationToken)
    {
        var traceServices = await VictoriaDiscoveryMapper.TryListTraceServicesAsync(
            tracesClient, logger, cancellationToken);
        var logStreams = await VictoriaDiscoveryMapper.TryListLogStreamsAsync(
            logsClient, logger, cancellationToken);

        return VictoriaDiscoveryMapper.ToServiceList(traceServices, logStreams);
    }
}
