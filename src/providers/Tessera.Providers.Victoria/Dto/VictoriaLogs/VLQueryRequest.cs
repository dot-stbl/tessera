namespace Tessera.Providers.Victoria.Dto.VictoriaLogs;

/// <summary>
///     Parameters for a VictoriaLogs LogsQL query. Mapped to
///     <c>GET /select/{tenant}/logsql/query</c>.
/// </summary>
/// <param name="Query">LogsQL expression (e.g. <c>_trace_id:"abc123"</c>).</param>
/// <param name="Limit">Max log entries (default 500, max 1000 per query).</param>
/// <param name="Offset">Skip entries; requires <paramref name="Limit" /> to also be set.</param>
/// <param name="Start">RFC3339 / ISO8601 start time (inclusive).</param>
/// <param name="End">RFC3339 / ISO8601 end time (exclusive).</param>
public sealed record VLQueryRequest(
    string Query,
    int? Limit = 500,
    int? Offset = null,
    string? Start = null,
    string? End = null);
