namespace Tessera.Modules.Discovery.Errors;

/// <summary>
///     Stable machine-readable error codes for the Discovery module.
/// </summary>
public static class DiscoveryErrors
{
    /// <summary>RED query is malformed (missing/inverted time range). Maps to 502 via base ProviderException.</summary>
    public const string RedInvalid = "discovery.red.invalid";

    /// <summary>Named service is not present in the inventory. Maps to 404.</summary>
    public const string ServiceNotFound = "discovery.service.not_found";
}
