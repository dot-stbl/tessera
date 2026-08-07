using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Core;

namespace Tessera.Shared.Authentication.Providers.Admin;

/// <summary>
///     Kernel-level face of the Admin Bearer authentication scheme.
///     <para>
///         For Guest this is a direct <see cref="AuthenticateResult.Success" />
///         construction (anonymous trivially succeeds); for AdminBearer this
///         delegates to <see cref="IAuthenticationService" /> resolved from
///         <c>HttpContext.RequestServices</c>, which the framework's
///         <c>AddScheme&lt;AdminBearerOptions, AdminBearerHandler&gt;</c>
///         registration handles. Token validation stays in the hardened
///         platform handler — we never hand-roll JWT/bearer parsing
///         (banned by the global <c>csharp/technology-stack.md</c> matrix).
///     </para>
///     <para>
///         Used by <c>ICurrentUserContext</c> to materialize the
///         principal without going through <c>[Authorize]</c> — for
///         endpoints that <em>read</em> identity rather than <em>gate</em>
///         access.
///     </para>
/// </summary>
public sealed class AdminBearerAuthProvider : IAuthProvider
{
    /// <summary>Scheme name constant.</summary>
    public const string NameConst = "admin-bearer";

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
        // Per-key binding happens via AdminBearerOptions + PostConfigure
        // (ResolveAdminBearerToken.Apply), not here. Nothing to do.
        _ = section;
    }
}
