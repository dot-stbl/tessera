using Tessera.Providers.Victoria.Dto.Jaeger.Span;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;

using DomainSpan = Tessera.Shared.Kernel.Domain.Spans.Span;

namespace Tessera.Providers.Victoria.Implementation.Mapping;

/// <summary>
///     Pure mappings from Jaeger-shaped DTOs to domain
///     <see cref="TraceSummary" /> / <see cref="TraceDetail" /> / <see cref="DomainSpan" />.
///     All methods are <c>internal static</c> so they are top-level
///     (not private to a single class) and unit-testable in isolation
///     without requiring the surrounding provider's DI graph.
/// </summary>
internal static class VictoriaTraceMapper
{
    /// <summary>
    ///     Map a Jaeger trace envelope to a page of <see cref="TraceSummary" />.
    /// </summary>
    public static Page<TraceSummary> ToSummaryPage(IReadOnlyList<JaegerTrace> traces)
    {
        var items = new List<TraceSummary>(traces.Count);
        for (var i = 0; i < traces.Count; i++)
        {
            items.Add(ToSummary(traces[i]));
        }

        return new Page<TraceSummary>(items, Cursor: null, HasMore: false);
    }

    /// <summary>
    ///     Map a single Jaeger trace to a domain <see cref="TraceDetail" />
    ///     with reconstructed span tree and root service / operation derived
    ///     from the process map.
    /// </summary>
    public static TraceDetail ToDetail(JaegerTrace jaeger)
    {
        var root = FindRootSpan(jaeger);
        var rootService = ResolveRootService(root, jaeger);
        var rootOperation = root?.OperationName ?? "unknown";
        var spans = new List<DomainSpan>(jaeger.Spans.Count);

        for (var i = 0; i < jaeger.Spans.Count; i++)
        {
            spans.Add(ToDomainSpan(jaeger.Spans[i], jaeger.Processes));
        }

        return new TraceDetail(
            TraceId: new TraceId(jaeger.TraceID),
            RootService: rootService,
            RootOperation: rootOperation,
            StartTime: root?.StartTime ?? 0,
            DurationMs: root is not null ? root.Duration / 1000 : 0,
            Status: root is not null ? ToStatus(root) : TraceStatus.Unset,
            Spans: spans);
    }

    /// <summary>
    ///     Map a single Jaeger trace to a domain <see cref="TraceSummary" />.
    /// </summary>
    public static TraceSummary ToSummary(JaegerTrace trace)
    {
        var root = FindRootSpan(trace);
        var service = ResolveRootService(root, trace);
        var operation = root?.OperationName ?? "unknown";

        var services = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in trace.Processes.Values)
        {
            if (seen.Add(p.ServiceName))
            {
                services.Add(p.ServiceName);
            }
        }

        return new TraceSummary(
            TraceId: new TraceId(trace.TraceID),
            RootService: service,
            RootOperation: operation,
            StartTime: root?.StartTime ?? 0,
            DurationMs: root is not null ? root.Duration / 1000 : 0,
            Status: root is not null ? ToStatus(root) : TraceStatus.Unset,
            SpanCount: trace.Spans.Count,
            Services: services);
    }

    /// <summary>
    ///     Convert a Jaeger span and process map to a domain <see cref="DomainSpan" />.
    /// </summary>
    public static DomainSpan ToDomainSpan(
        JaegerSpan dto,
        IReadOnlyDictionary<string, JaegerProcess> processes)
    {
        SpanId? parentSpanId = null;
        for (var i = 0; i < dto.References.Count; i++)
        {
            if (dto.References[i].RefType == "CHILD_OF")
            {
                parentSpanId = new SpanId(dto.References[i].SpanID);
                break;
            }
        }

        var service = processes.TryGetValue(dto.ProcessID, out var p) ? p.ServiceName : "unknown";

        var events = new List<SpanEvent>(dto.Logs.Count);
        for (var i = 0; i < dto.Logs.Count; i++)
        {
            events.Add(ToSpanEvent(dto.Logs[i]));
        }

        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < dto.Tags.Count; i++)
        {
            tags[dto.Tags[i].Key] = dto.Tags[i].Value;
        }

        return new DomainSpan(
            SpanId: new SpanId(dto.SpanID),
            ParentSpanId: parentSpanId,
            Service: service,
            Operation: dto.OperationName,
            StartTime: dto.StartTime,
            DurationMs: dto.Duration / 1000,
            Status: ToStatus(dto),
            Tags: tags,
            Events: events);
    }

    /// <summary>
    ///     Convert a Jaeger log to a domain <see cref="SpanEvent" />.
    ///     If the log fields contain an "event" key, it is used as the event name;
    ///     otherwise the event is generically named "log".
    /// </summary>
    public static SpanEvent ToSpanEvent(JaegerLog log)
    {
        var name = "log";
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < log.Fields.Count; i++)
        {
            var field = log.Fields[i];
            attributes[field.Key] = field.Value;
            if (field.Key == "event")
            {
                name = field.Value;
            }
        }

        return new SpanEvent(Time: log.Timestamp, Name: name, Attributes: attributes);
    }

    /// <summary>
    ///     Derive <see cref="TraceStatus" /> from a span's tags by checking
    ///     <c>error</c> / <c>otel.status_code</c> keys.
    /// </summary>
    public static TraceStatus ToStatus(JaegerSpan span)
    {
        for (var i = 0; i < span.Tags.Count; i++)
        {
            var tag = span.Tags[i];
            if (tag.Key is "error" or "otel.status_code")
            {
                return tag.Value switch
                {
                    "ERROR" or "true" or "2" => TraceStatus.Error,
                    "OK" or "false" or "1" => TraceStatus.Ok,
                    _ => TraceStatus.Unset,
                };
            }
        }

        return TraceStatus.Unset;
    }

    /// <summary>
    ///     Locate the root span in a trace (no parent reference).
    ///     Falls back to the first span if no unambiguous root is found.
    /// </summary>
    public static JaegerSpan? FindRootSpan(JaegerTrace trace)
    {
        for (var i = 0; i < trace.Spans.Count; i++)
        {
            if (trace.Spans[i].References.Count == 0)
            {
                return trace.Spans[i];
            }
        }

        return trace.Spans.Count > 0 ? trace.Spans[0] : null;
    }

    /// <summary>
    ///     Resolve the service name for a trace root via the process map.
    ///     Falls back to the first process in the map, then to "unknown".
    /// </summary>
    public static string ResolveRootService(JaegerSpan? root, JaegerTrace jaeger)
    {
        if (root is not null && jaeger.Processes.TryGetValue(root.ProcessID, out var rp))
        {
            return rp.ServiceName;
        }

        if (jaeger.Processes.Count > 0)
        {
            return jaeger.Processes.Values.First().ServiceName;
        }

        return "unknown";
    }

    /// <summary>
    ///     Format a millisecond duration as Jaeger-style duration string
    ///     ("500ms" for sub-second, "5s" for ≥1s). Returns null when input is null.
    /// </summary>
    public static string? FormatDuration(int? ms)
    {
        if (ms is null)
        {
            return null;
        }

        return ms.Value >= 1000 ? $"{ms.Value / 1000}s" : $"{ms.Value}ms";
    }
}