using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tessera.Shared.Authentication.Providers.Guest;

/// <summary>
///     Authentication handler that always succeeds with a single
///     <c>guest</c> identity. Used as the default scheme for
///     self-hosted single-tenant Tessera deployments where anonymous
///     reads are acceptable and admin actions are gated by a separate
///     scheme (typically <c>admin-bearer</c>).
///     <para>
///         This handler never returns <see cref="AuthenticateResult.NoResult()" />
///         or <see cref="AuthenticateResult.Fail(string)" />. It
///         unconditionally produces a <see cref="ClaimsPrincipal" />
///         authenticated via <see cref="AuthenticationScheme.Name" /> with role
///         <c>"guest"</c>. Endpoints that should <em>not</em> be reachable
///         anonymously must carry <c>[Authorize(Policy="admin")]</c> or
///         similar explicit gating.
///     </para>
/// </summary>
/// <remarks>Framework-required constructor.</remarks>
public sealed class GuestAuthHandler(
    IOptionsMonitor<GuestAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<GuestAuthOptions>(options, logger, encoder)
{
    /// <summary>
    ///     Claim type emitted by <see cref="GuestAuthHandler" /> on the
    ///     anonymous principal. Downstream authorization policies and
    ///     <c>ICurrentUserContext</c> use this to distinguish Guest from
    ///     other schemes.
    /// </summary>
    public const string RoleClaimType = "role";

    /// <summary>Role assigned to Guest-authenticated principals.</summary>
    public const string RoleGuest = "guest";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(RoleClaimType, RoleGuest)],
            authenticationType: Scheme.Name,
            nameType: ClaimTypes.Name,
            roleType: RoleClaimType);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties(),
            Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    ///     Override to skip the <c>302 Found</c> challenge behavior —
    ///     Guest already authenticated every request as anonymous, so a
    ///     challenge is a misconfiguration, not an opportunity to redirect.
    /// </summary>
    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        return Task.CompletedTask;
    }
}
