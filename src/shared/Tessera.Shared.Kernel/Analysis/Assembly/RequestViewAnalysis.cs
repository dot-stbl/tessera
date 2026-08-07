using Tessera.Shared.Kernel.Analysis.Errors;
using Tessera.Shared.Kernel.Domain.Logs;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;

namespace Tessera.Shared.Kernel.Analysis.Assembly;

/// <summary>
///     Pure request-view derivation: classify mode, build timeline markers,
///     group logs by span, count errored spans. No I/O.
/// </summary>
public static class RequestViewAnalysis
{
    /// <summary>
    ///     Classify the degradable union of optional trace spans + logs.
    /// </summary>
    public static RequestViewMode Classify(TraceDetail? trace, IReadOnlyList<LogEntry> logs)
    {
        var hasSpans = trace is { Spans.Count: > 0 };
        var hasLogs = logs.Count > 0;

        if (hasSpans && hasLogs)
        {
            return RequestViewMode.Full;
        }

        if (hasSpans)
        {
            return RequestViewMode.SpansOnly;
        }

        if (hasLogs)
        {
            return RequestViewMode.LogsOnly;
        }

        return RequestViewMode.Empty;
    }

    /// <summary>
    ///     Build the domain <see cref="RequestView" /> (mode + markers).
    /// </summary>
    public static RequestView Build(TraceDetail? trace, IReadOnlyList<LogEntry> logs)
    {
        var mode = Classify(trace, logs);
        var markers = BuildMarkers(trace, logs);
        return new RequestView(trace, logs, mode, markers);
    }

    /// <summary>
    ///     Build timeline markers for each log. Offset rules (documented on
    ///     <see cref="LogMarker" />): span start when the log's span_id matches
    ///     a span on the trace; else trace start; else earliest log timestamp
    ///     when there is no trace (logs-only).
    /// </summary>
    public static IReadOnlyList<LogMarker> BuildMarkers(TraceDetail? trace, IReadOnlyList<LogEntry> logs)
    {
        if (logs.Count == 0)
        {
            return [];
        }

        Dictionary<string, long>? spanStarts = null;
        if (trace is { Spans.Count: > 0 })
        {
            spanStarts = new Dictionary<string, long>(trace.Spans.Count, StringComparer.Ordinal);
            foreach (var span in trace.Spans)
            {
                spanStarts[span.SpanId.Value] = span.StartTime;
            }
        }

        var traceStart = trace?.StartTime;
        var fallbackOrigin = logs.Min(static log => log.Timestamp);

        var markers = new List<LogMarker>(logs.Count);
        foreach (var log in logs)
        {
            SpanId? markerSpanId = null;
            long anchorStart;

            if (log.SpanId is { } spanId
                && spanStarts is not null
                && spanStarts.TryGetValue(spanId.Value, out var spanStart))
            {
                markerSpanId = spanId;
                anchorStart = spanStart;
            }
            else
            {
                anchorStart = traceStart is { } start ? start : fallbackOrigin;
            }

            markers.Add(new LogMarker(
                markerSpanId,
                OffsetMs: log.Timestamp - anchorStart,
                log.Level,
                log.Message,
                TimestampUnixMs: log.Timestamp));
        }

        return markers;
    }

    /// <summary>
    ///     Group logs that carry a <see cref="LogEntry.SpanId" /> by that id.
    ///     Logs without a span id are omitted (trace-level only).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<LogEntry>> GroupLogsBySpan(
        IReadOnlyList<LogEntry> logs)
    {
        if (logs.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<LogEntry>>(StringComparer.Ordinal);
        }

        var buckets = new Dictionary<string, List<LogEntry>>(StringComparer.Ordinal);
        foreach (var log in logs)
        {
            if (log.SpanId is not { } spanId)
            {
                continue;
            }

            if (!buckets.TryGetValue(spanId.Value, out var bucket))
            {
                bucket = [];
                buckets[spanId.Value] = bucket;
            }

            bucket.Add(log);
        }

        var result = new Dictionary<string, IReadOnlyList<LogEntry>>(buckets.Count, StringComparer.Ordinal);
        foreach (var (key, value) in buckets)
        {
            result[key] = value;
        }

        return result;
    }

    /// <summary>
    ///     Count spans with <see cref="TraceStatus.Error" /> (badge input for P7).
    ///     Delegates to <see cref="ErrorAnalysis.CountErroredSpans" />.
    /// </summary>
    public static int CountErroredSpans(TraceDetail? trace)
    {
        return ErrorAnalysis.CountErroredSpans(trace);
    }
}
