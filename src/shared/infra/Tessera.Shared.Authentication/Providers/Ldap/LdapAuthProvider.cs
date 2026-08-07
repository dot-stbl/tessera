using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tessera.Shared.Authentication.Core;

namespace Tessera.Shared.Authentication.Providers.Ldap;

/// <summary>
///     Kernel-level face of the LDAP authentication scheme.
///     <para>
///         Forwards <see cref="IAuthProvider.AuthenticateAsync" /> calls
///         to the framework's <see cref="IAuthenticationService" />
///         resolved from <c>HttpContext.RequestServices</c>. The actual
///         directory roundtrip happens in <see cref="LdapAuthHandler" />
///         via <see cref="LdapAuthenticationHelper" />, behind the
///         hardened ASP.NET auth pipeline. We don't hand-roll LDAP
///         — banned by the global <c>csharp/technology-stack.md</c> matrix.
///     </para>
/// </summary>
public sealed class LdapAuthProvider : IAuthProvider
{
    /// <summary>Scheme name constant.</summary>
    public const string NameConst = "ldap";

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
        // Backing options are validated by Microsoft.Extensions.Options
        // via [Required] data annotations + ValidateOnStart at the
        // installer call site; nothing to do here.
        _ = section;
    }
}
