using Microsoft.Extensions.Options;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Shared.Authentication.Providers.Ldap;

/// <summary>
///     PostConfigure hook for <see cref="LdapAuthOptions" />. Resolves the
///     <c>BindDn</c> and <c>BindPassword</c> raw values (which may be
///     <see cref="SecretReference" /> strings like <c>env:VAR</c> or
///     <c>file:/path</c>) at startup. Per ADR-0001 Decision 2 — LDAP uses
///     <c>System.DirectoryServices.Protocols</c> natively, no extra
///     abstraction layer.
///     <para>
///         Idempotent: if the value is already a literal DN or no
///         <see cref="SecretReference" /> prefix matches, the input
///         passes through unchanged. This keeps direct (non-secret)
///         deployments working without rewriting the TOML shape.
///     </para>
/// </summary>
internal static class ResolveLdapCredentials
{
    /// <summary>
    ///     The <see cref="IConfigureOptions{TOptions}" /> delegate wired
    ///     by <c>AddOptions&lt;LdapAuthOptions&gt;().PostConfigure(...)</c>.
    /// </summary>
    public static void Apply(LdapAuthOptions options)
    {
        var resolvedDn = SecretReference.Resolve(options.BindDn);
        if (!string.IsNullOrEmpty(resolvedDn))
        {
            options.BindDn = resolvedDn;
        }

        var resolvedPassword = SecretReference.Resolve(options.BindPassword);
        if (!string.IsNullOrEmpty(resolvedPassword))
        {
            options.BindPassword = resolvedPassword;
        }
    }
}
