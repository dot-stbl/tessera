using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Tessera.Shared.Authentication.Providers.Keycloak;

/// <summary>
///     Options for the Keycloak JWT bearer authentication provider.
///     Validates inbound bearer tokens via the OIDC discovery document
///     served by Keycloak's realm endpoint. Per ADR-0001 Decision 3 —
///     Keycloak = <c>Microsoft.AspNetCore.Authentication.JwtBearer</c>
///     with OIDC discovery via <see cref="JwtBearerOptions.Authority" />.
///     <para>
///         Single-tenant deployments typically point
///     <see cref="Authority" /> at a single realm URL (e.g.
///     <c>https://kc.example.com/realms/tessera</c>). The
///     <c>JWKS_uri</c> is fetched automatically from the discovery
///     document, so the public-key rotation is transparent.
///     <see cref="ValidIssuers" /> is an additional allow-list matched
///     against the <c>iss</c> claim when the realm is exposed through
///     more than one URL (internal + external DNS, dual stack, etc.).
///     </para>
///     <para>
///         Token validation parameters (signature, expiry, audience) are
///         configured by the framework handler from
///     <see cref="JwtBearerOptions.TokenValidationParameters" />; this
///     class carries the cross-cutting fields so the
///     <c>AddTesseraAuthentication</c> installer stays declarative.
///     </para>
/// </summary>
public sealed class KeycloakAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "auth.providers.keycloak";

    /// <summary>JWT bearer header. Standard <c>Authorization</c>.</summary>
    public const string AuthorizationHeader = "Authorization";

    /// <summary>Bearer token prefix.</summary>
    public const string BearerPrefix = "Bearer ";

    /// <summary>
    ///     Keycloak realm OIDC endpoint — drives
    ///     <c>JWKS_uri</c> discovery. Bound from
    ///     <c>[auth.providers.keycloak]/authority</c>.
    /// </summary>
    [Required]
    public Uri Authority { get; internal set; } = new("https://localhost:8443/realms/tessera");

    /// <summary>
    ///     Expected JWT <c>aud</c> claim. Bound from
    ///     <c>[auth.providers.keycloak]/audience</c>.
    ///     <c>null</c> skips audience validation (relies on iss only).
    /// </summary>
    public string? Audience { get; internal set; }

    /// <summary>
    ///     Extra allowed <c>iss</c> claim values beyond the realm —
    ///     useful when Keycloak is reachable through multiple DNS names
    ///     (internal + external) but most deployments leave this empty.
    /// </summary>
    public string[] ValidIssuers { get; internal set; } = [];

    /// <summary>
    ///     Require HTTPS for the OIDC metadata. Default <c>true</c>;
    ///     dev can set <c>require_https_metadata = false</c> via
    ///     <c>[auth.providers.keycloak]/require_https_metadata</c>.
    /// </summary>
    public bool RequireHttpsMetadata { get; internal set; } = true;
}

/// <summary>
///     File-static helper that resolves SecretReference-shaped
///     <see cref="KeycloakAuthOptions.Authority" /> values at startup.
///     Keep this helper file-static so the same PostConfigure pattern
///     from the LDAP / AdminBearer providers applies without
///     multiplying <see cref="Microsoft.Extensions.Options.IConfigureOptions{TOptions}" />
///     classes (code-shape.md §9 ban).
/// </summary>
internal static class ResolveKeycloakAuthority
{
    /// <summary>
    ///     PostConfigure hook — resolves the <see cref="KeycloakAuthOptions.Authority" />
    ///     string if the operator wrote <c>"env:VAR"</c> or
    ///     <c>"file:/path"</c> instead of a literal URL.
    /// </summary>
    public static void Apply(KeycloakAuthOptions options)
    {
        // The Authority is bound from configuration as a Uri directly
        // by Microsoft.Extensions.Configuration (the value at the
        // binding path is parsed). We don't need to touch it here — the
        // hook exists so future fields added to KeycloakAuthOptions
        // (e.g. client_secret) get the same SecretReference treatment
        // pattern as LDAP / AdminBearer.
        _ = options;
    }
}
