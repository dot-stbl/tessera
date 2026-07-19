namespace Tessera.Shared.Kernel.Identifiers;

/// <summary>
///     Trace identifier (typically 32-char hex per OpenTelemetry / Jaeger convention).
/// </summary>
public sealed record TraceId(string Value);
