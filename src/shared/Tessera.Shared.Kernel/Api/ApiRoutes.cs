namespace Tessera.Shared.Kernel.Api;

/// <summary>
///     Single source of truth for HTTP route templates. Endpoints, integration
///     tests, and (eventually) the FE codegen pipeline all reference these
///     constants — never literal route strings in <c>[Route]</c> or
///     <c>HttpGet</c>. Matches the Plexor convention
///     (<see href="https://github.com/dot-stbl/plexor" />).
/// </summary>
public static class ApiRoutes
{
    /// <summary>The API version segment embedded in every route (v1).</summary>
    public const string ApiVersion = "v1";

    /// <summary>Base path prefix composed from <see cref="ApiVersion" /> (<c>"api/v1"</c>).</summary>
    public const string Base = "api/" + ApiVersion;

    /// <summary>
    ///     Composes a resource root under <see cref="Base" />:
    ///     <c>api/v1/{name}</c>. Use in controller <c>[Route]</c> attributes:
    ///     <c>[Route($"{ApiRoutes.Base}/services")]</c>.
    /// </summary>
    public static string Resource(string name)
    {
        return Base + "/" + name;
    }

    /// <summary>Composite health probe across all wired providers.</summary>
    public const string Health = Base + "/health";

    /// <summary>Service inventory (list of services known to the backend, each with its operations).</summary>
    public const string Services = Base + "/services";

    /// <summary>
    ///     RED (rate/errors/duration) for one service. Query: startUnixMs, endUnixMs,
    ///     optional operation and stepSeconds. Owned by Discovery (ADR-0003).
    /// </summary>
    public const string ServiceRed = Base + "/services/{serviceName}/red";

    /// <summary>Trace search.</summary>
    public const string Traces = Base + "/traces";

    /// <summary>
    ///     Trace detail by id (with reconstructed span tree), as an absolute
    ///     path — for integration tests, link generation and documentation.
    ///     <c>:length(32)</c> matches Tessera's 32-char hex trace-id convention.
    ///     <para>
    ///         Do <b>not</b> put this on a method of a controller already routed
    ///         at <see cref="Traces" />: MVC concatenates class and method
    ///         templates, which produced the live endpoint
    ///         <c>api/v1/traces/api/v1/traces/{traceId}</c>. Use
    ///         <see cref="TraceByIdRelative" /> there.
    ///     </para>
    /// </summary>
    public const string Trace = Base + "/traces/{traceId:length(32)}";

    /// <summary>
    ///     The trace-detail template relative to <see cref="Traces" />, for
    ///     <c>[HttpGet]</c> on a controller whose <c>[Route]</c> is already
    ///     <see cref="Traces" />. Kept in the same file as <see cref="Trace" />
    ///     so the two cannot drift.
    /// </summary>
    public const string TraceByIdRelative = "{traceId:length(32)}";

    /// <summary>Logs correlated with a specific trace (sub-resource).</summary>
    public const string TraceLogs = Base + "/traces/{traceId:length(32)}/logs";

    /// <summary>
    ///     <see cref="TraceLogs" /> relative to <see cref="Traces" />, for the
    ///     same reason as <see cref="TraceByIdRelative" />.
    /// </summary>
    public const string TraceLogsRelative = "{traceId:length(32)}/logs";

    /// <summary>Ad-hoc log search (LogsQL filter, MVP-02).</summary>
    public const string Logs = Base + "/logs";

    /// <summary>Errors inbox — grouped exception summaries over a time window (Traces module).</summary>
    public const string Errors = Base + "/errors";
}
