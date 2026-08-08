using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaTraces implementation of <see cref="ITraceProvider" />.
/// </summary>
public sealed class VictoriaTraceProvider(IVictoriaTracesClient client) : ITraceProvider
{
    /// <inheritdoc />
    public async Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery query, CancellationToken cancellationToken)
    {
        var windowMs = Math.Max(0, query.EndUnixMs - query.StartUnixMs);
        var lookbackHours = Math.Clamp((int)Math.Ceiling(windowMs / 3_600_000.0), 1, 168);
        var lookback = lookbackHours + "h";

        var response = await client.SearchTracesAsync(
            service: query.Service,
            operation: query.Operation,
            tags: null,
            start: null,
            end: null,
            lookback: lookback,
            minDuration: VictoriaTraceMapper.FormatDuration(query.MinDurationMs),
            maxDuration: VictoriaTraceMapper.FormatDuration(query.MaxDurationMs),
            limit: query.Limit,
            cancellationToken);

        return VictoriaTraceMapper.ToSummaryPage(response.Data);
    }

    /// <inheritdoc />
    public async Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken cancellationToken)
    {
        var response = await client.GetTraceAsync(traceId.Value, cancellationToken);
        return response.Data.Count == 0 ? null : VictoriaTraceMapper.ToDetail(response.Data[0]);
    }
}
