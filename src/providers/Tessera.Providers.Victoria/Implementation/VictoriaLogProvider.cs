using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Implementation.Mapping;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Exceptions;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaLogs implementation of <see cref="ILogProvider" />.
/// </summary>
public sealed class VictoriaLogProvider(IVictoriaLogsClient client) : ILogProvider
{
    /// <inheritdoc />
    public async Task<Page<LogEntry>> QueryAsync(LogQuery query, CancellationToken cancellationToken)
    {
        var logsql = VictoriaLogMapper.BuildLogsQuery(query);

        HttpResponseMessage response;
        try
        {
            response = await client.QueryAsync(
                logsql,
                query.Limit,
                query.StartUnixMs is null ? null : VictoriaLogMapper.ToIso8601(query.StartUnixMs.Value),
                query.EndUnixMs is null ? null : VictoriaLogMapper.ToIso8601(query.EndUnixMs.Value),
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(
                code: "provider.network_error",
                message: $"Victoria logs query failed: {ex.Message}",
                inner: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderException(
                    code: "provider.network_error",
                    message: $"Victoria logs query returned {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return VictoriaLogMapper.AsPage(VictoriaLogMapper.ParseNdjson(body, query.TraceId));
        }
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
