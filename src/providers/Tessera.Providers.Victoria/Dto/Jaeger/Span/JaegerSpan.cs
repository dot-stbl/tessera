namespace Tessera.Providers.Victoria.Dto.Jaeger.Span;

using Tessera.Providers.Victoria.Dto.Jaeger;

/// <summary>
///     Jaeger span record as returned by VictoriaTraces. Times are in
///     <strong>microseconds since epoch</strong> (Jaeger convention).
/// </summary>
/// <param name="TraceID">Trace identifier (duplicated on every span).</param>
/// <param name="SpanID">16-char hex span identifier.</param>
/// <param name="OperationName">Span name (e.g. <c>"GET /cart"</c>).</param>
/// <param name="ProcessID">Foreign key into <c>JaegerTrace.Processes</c>.</param>
/// <param name="StartTime">Start of the span, in microseconds since epoch.</param>
/// <param name="Duration">Span duration, in microseconds.</param>
/// <param name="Tags">Span attributes (key-value pairs).</param>
/// <param name="Logs">Span events (exception, message) recorded during the span.</param>
/// <param name="References">Parent-child references to other spans (CHILD_OF, FOLLOWS_FROM).</param>
public sealed record JaegerSpan(
    string TraceID,
    string SpanID,
    string OperationName,
    string ProcessID,
    long StartTime,
    long Duration,
    IReadOnlyList<JaegerTag> Tags,
    IReadOnlyList<JaegerLog> Logs,
    IReadOnlyList<JaegerReference> References);