using Microsoft.AspNetCore.Authentication;

namespace Tessera.Host.Auth;

/// <summary>
///     Authentication-scheme options for the admin bearer scheme. Token
///     comparison happens in <see cref="AdminBearerHandler.HandleAuthenticateAsync" />.
///     Token is nullable: when null (env var
///     <c>TESSERA_ADMIN_TOKEN</c> unset), the handler always returns
///     <see cref="AuthenticateResult.NoResult()" /> so admin endpoints
///     reject with 401 and the host still starts cleanly. This matches the
///     MVP-01 "admin bearer optional" decision.
/// </summary>
public sealed class AdminBearerOptions : AuthenticationSchemeOptions
{
    /// <summary>
    ///     Expected bearer token. Bound from <c>TESSERA_ADMIN_TOKEN</c> env var
    ///     by the host composition root; the handler compares incoming
    ///     <c>Authorization: Bearer {token}</c> against this value via
    ///     <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals" />
    ///     to prevent timing-attack token recovery.
    /// </summary>
    public string? AdminToken { get; set; }
}