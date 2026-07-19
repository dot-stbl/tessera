namespace Tessera.Providers.Victoria.Dto.Jaeger.Span;

/// <summary>
///     Jaeger span event: a timestamped log entry with structured fields.
///     Typical uses: exception, message, attribute change.
/// </summary>
/// <param name="Timestamp">Event time, in microseconds since epoch.</param>
/// <param name="Fields">Structured key-value attributes on the event.</param>
public sealed record JaegerLog(long Timestamp, IReadOnlyList<Jaeger.JaegerTag> Fields);