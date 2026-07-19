namespace Tessera.Shared.Http.Configuration;

/// <summary>
///     Authentication options shared by Tessera HTTP clients (Refit + Polly
///     resilience pipelines). Bound from <c>tessera.toml</c> at composition root.
/// </summary>
public sealed class HttpClientAuthOptions
{
    /// <summary>
    ///     Bearer token to send in <c>Authorization: Bearer &lt;token&gt;</c>. Null or
    ///     whitespace disables auth (no header is sent).
    /// </summary>
    public string? AuthToken { get; init; }
}
