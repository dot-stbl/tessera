namespace Tessera.Shared.Kernel.Domain.Traces;

/// <summary>
///     Status of a trace or span, mirroring OpenTelemetry span status codes.
/// </summary>
public enum TraceStatus
{
    /// <summary>Span completed without error.</summary>
    Ok = 0,

    /// <summary>Span completed with an error.</summary>
    Error = 1,

    /// <summary>Span status was unset (OpenTelemetry default).</summary>
    Unset = 2
}
