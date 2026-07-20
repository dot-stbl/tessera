namespace Tessera.Shared.Kernel.Time;

/// <summary>
///     Log severity level. Maps to OpenTelemetry LogLevel + syslog severity.
/// </summary>
public enum LogLevel
{
    /// <summary>Verbose trace information; typically disabled in production.</summary>
    Trace = 0,

    /// <summary>Diagnostic information useful for debugging.</summary>
    Debug = 1,

    /// <summary>Informational messages about normal operation.</summary>
    Information = 2,

    /// <summary>Recoverable abnormal conditions.</summary>
    Warning = 3,

    /// <summary>Errors that prevented an operation from completing.</summary>
    Error = 4,

    /// <summary>Critical failures requiring immediate attention.</summary>
    Fatal = 5
}
