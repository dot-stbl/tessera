namespace Tessera.Shared.Kernel.Identifiers;

/// <summary>
///     Span identifier (typically 16-char hex per OpenTelemetry / Jaeger convention).
/// </summary>
public sealed record SpanId(string Value);
