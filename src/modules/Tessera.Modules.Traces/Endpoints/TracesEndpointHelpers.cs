using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Providers.Logs;
using Tessera.Shared.Kernel.Time;

namespace Tessera.Modules.Traces.Endpoints;

/// <summary>
///     Pure transformation helpers used by <see cref="Controllers.TracesController" />
///     when fetching correlated logs for a trace. Lives outside the mapper
///     because it shapes provider queries (not DTOs) — no <c>[Mapper]</c>
///     involvement.
/// </summary>
internal static class TracesEndpointHelpers
{
    /// <summary>
    ///     Time padding around a trace's own window when querying for correlated
    ///     logs. Async log emissions near the trace boundaries should still
    ///     appear in the trace-detail panel.
    /// </summary>
    private static readonly TimeSpan LogCorrelationPadding = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Build the log-query time range for a given trace, padding
    ///     <see cref="LogCorrelationPadding" /> on both sides so async log
    ///     emissions near the trace boundaries aren't missed.
    /// </summary>
    public static TimeRange ToLogCorrelationRange(TraceDetail trace)
    {
        var paddingMs = (long)LogCorrelationPadding.TotalMilliseconds;
        return new TimeRange(
            trace.StartTime - paddingMs,
            trace.StartTime + trace.DurationMs + paddingMs);
    }

    /// <summary>
    ///     Build the <see cref="LogQuery" /> that <see cref="ILogProvider" />
    ///     consumes for correlated-log lookups by trace id.
    /// </summary>
    public static LogQuery ToLogQuery(TraceId traceId, TimeRange range)
    {
        return new LogQuery(traceId, null, range.StartUnixMs, range.EndUnixMs, null, Limit: 500);
    }
}
