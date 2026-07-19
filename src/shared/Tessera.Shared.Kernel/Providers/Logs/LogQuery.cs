
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Shared.Kernel.Providers.Logs;
/// <summary>
/// Parameters for log query. Maps to GET /api/logs query string.
/// </summary>
/// <param name="TraceId">Filter logs by trace correlation (alternative to <paramref name="Filter"/>).</param>
/// <param name="Stream">Filter by VictoriaLogs stream / service name.</param>
/// <param name="StartUnixMs">Start of time range, inclusive.</param>
/// <param name="EndUnixMs">End of time range, inclusive.</param>
/// <param name="Filter">Backend-native filter expression (e.g. LogsQL). Overrides <paramref name="TraceId"/>.</param>
/// <param name="Limit">Max items per page (default 500, max 1000).</param>
public sealed record LogQuery(
    TraceId? TraceId,
    string? Stream,
    long? StartUnixMs,
    long? EndUnixMs,
    string? Filter,
    int? Limit = 500);