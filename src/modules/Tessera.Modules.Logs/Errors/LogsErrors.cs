namespace Tessera.Modules.Logs.Errors;

/// <summary>
///     Stable machine-readable error codes for the Logs module. Used as
///     <see cref="Tessera.Shared.Kernel.Exceptions.ProviderException.Code" />
///     values; the global <c>TesseraExceptionHandler</c> translates them into
///     ProblemDetails bodies.
/// </summary>
public static class LogsErrors
{
    /// <summary>
    ///     Query omitted the required <c>traceId</c>. MVP-01 only supports
    ///     trace-correlation lookup; ad-hoc LogsQL is MVP-02. Maps to 400.
    /// </summary>
    public const string TraceIdRequired = "logs.trace_id_required";
}
