namespace Tessera.Providers.Victoria.Dto.Jaeger.Span;

/// <summary>
///     Jaeger reference to another span in the same trace. <c>RefType</c> is
///     <c>CHILD_OF</c> for parent-child relationships, <c>FOLLOWS_FROM</c> for causal
///     ordering. The consumer reconstructs the span tree from these.
/// </summary>
public sealed record JaegerReference(string RefType, string SpanID, string TraceID);
