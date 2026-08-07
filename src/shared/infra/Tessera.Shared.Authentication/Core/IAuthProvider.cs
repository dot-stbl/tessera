using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Tessera.Shared.Authentication.Core;

/// <summary>
///     Authentication-provider abstraction for Tessera. Each concrete
///     provider (Guest, AdminBearer, LDAP, Keycloak) registers itself via
///     <c>AddScheme&lt;TOptions, THandler&gt;</c> so multiple schemes
///     coexist; <see cref="IAuthProvider" /> is the kernel abstraction the
///     rest of the app sees, not a specific <see cref="AuthenticationHandler{T}" />.
///     Per ADR-0001 Decision 1.
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    ///     Stable name used as the authentication scheme, the policy name,
    ///     and the <c>[auth.providers.&lt;name&gt;]</c> TOML section key.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Authenticate the supplied ASP.NET request and return the
    ///     resulting <see cref="AuthenticateResult" />. Returns
    ///     <see cref="AuthenticateResult.NoResult()" /> when this provider
    ///     cannot authenticate the request (e.g. anonymous read under
    ///     Guest; missing JWT under Keycloak).
    /// </summary>
    /// <remarks>
    ///     Implemented as a thin wrapper over the underlying
    ///     <see cref="AuthenticationHandler{T}" /> that the provider
    ///     registers via <c>AddScheme&lt;TOptions, THandler&gt;</c>. The
    ///     handler is the framework-bridged implementation; the provider is
    ///     the kernel-level facet exposed to handlers that need to resolve
    ///     auth state programmatically (e.g. ICurrentUserContext).
    /// </remarks>
    public Task<AuthenticateResult> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validate the <c>[auth.providers.&lt;name&gt;]</c> configuration
    ///     section at startup. Throws <see cref="OptionsValidationException" />
    ///     with dot.case <c>code</c> attribute when required keys are
    ///     missing. Called by <c>ValidateOnStart</c>; see
    ///     <see cref="TesseraAuthenticationOptions" />.
    /// </summary>
    public void ValidateOptions(IConfigurationSection section);
}
