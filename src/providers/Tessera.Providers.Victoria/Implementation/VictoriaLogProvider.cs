using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaLogs implementation of <see cref="ILogProvider" />.
///     Delegates LogsQL query construction + NDJSON parsing to
///     <see cref="VictoriaLogMapper" />; this class only handles I/O.
/// </summary>
public sealed class VictoriaLogProvider(IVictoriaLogsClient client, VictoriaOptions options) : ILogProvider
{
    /// <inheritdoc />
    public async Task<Page<LogEntry>> QueryAsync(LogQuery query, CancellationToken cancellationToken)
    {
        var logsql = VictoriaLogMapper.BuildLogsQuery(query);

        using var response = await client.QueryAsync(
            options.Tenant,
            logsql,
            query.Limit,
            query.StartUnixMs is null ? null : VictoriaLogMapper.ToIso8601(query.StartUnixMs.Value),
            query.EndUnixMs is null ? null : VictoriaLogMapper.ToIso8601(query.EndUnixMs.Value),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return VictoriaLogMapper.AsPage(VictoriaLogMapper.ParseNdjson(body, query.TraceId));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogEntry>> ListByTraceAsync(
        TraceId traceId,
        TimeRange range,
        CancellationToken cancellationToken)
    {
        var query = new LogQuery(traceId, null, range.StartUnixMs, range.EndUnixMs, null, 500);
        var page = await QueryAsync(query, cancellationToken);
        return page.Items;
    }
}
