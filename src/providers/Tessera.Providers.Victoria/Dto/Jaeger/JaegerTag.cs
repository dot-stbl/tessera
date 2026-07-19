namespace Tessera.Providers.Victoria.Dto.Jaeger;

/// <summary>
///     Jaeger tag / attribute key-value record. <see cref="Type" /> is the
///     OpenTelemetry attribute type (<c>"string"</c>, <c>"int"</c>, <c>"double"</c>, <c>"bool"</c>).
///     Both Span and Process carry lists of these.
/// </summary>
public sealed record JaegerTag(string Key, string Type, string Value);