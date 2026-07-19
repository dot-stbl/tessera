using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaTraces implementation of <see cref="ITraceProvider" />.
///     Delegates all Jaeger → domain mapping to
///     <see cref="VictoriaTraceMapper" />; this class only handles I/O.
/// </summary>
public sealed class VictoriaTraceProvider(IVictoriaTracesClient client, VictoriaOptions options) : ITraceProvider
{
    /// <inheritdoc />
    public async Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery query, CancellationToken cancellationToken)
    {
        var response = await client.SearchTracesAsync(
            options.Tenant,
            service: query.Service,
            operation: query.Operation,
            tags: null,
            start: query.StartUnixMs,
            end: query.EndUnixMs,
            minDuration: VictoriaTraceMapper.FormatDuration(query.MinDurationMs),
            maxDuration: VictoriaTraceMapper.FormatDuration(query.MaxDurationMs),
            limit: query.Limit,
            cancellationToken);

        return VictoriaTraceMapper.ToSummaryPage(response.Data);
    }

    /// <inheritdoc />
    public async Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken cancellationToken)
    {
        var response = await client.GetTraceAsync(options.Tenant, traceId.Value, cancellationToken);
        return response.Data.Count == 0 ? null : VictoriaTraceMapper.ToDetail(response.Data[0]);
    }
}