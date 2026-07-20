namespace Tessera.Modules.Traces.Errors;

/// <summary>
///     Stable machine-readable error codes for the Traces module. Used as
///     <see cref="Tessera.Shared.Kernel.Exceptions.ProviderException.Code" />
///     values; the global <c>TesseraExceptionHandler</c> translates them into
///     ProblemDetails bodies with <c>type = "/errors/{code}"</c>.
/// </summary>
public static class TracesErrors
{
    /// <summary>Trace id was valid but the upstream backend has no trace under it. Maps to 404.</summary>
    public const string TraceNotFound = "trace.not_found";

    /// <summary>Search query is malformed (negative duration, end before start, etc.). Maps to 400.</summary>
    public const string SearchInvalid = "trace.search.invalid";

    /// <summary>Generic upstream network failure. Maps to 502.</summary>
    public const string BackendUnreachable = "provider.network_error";
}
