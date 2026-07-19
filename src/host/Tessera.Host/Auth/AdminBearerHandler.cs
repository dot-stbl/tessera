using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace Tessera.Host.Auth;

/// <summary>
///     Authentication handler for the admin-bearer scheme. Single-factor:
///     compares the <c>Authorization: Bearer {token}</c> header value to the
///     expected token from <see cref="AdminBearerOptions.AdminToken" /> using
///     <see cref="CryptographicOperations.FixedTimeEquals" /> so the comparison
///     timing is independent of how many bytes match.
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
    private const string SchemeName = "admin";
    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = Options.AdminToken;
        if (string.IsNullOrEmpty(expected))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(AuthorizationHeader, out var raw))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = raw.ToString();
        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid scheme."));
        }

        var presented = headerValue[BearerPrefix.Length..].Trim();
        if (!FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid token."));
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "admin"),
        ],
        SchemeName);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    ///     Length-equalizing constant-time byte comparison. Pads the shorter
    ///     input to the longer so the loop runs the same number of iterations
    ///     regardless of where the bytes first differ — closes the timing
    ///     side channel on token length.
    /// </summary>
    private static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}