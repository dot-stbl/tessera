namespace Tessera.Shared.Kernel.Domain.Spans;

/// <summary>
/// A point-in-time event recorded on a span (e.g. exception, log message).
/// </summary>
public sealed record SpanEvent(
    long Time,
    string Name,
    IReadOnlyDictionary<string, string> Attributes);