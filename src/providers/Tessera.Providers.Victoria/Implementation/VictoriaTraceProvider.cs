using Tessera.Providers.Victoria.Clients;
using Tessera.Providers.Victoria.Configuration;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Pagination;
using Tessera.Shared.Kernel.Providers.Traces;

using DomainSpan = Tessera.Shared.Kernel.Domain.Spans.Span;
using JaegerSpan = Tessera.Providers.Victoria.Dto.Jaeger.Span.JaegerSpan;

namespace Tessera.Providers.Victoria.Implementation;

/// <summary>
///     VictoriaTraces implementation of <see cref="ITraceProvider" />.
///     Maps Jaeger-shaped responses from <c>vtselect</c> to domain
///     <see cref="TraceSummary" /> / <see cref="TraceDetail" /> types.
/// </summary>
public sealed class VictoriaTraceProvider(IVictoriaTracesClient client, VictoriaOptions options) : ITraceProvider
{
    /// <inheritdoc />
    public async Task<Page<TraceSummary>> SearchAsync(TraceSearchQuery query, CancellationToken ct)
    {
        var response = await client.SearchTracesAsync(
            options.Tenant,
            service: query.Service,
            operation: query.Operation,
            tags: null,
            start: query.StartUnixMs,
            end: query.EndUnixMs,
            minDuration: FormatDuration(query.MinDurationMs),
            maxDuration: FormatDuration(query.MaxDurationMs),
            limit: query.Limit,
            ct);

        var items = new List<TraceSummary>(response.Data.Count);
        for (var i = 0; i < response.Data.Count; i++)
        {
            items.Add(MapToSummary(response.Data[i]));
        }

        return new Page<TraceSummary>(items, Cursor: null, HasMore: false);
    }

    /// <inheritdoc />
    public async Task<TraceDetail?> GetByIdAsync(TraceId traceId, CancellationToken ct)
    {
        var response = await client.GetTraceAsync(options.Tenant, traceId.Value, ct);
        if (response.Data.Count == 0)
        {
            return null;
        }

        var jaeger = response.Data[0];
        var rootJaeger = FindRootSpan(jaeger);
        var rootService = ResolveRootService(rootJaeger, jaeger);
        var rootOperation = rootJaeger?.OperationName ?? "unknown";

        var domainSpans = new List<DomainSpan>(jaeger.Spans.Count);
        for (var i = 0; i < jaeger.Spans.Count; i++)
        {
            domainSpans.Add(MapToDomainSpan(jaeger.Spans[i], jaeger.Processes));
        }

        return new TraceDetail(
            TraceId: new TraceId(jaeger.TraceID),
            RootService: rootService,
            RootOperation: rootOperation,
            StartTime: rootJaeger?.StartTime ?? 0,
            DurationMs: rootJaeger is not null ? rootJaeger.Duration / 1000 : 0,
            Status: rootJaeger is not null ? MapStatus(rootJaeger) : TraceStatus.Unset,
            Spans: domainSpans);
    }

    private static JaegerSpan? FindRootSpan(JaegerTrace trace)
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

    private static string ResolveRootService(JaegerSpan? root, JaegerTrace jaeger)
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

    private static TraceSummary MapToSummary(JaegerTrace trace)
    {
        var rootSpan = FindRootSpan(trace);
        var service = ResolveRootService(rootSpan, trace);
        var operation = rootSpan?.OperationName ?? "unknown";

        var services = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in trace.Processes.Values)
        {
            if (seen.Add(p.ServiceName))
            {
                services.Add(p.ServiceName);
            }
        }

        var spanCount = trace.Spans.Count;
        var startTime = rootSpan?.StartTime ?? 0;
        var durationMs = rootSpan is not null ? rootSpan.Duration / 1000 : 0;
        var status = rootSpan is not null ? MapStatus(rootSpan) : TraceStatus.Unset;

        return new TraceSummary(
            TraceId: new TraceId(trace.TraceID),
            RootService: service,
            RootOperation: operation,
            StartTime: startTime,
            DurationMs: durationMs,
            Status: status,
            SpanCount: spanCount,
            Services: services);
    }

    private static DomainSpan MapToDomainSpan(JaegerSpan dto, IReadOnlyDictionary<string, JaegerProcess> processes)
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
            var log = dto.Logs[i];
            var name = "log";
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var j = 0; j < log.Fields.Count; j++)
            {
                var field = log.Fields[j];
                attributes[field.Key] = field.Value;
                if (field.Key == "event")
                {
                    name = field.Value;
                }
            }

            events.Add(new SpanEvent(Time: log.Timestamp, Name: name, Attributes: attributes));
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
            Status: MapStatus(dto),
            Tags: tags,
            Events: events);
    }

    private static TraceStatus MapStatus(JaegerSpan span)
    {
        for (var i = 0; i < span.Tags.Count; i++)
        {
            var tag = span.Tags[i];
            if (tag.Key is "error" or "otel.status_code")
            {
                return MapTagValue(tag.Value);
            }
        }

        return TraceStatus.Unset;
    }

    private static TraceStatus MapTagValue(string value)
    {
        return value switch
        {
            "ERROR" or "true" or "2" => TraceStatus.Error,
            "OK" or "false" or "1" => TraceStatus.Ok,
            _ => TraceStatus.Unset,
        };
    }

    private static string? FormatDuration(int? ms)
    {
        if (ms is null)
        {
            return null;
        }

        return ms.Value >= 1000 ? $"{ms.Value / 1000}s" : $"{ms.Value}ms";
    }
}