using Tessera.Providers.Victoria.Dto.Jaeger.Span;
using Tessera.Providers.Victoria.Dto.Jaeger.Trace;
using Tessera.Shared.Kernel.Domain.Resources;
using Tessera.Shared.Kernel.Domain.Spans;
using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;
using Tessera.Shared.Kernel.Observability;
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
    ///     from the process map. Times are converted from Jaeger µs to domain ms.
    /// </summary>
    public static TraceDetail ToDetail(JaegerTrace jaeger)
    {
        var root = FindRootSpan(jaeger);
        var rootService = ResolveRootService(root, jaeger);
        var rootOperation = root?.OperationName ?? "unknown";
        var resourceByProcessId = InternResources(jaeger.Processes);
        var spans = new List<DomainSpan>(jaeger.Spans.Count);

        for (var i = 0; i < jaeger.Spans.Count; i++)
        {
            spans.Add(ToDomainSpan(jaeger.Spans[i], resourceByProcessId));
        }

        return new TraceDetail(
            TraceId: new TraceId(jaeger.TraceID),
            RootService: rootService,
            RootOperation: rootOperation,
            StartTime: root is not null ? root.StartTime / 1000 : 0,
            DurationMs: root is not null ? root.Duration / 1000 : 0,
            Status: root is not null ? ToStatus(root) : TraceStatus.Unset,
            Spans: spans);
    }

    /// <summary>
    ///     Map a single Jaeger trace to a domain <see cref="TraceSummary" />.
    ///     StartTime is converted from Jaeger µs to domain unix ms.
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
            StartTime: root is not null ? root.StartTime / 1000 : 0,
            DurationMs: root is not null ? root.Duration / 1000 : 0,
            Status: root is not null ? ToStatus(root) : TraceStatus.Unset,
            SpanCount: trace.Spans.Count,
            Services: services);
    }

    /// <summary>
    ///     Convert a Jaeger span and interned process resources to a domain
    ///     <see cref="DomainSpan" />. StartTime and Duration are µs→ms.
    /// </summary>
    public static DomainSpan ToDomainSpan(
        JaegerSpan dto,
        IReadOnlyDictionary<string, Resource> resourceByProcessId)
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

        var resource = resourceByProcessId.TryGetValue(dto.ProcessID, out var resolved)
            ? resolved
            : new Resource("unknown", null, null, new Dictionary<string, string>(StringComparer.Ordinal));

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

        var kind = ToSpanKind(tags);

        return new DomainSpan(
            SpanId: new SpanId(dto.SpanID),
            ParentSpanId: parentSpanId,
            Service: resource.ServiceName,
            Operation: dto.OperationName,
            StartTime: dto.StartTime / 1000,
            DurationMs: dto.Duration / 1000,
            Status: ToStatus(dto),
            Kind: kind,
            Resource: resource,
            Tags: tags,
            Events: events);
    }

    /// <summary>
    ///     Convert a Jaeger log to a domain <see cref="SpanEvent" />.
    ///     Event name comes from the <c>event</c> field, or is forced to
    ///     <c>exception</c> when exception.* attributes are present; otherwise
    ///     the event is generically named <c>log</c>. Timestamp is µs→ms.
    /// </summary>
    public static SpanEvent ToSpanEvent(JaegerLog log)
    {
        var name = "log";
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasExceptionAttr = false;

        for (var i = 0; i < log.Fields.Count; i++)
        {
            var field = log.Fields[i];
            attributes[field.Key] = field.Value;
            if (field.Key == "event")
            {
                name = field.Value;
            }

            if (field.Key is SemanticConventions.ExceptionType
                or SemanticConventions.ExceptionMessage
                or SemanticConventions.ExceptionStacktrace)
            {
                hasExceptionAttr = true;
            }
        }

        if (hasExceptionAttr && name is "log" or "exception")
        {
            name = "exception";
        }

        return new SpanEvent(Time: log.Timestamp / 1000, Name: name, Attributes: attributes);
    }

    /// <summary>
    ///     Derive <see cref="TraceStatus" />: prefer <c>otel.status_code</c> /
    ///     <c>error</c>, then fall back to HTTP ≥500 / gRPC ≠0.
    /// </summary>
    public static TraceStatus ToStatus(JaegerSpan span)
    {
        string? otelStatus = null;
        string? errorTag = null;
        string? httpStatus = null;
        string? grpcStatus = null;

        for (var i = 0; i < span.Tags.Count; i++)
        {
            var tag = span.Tags[i];
            if (tag.Key == SemanticConventions.OtelStatusCode)
            {
                otelStatus = tag.Value;
            }
            else if (tag.Key == "error")
            {
                errorTag = tag.Value;
            }
            else if (tag.Key is SemanticConventions.HttpResponseStatusCode or "http.status_code")
            {
                httpStatus = tag.Value;
            }
            else if (tag.Key == SemanticConventions.RpcGrpcStatusCode)
            {
                grpcStatus = tag.Value;
            }
        }

        if (otelStatus is not null)
        {
            return otelStatus switch
            {
                "ERROR" or "2" => TraceStatus.Error,
                "OK" or "1" => TraceStatus.Ok,
                _ => TraceStatus.Unset,
            };
        }

        if (errorTag is not null)
        {
            return errorTag switch
            {
                "true" or "TRUE" or "1" => TraceStatus.Error,
                "false" or "FALSE" or "0" => TraceStatus.Ok,
                _ => TraceStatus.Unset,
            };
        }

        if (httpStatus is not null
            && int.TryParse(httpStatus, out var httpCode)
            && httpCode >= 500)
        {
            return TraceStatus.Error;
        }

        if (grpcStatus is not null
            && int.TryParse(grpcStatus, out var grpcCode)
            && grpcCode != 0)
        {
            return TraceStatus.Error;
        }

        return TraceStatus.Unset;
    }

    /// <summary>
    ///     Map span.kind tag (Jaeger / OTLP string forms) to <see cref="SpanKind" />.
    /// </summary>
    public static SpanKind ToSpanKind(IReadOnlyDictionary<string, string> tags)
    {
        if (!tags.TryGetValue(SemanticConventions.SpanKind, out var raw)
            && !tags.TryGetValue("span.kind", out raw))
        {
            return SpanKind.Unspecified;
        }

        return raw.ToLowerInvariant() switch
        {
            "internal" => SpanKind.Internal,
            "server" => SpanKind.Server,
            "client" => SpanKind.Client,
            "producer" => SpanKind.Producer,
            "consumer" => SpanKind.Consumer,
            "unspecified" or "" => SpanKind.Unspecified,
            _ => SpanKind.Unspecified,
        };
    }

    /// <summary>
    ///     Build one interned <see cref="Resource" /> per process id from the
    ///     Jaeger process map (shared by reference across spans of that process).
    /// </summary>
    public static IReadOnlyDictionary<string, Resource> InternResources(
        IReadOnlyDictionary<string, JaegerProcess> processes)
    {
        var map = new Dictionary<string, Resource>(processes.Count, StringComparer.Ordinal);
        foreach (var (processId, process) in processes)
        {
            map[processId] = ToResource(process);
        }

        return map;
    }

    /// <summary>
    ///     Map a Jaeger process (service name + tags) to an OTel <see cref="Resource" />.
    /// </summary>
    public static Resource ToResource(JaegerProcess process)
    {
        string? serviceNamespace = null;
        string? deploymentEnvironment = null;
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < process.Tags.Count; i++)
        {
            var tag = process.Tags[i];
            if (tag.Key == SemanticConventions.ServiceNamespace)
            {
                serviceNamespace = tag.Value;
            }
            else if (tag.Key == SemanticConventions.DeploymentEnvironment)
            {
                deploymentEnvironment = tag.Value;
            }
            else
            {
                // Including service.name when present as a process tag — process.ServiceName
                // remains the canonical ServiceName on Resource.
                attributes[tag.Key] = tag.Value;
            }
        }

        return new Resource(
            process.ServiceName,
            serviceNamespace,
            deploymentEnvironment,
            attributes);
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
