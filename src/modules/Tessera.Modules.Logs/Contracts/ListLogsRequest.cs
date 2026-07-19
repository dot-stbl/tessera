using Tessera.Shared.Kernel.Providers.Logs;

namespace Tessera.Modules.Logs.Contracts;

/// <summary>
///     Query string parameters for <c>GET /api/logs</c>. MVP-01 only supports
///     the trace-id correlation lookup — ad-hoc LogsQL is deferred to MVP-02.
/// </summary>
public sealed class ListLogsRequest
{
    /// <summary>Filter logs by trace correlation id (required for MVP-01).</summary>
    public string? TraceId { get; init; }

    /// <summary>Filter by VictoriaLogs stream / service name.</summary>
    public string? Stream { get; init; }

    /// <summary>Start of the time range, inclusive (UTC unix milliseconds).</summary>
    public long StartUnixMs { get; init; }

    /// <summary>End of the time range, inclusive (UTC unix milliseconds).</summary>
    public long EndUnixMs { get; init; }

    /// <summary>Max items per page (default 500, max 1000).</summary>
    public int? Limit { get; init; } = 500;

    /// <summary>
    ///     Project this request into a <see cref="LogQuery" /> the
    ///     <see cref="ILogProvider" /> can consume. Returns null when
    ///     <see cref="TraceId" /> is missing — MVP-01 requires a trace id.
    /// </summary>
    public LogQuery? ToLogQuery()
    {
        var parsedTraceId = TraceId is null
            ? null
            : new Tessera.Shared.Kernel.Identifiers.TraceId(TraceId);
        return new LogQuery(parsedTraceId, Stream, StartUnixMs, EndUnixMs, null, Limit);
    }
}