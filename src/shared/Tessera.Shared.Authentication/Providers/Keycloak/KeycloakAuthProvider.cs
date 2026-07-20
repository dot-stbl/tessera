using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Core;

namespace Tessera.Shared.Authentication.Providers.Keycloak;

/// <summary>
///     Kernel-level face of the Keycloak JWT bearer authentication
///     scheme. Per ADR-0001 Decision 3 — Keycloak integrates through
///     <c>Microsoft.AspNetCore.Authentication.JwtBearer</c> with OIDC
///     discovery via <c>Authority</c>; we don't hand-roll JWT
///     validation.
///     <para>
///         Forwards <see cref="IAuthProvider.AuthenticateAsync" /> calls
///         to the framework's <see cref="IAuthenticationService" />
///         resolved from <c>HttpContext.RequestServices</c>. The actual
///         bearer-token validation — signature, expiry, audience,
///         issuer — happens inside the framework's
///     <c>JwtBearerHandler</c> registered via
///     <c>AddScheme&lt;KeycloakAuthOptions, JwtBearerHandler&gt;</c>.
///     </para>
/// </summary>
public sealed class KeycloakAuthProvider : IAuthProvider
{
    /// <summary>Scheme name constant.</summary>
    public const string NameConst = "keycloak";

    /// <inheritdoc />
    public string Name => NameConst;

    /// <inheritdoc />
    public Task<AuthenticateResult> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var authentication = context.RequestServices.GetService<IAuthenticationService>()
            ?? throw new InvalidOperationException(
                "IAuthenticationService is not registered; "
                + "call AddAuthentication() in the host composition root.");

        return authentication.AuthenticateAsync(context, Name);
    }

    /// <inheritdoc />
    public void ValidateOptions(IConfigurationSection section)
    {
        // [Required] data annotations + ValidateOnStart cover the
        // Authority presence check; nothing extra here.
        _ = section;
    }
}
