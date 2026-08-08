using Refit;
using Tessera.Providers.Victoria.Dto.VictoriaLogs;

namespace Tessera.Providers.Victoria.Clients;

/// <summary>
///     Refit client for single-node VictoriaLogs LogsQL HTTP API
///     (<c>/select/logsql/query</c>, no tenant segment).
/// </summary>
public interface IVictoriaLogsClient
{
    /// <summary>
    ///     <c>GET /select/logsql/query</c> — NDJSON body as response stream.
    /// </summary>
    [Get("/select/logsql/query")]
    public Task<HttpResponseMessage> QueryAsync(
        [Query] string query,
        [Query] int? limit,
        [Query] string? start,
        [Query] string? end,
        CancellationToken cancellationToken);
}
