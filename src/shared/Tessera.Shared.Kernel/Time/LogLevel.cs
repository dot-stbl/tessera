namespace Tessera.Shared.Kernel.Time;

/// <summary>
///     Log severity level. Maps to OpenTelemetry LogLevel + syslog severity.
///     <para>
///         Member names are the OTel <c>SeverityText</c> spellings
///         (<c>INFO</c> / <c>WARN</c>), not the .NET
///         <c>Information</c> / <c>Warning</c> ones. They travel to the wire
///         verbatim (camelCased) and every consumer — the TypeScript union and
///         the <c>.log-level-*</c> CSS classes it composes by name — keys off
///         those spellings, so renaming a member here silently unstyles a
///         severity in the UI.
///     </para>
/// </summary>
public enum LogLevel
{
    /// <summary>Verbose trace information; typically disabled in production.</summary>
    Trace = 0,

    /// <summary>Diagnostic information useful for debugging.</summary>
    Debug = 1,

    /// <summary>Informational messages about normal operation.</summary>
    Info = 2,

    /// <summary>Recoverable abnormal conditions.</summary>
    Warn = 3,

    /// <summary>Errors that prevented an operation from completing.</summary>
    Error = 4,

    /// <summary>Critical failures requiring immediate attention.</summary>
    Fatal = 5
}
