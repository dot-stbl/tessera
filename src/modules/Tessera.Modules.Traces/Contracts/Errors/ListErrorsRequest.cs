namespace Tessera.Modules.Traces.Contracts.Errors;

/// <summary>
///     Query string parameters for <c>GET /api/v1/errors</c> (errors inbox).
/// </summary>
public sealed class ListErrorsRequest
{
    /// <summary>Start of the time range, inclusive (UTC unix milliseconds). Required.</summary>
    public long StartUnixMs { get; init; }

    /// <summary>End of the time range, inclusive (UTC unix milliseconds). Required.</summary>
    public long EndUnixMs { get; init; }

    /// <summary>Optional service filter (substring match on search).</summary>
    public string? Service { get; init; }

    /// <summary>
    ///     Max error-status summaries to consider from search (default 50).
    ///     Detail fetch is further capped by
    ///     <see cref="Tessera.Modules.Traces.Services.ErrorsInboxService.MaxDetailFetches" />.
    /// </summary>
    public int? Limit { get; init; } = 50;
}
