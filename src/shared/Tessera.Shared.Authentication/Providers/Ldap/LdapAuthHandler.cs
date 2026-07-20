using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tessera.Shared.Authentication.Providers.Ldap;

/// <summary>
///     Authentication handler for the LDAP scheme. Implements
///     search-and-bind per ADR-0001 Decision 2:
///     <list type="number">
///         <item>
///             Parse <c>Authorization: Basic &lt;base64(user:pass)&gt;</c>
///             from the request. Returns <see cref="AuthenticateResult.NoResult()" />
///             when the header is absent or malformed — same shape as
///             AdminBearer's <see cref="AuthenticateResult.NoResult()" />
///             on missing header, so the host can decide whether anonymous
///             read is allowed.
///         </item>
///         <item>
///             Bind with the service account (BindDn + BindPassword).
///             Catch any <c>System.DirectoryServices.Protocols.LdapException</c>
///             on bind failure and return
///             <see cref="AuthenticateResult.Fail(string)" /> (string
///             overload) — invalid service-account credentials must not
///             leak whether the user exists.
///         </item>
///         <item>
///             Search under <see cref="LdapAuthOptions.BaseDn" /> using
///             <see cref="LdapAuthOptions.UserFilter" /> for the user.
///             Take the first match's DN and requested role attribute.
///         </item>
///         <item>
///             Re-bind with the user DN + presented password. Return
///             <see cref="AuthenticateResult.Success" /> on success,
///             <see cref="AuthenticateResult.Fail(string)" /> on bind failure.
///         </item>
///     </list>
/// </summary>
/// <remarks>
///     <para>
///         The <c>LdapConnection</c> lifecycle is bound to the request
///         (<c>using</c>) — no caching of credentials between requests
///         (anti-pattern §8 in <c>csharp/multi-provider-auth.md</c>).
///         Bind-timeouts are the framework default — production tuning
///         of timeouts is owned by Phase 5+ reliability work.
///     </para>
///     <para>
///         LdapException handling maps to <see cref="AuthenticateResult.Fail(string)" />
///         with a stable <c>code</c> (<c>"auth.ldap.error"</c>) — the
///         host's <c>IExceptionHandler</c> turns these into a 502
///         ProblemDetails response without leaking directory structure.
///     </para>
/// </remarks>
public sealed class LdapAuthHandler(
    IOptionsMonitor<LdapAuthOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<LdapAuthOptions>(options, loggerFactory, encoder)
{
    private const string BasicPrefix = "Basic ";

    /// <summary>Stable error code for all LDAP-side failures (binding, search, malformed response).</summary>
    public const string ErrorCode = "auth.ldap.error";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var opts = Options;

        if (string.IsNullOrEmpty(opts.BindDn) || string.IsNullOrEmpty(opts.BindPassword))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(opts.AuthorizationHeader, out var raw))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = raw.ToString();
        if (!headerValue.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid scheme."));
        }

        var credentialBytes = Convert.FromBase64String(headerValue[BasicPrefix.Length..].Trim());
        var credentialText = Encoding.UTF8.GetString(credentialBytes);
        var separator = credentialText.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed credentials."));
        }

        var presentedUsername = credentialText[..separator];
        var presentedPassword = credentialText[(separator + 1)..];

        if (string.IsNullOrEmpty(presentedUsername) || string.IsNullOrEmpty(presentedPassword))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty credentials."));
        }

        var ldapException = LdapAuthenticationHelper.TryAuthenticate(
            opts,
            presentedUsername,
            presentedPassword,
            out _,
            out var roles);

        if (ldapException is not null)
        {
            Logger.LogWarning(
                ldapException,
                "LDAP authentication failed for {Username} on {Server}",
                presentedUsername,
                opts.Server);

            return Task.FromResult(AuthenticateResult.Fail(ErrorCode));
        }

        var claims = BuildClaims(presentedUsername, roles);
        var identity = new ClaimsIdentity(claims, authenticationType: Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static IEnumerable<Claim> BuildClaims(string username, IReadOnlyList<string> roles)
    {
        yield return new Claim(ClaimTypes.Name, username);
        yield return new Claim(ClaimTypes.NameIdentifier, username);

        foreach (var role in roles)
        {
            yield return new Claim(ClaimTypes.Role, role);
        }
    }
}
