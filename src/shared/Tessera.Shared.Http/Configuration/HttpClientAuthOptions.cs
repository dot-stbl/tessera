namespace Tessera.Shared.Http.Configuration;

/// <summary>
///     Authentication options shared by Tessera HTTP clients (Refit + Polly
///     resilience pipelines). Bound from <c>tessera.toml</c> at composition root.
///     Declared as a <c>sealed record</c> so callers (notably
///     <c>VictoriaOptions</c> PostConfigure) can use <c>with</c>-expressions
///     after SecretReference resolution.
/// </summary>
public sealed record HttpClientAuthOptions
{
    /// <summary>
    ///     Bearer token to send in <c>Authorization: Bearer &lt;token&gt;</c>. Null or
    ///     whitespace disables auth (no header is sent).
    /// </summary>
    /// <remarks>
    ///     Settable so secret resolution can rewrite <c>env:</c>/<c>file:</c>
    ///     references in place during PostConfigure.
    /// </remarks>
    public string? AuthToken { get; set; }
}
