namespace Tessera.Shared.Kernel.Api;

/// <summary>
///     Single source of truth for HTTP route templates. Endpoints, integration
///     tests, and the generated FE client all reference these constants —
///     never literal route strings in <c>MapGet</c>/<c>MapPost</c>.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Composite health probe across all wired providers.</summary>
    public const string Health = "/api/health";

    /// <summary>Service inventory (list of services known to the backend, each with its operations).</summary>
    public const string Services = "/api/services";

    /// <summary>Trace search.</summary>
    public const string Traces = "/api/traces";

    /// <summary>Trace detail by id (with reconstructed span tree).</summary>
    public const string Trace = "/api/traces/{traceId}";

    /// <summary>Logs correlated with a specific trace (sub-resource).</summary>
    public const string TraceLogs = "/api/traces/{traceId}/logs";

    /// <summary>Ad-hoc log search (LogsQL filter, MVP-02).</summary>
    public const string Logs = "/api/logs";
}