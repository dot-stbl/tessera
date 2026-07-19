using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Services;
using Tessera.Shared.Kernel.Providers.Discovery;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaTraces + VictoriaLogs implementation of <see cref="IDiscoveryProvider" />.
///     Delegates all transforms + per-backend I/O to
///     <see cref="VictoriaDiscoveryMapper" />; this class is purely the
///     per-request coordination of the two backend queries.
/// </summary>
public sealed class VictoriaDiscoveryProvider(
    IVictoriaTracesClient tracesClient,
    IVictoriaLogsClient logsClient,
    VictoriaOptions options) : IDiscoveryProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken cancellationToken)
    {
        var traceServices = await VictoriaDiscoveryMapper.TryListTraceServicesAsync(tracesClient, options.Tenant, cancellationToken);
        var logStreams = await VictoriaDiscoveryMapper.TryListLogStreamsAsync(logsClient, options.Tenant, cancellationToken);

        return VictoriaDiscoveryMapper.ToServiceList(traceServices, logStreams);
    }
}
