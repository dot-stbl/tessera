using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Tessera.Shared.Authentication.Core;

namespace Tessera.Shared.Authentication.Providers.Guest;

/// <summary>
///     Kernel-level face of the Guest authentication scheme. Backed by
///     <see cref="GuestAuthHandler" /> via the standard ASP.NET auth
///     pipeline; this class exists so non-endpoint code (such as
///     <c>ICurrentUserContext</c>) can resolve auth state programmatically
///     without calling the framework handler directly.
/// </summary>
public sealed class GuestAuthProvider : IAuthProvider
{
    /// <summary>
    ///     Scheme name. Constant because the Guest provider is always
    ///     available — no need for an <c>[auth.providers.guest]/enabled</c>
    ///     gate.
    /// </summary>
    public const string NameConst = "guest";

    /// <inheritdoc />
    public string Name => NameConst;

    /// <inheritdoc />
    public Task<AuthenticateResult> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(GuestAuthHandler.RoleClaimType, GuestAuthHandler.RoleGuest)],
            authenticationType: Name,
            nameType: System.Security.Claims.ClaimTypes.Name,
            roleType: GuestAuthHandler.RoleClaimType);

        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <inheritdoc />
    public void ValidateOptions(IConfigurationSection section)
    {
        // Guest has no tunable configuration beyond the section
        // existence (which is implicit — Guest is always-on). A
        // presence-only section is valid; an empty section is valid;
        // a malformed section is just <c>[auth.providers.guest]</c>
        // with no keys, which we don't validate.
    }
}

/// <summary>
///     Default-options placeholder so <c>AddScheme&lt;GuestAuthOptions,
///     GuestAuthHandler&gt;</c> can be invoked with the minimum
///     delegate. No settings are bound here — Guest has no configuration.
/// </summary>
public sealed class GuestAuthOptions : AuthenticationSchemeOptions;
