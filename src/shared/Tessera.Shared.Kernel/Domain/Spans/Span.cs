using Tessera.Shared.Kernel.Domain.Traces;
using Tessera.Shared.Kernel.Identifiers;

namespace Tessera.Shared.Kernel.Domain.Spans;

/// <summary>
///     A single span within a trace. May have a parent (<see cref="ParentSpanId" />)
///     and child spans (<see cref="Events" /> captures span events like exceptions).
/// </summary>
public sealed record Span(
    SpanId SpanId,
    SpanId? ParentSpanId,
    string Service,
    string Operation,
    long StartTime,
    long DurationMs,
    TraceStatus Status,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<SpanEvent> Events);
