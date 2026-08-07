namespace Tessera.Modules.Discovery.Contracts;

/// <summary>
///     Query string parameters for <c>GET /api/v1/services/{serviceName}/red</c>.
/// </summary>
public sealed class GetServiceRedRequest
{
    /// <summary>Start of the evaluation window, inclusive (UTC unix milliseconds). Required.</summary>
    public long StartUnixMs { get; init; }

    /// <summary>End of the evaluation window, inclusive (UTC unix milliseconds). Required.</summary>
    public long EndUnixMs { get; init; }

    /// <summary>
    ///     Optional operation / route filter. When set, PromQL adds
    ///     <c>http_route="…"</c> (OTel HTTP route label).
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    ///     Optional range-query step in seconds. Reserved for future series
    ///     responses; instant queries at <see cref="EndUnixMs" /> ignore it.
    /// </summary>
    public int? StepSeconds { get; init; }
}
