namespace Tessera.Providers.Victoria.Dto.Jaeger.Trace;

/// <summary>
///     Jaeger trace envelope as returned by VictoriaTraces (Jaeger-compatible API).
///     Flat list of spans + indexed processes; consumer is responsible for
///     reconstructing the parent/child tree from
///     <see cref="Span.JaegerSpan.References" />.
/// </summary>
/// <param name="TraceID">32-char hex trace identifier.</param>
/// <param name="Spans">Flat list of spans belonging to this trace.</param>
/// <param name="Processes">Map of <c>processID</c> to process metadata.</param>
/// <param name="Warnings">Optional backend warnings (null if none).</param>
public sealed record JaegerTrace(
    string TraceID,
    IReadOnlyList<Span.JaegerSpan> Spans,
    IReadOnlyDictionary<string, JaegerProcess> Processes,
    object? Warnings);