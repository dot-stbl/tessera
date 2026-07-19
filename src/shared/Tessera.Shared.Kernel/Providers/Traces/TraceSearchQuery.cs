namespace Tessera.Shared.Kernel.Providers.Traces;

/// <summary>
/// Parameters for trace search. Maps to GET /api/traces query string.
/// </summary>
/// <param name="Service">Filter by service name (substring match).</param>
/// <param name="Operation">Filter by operation name (substring match).</param>
/// <param name="StartUnixMs">Start of time range, inclusive.</param>
/// <param name="EndUnixMs">End of time range, inclusive.</param>
/// <param name="MinDurationMs">Minimum span duration in milliseconds.</param>
/// <param name="MaxDurationMs">Maximum span duration in milliseconds.</param>
/// <param name="Cursor">Pagination cursor from a previous response; null for first page.</param>
/// <param name="Limit">Max items per page (default 50, max 200).</param>
public sealed record TraceSearchQuery(
    string? Service,
    string? Operation,
    long StartUnixMs,
    long EndUnixMs,
    int? MinDurationMs,
    int? MaxDurationMs,
    string? Cursor,
    int? Limit = 50);