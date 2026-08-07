namespace Tessera.Shared.Kernel.Analysis;

/// <summary>
///     Degradation mode for a request view (trace + correlated logs).
///     FE uses this to show a banner when one source is missing.
/// </summary>
public enum RequestViewMode
{
    /// <summary>Both spans and correlated logs are present.</summary>
    Full = 0,

    /// <summary>Trace/spans present; no correlated logs in the query window.</summary>
    SpansOnly = 1,

    /// <summary>Logs present for the trace id; no span tree (or empty spans).</summary>
    LogsOnly = 2,

    /// <summary>Neither spans nor logs — maps to 404 at the module boundary.</summary>
    Empty = 3,
}
