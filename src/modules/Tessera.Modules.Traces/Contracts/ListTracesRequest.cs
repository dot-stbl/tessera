using Tessera.Shared.Kernel.Providers.Traces;

namespace Tessera.Modules.Traces.Contracts;

/// <summary>
///     Query string parameters for <c>GET /api/traces</c>. Bound via
///     <see cref="Microsoft.AspNetCore.Http.AsParametersAttribute" /> —
///     the minimal-API binder fills each public property from a same-named
///     query-string segment. <see cref="ToTraceSearchQuery" /> converts to the
///     provider-level query type.
/// </summary>
public sealed class ListTracesRequest
{
    /// <summary>Filter by service name (substring match).</summary>
    public string? Service { get; init; }

    /// <summary>Filter by operation name (substring match).</summary>
    public string? Operation { get; init; }

    /// <summary>Start of the time range, inclusive (UTC unix milliseconds).</summary>
    public long StartUnixMs { get; init; }

    /// <summary>End of the time range, inclusive (UTC unix milliseconds).</summary>
    public long EndUnixMs { get; init; }

    /// <summary>Minimum span duration in milliseconds.</summary>
    public int? MinDurationMs { get; init; }

    /// <summary>Maximum span duration in milliseconds.</summary>
    public int? MaxDurationMs { get; init; }

    /// <summary>Pagination cursor from a previous response; null for first page.</summary>
    public string? Cursor { get; init; }

    /// <summary>Max items per page (default 50, max 200).</summary>
    public int? Limit { get; init; } = 50;

    /// <summary>
    ///     Project this request into a <see cref="TraceSearchQuery" /> the
    ///     <see cref="Tessera.Shared.Kernel.Providers.Traces.ITraceProvider" />
    ///     can consume.
    /// </summary>
    public TraceSearchQuery ToTraceSearchQuery()
    {
        return new TraceSearchQuery(
            Service,
            Operation,
            StartUnixMs,
            EndUnixMs,
            MinDurationMs,
            MaxDurationMs,
            Cursor,
            Limit);
    }
}
