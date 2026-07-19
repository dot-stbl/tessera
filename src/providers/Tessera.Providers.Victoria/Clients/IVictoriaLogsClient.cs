using Refit;
using Tessera.Providers.Victoria.Dto.VictoriaLogs;

namespace Tessera.Providers.Victoria.Clients;

/// <summary>
///     Refit client for VictoriaLogs LogsQL HTTP API.
///     Single-tenant path (<c>{tenant}</c>, MVP value <c>"0"</c>).
/// </summary>
public interface IVictoriaLogsClient
{
    /// <summary>
    ///     <c>GET /select/{tenant}/logsql/query</c> — execute a LogsQL query.
    ///     Returns the raw NDJSON response body as a string; the consumer
    ///     splits on newlines and deserializes each line as
    ///     <see cref="VLLogEntry" />.
    /// </summary>
    [Get("/select/{tenant}/logsql/query")]
    public Task<HttpResponseMessage> QueryAsync(
        string tenant,
        [Query] string query,
        [Query] int? limit,
        [Query] string? start,
        [Query] string? end,
        CancellationToken cancellationToken);
}
