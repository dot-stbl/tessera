using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tessera.Shared.Authentication.Providers.Admin;

/// <summary>
///     Authentication handler for the admin-bearer scheme. Single-factor:
///     compares the <c>Authorization: Bearer {token}</c> header value to the
///     expected token from <see cref="AdminBearerOptions.AdminToken" /> using
///     <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals" />
///     so the comparison timing is independent of how many bytes match.
/// </summary>
/// <remarks>
///     When <see cref="AdminBearerOptions.AdminToken" /> is null (env var
///     <c>TESSERA_ADMIN_TOKEN</c> not set), the handler returns
///     <see cref="AuthenticateResult.NoResult()" /> for every request —
///     effectively disabling the admin scheme without throwing. Admin
///     endpoints protected by <c>[Authorize(Policy = "admin")]</c> then
///     return 401. This matches the MVP-01 "admin bearer optional" decision
///     (deferred per-state and doc).
/// </remarks>
public sealed class AdminBearerHandler(
    IOptionsMonitor<AdminBearerOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AdminBearerOptions>(options, loggerFactory, encoder)
{
    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = Options.AdminToken;
        if (string.IsNullOrEmpty(expected))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(AdminBearerConstants.AuthorizationHeader, out var raw))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = raw.ToString();
        if (!headerValue.StartsWith(AdminBearerConstants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid scheme."));
        }

        var presented = headerValue[AdminBearerConstants.BearerPrefix.Length..].Trim();
        if (!AdminBearerCryptography.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid token."));
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "admin"),
        ],
        AdminBearerConstants.SchemeName);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AdminBearerConstants.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
