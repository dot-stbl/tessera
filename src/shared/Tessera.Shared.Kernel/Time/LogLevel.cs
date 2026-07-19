namespace Tessera.Shared.Kernel.Time;

/// <summary>
///     Log severity level. Maps to OpenTelemetry LogLevel + syslog severity.
/// </summary>
public enum LogLevel
{
    /// <summary>Verbose trace information; typically disabled in production.</summary>
    Trace,

    /// <summary>Diagnostic information useful for debugging.</summary>
    Debug,

    /// <summary>Informational messages about normal operation.</summary>
    Information,

    /// <summary>Recoverable abnormal conditions.</summary>
    Warning,

    /// <summary>Errors that prevented an operation from completing.</summary>
    Error,

    /// <summary>Critical failures requiring immediate attention.</summary>
    Fatal
}
