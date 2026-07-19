namespace Tessera.Shared.Kernel.Domain.Traces;

/// <summary>
///     Status of a trace or span, mirroring OpenTelemetry span status codes.
/// </summary>
public enum TraceStatus
{
    /// <summary>Span completed without error.</summary>
    Ok,

    /// <summary>Span completed with an error.</summary>
    Error,

    /// <summary>Span status was unset (OpenTelemetry default).</summary>
    Unset
}
