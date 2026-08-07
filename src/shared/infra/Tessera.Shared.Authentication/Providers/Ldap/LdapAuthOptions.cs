using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Tessera.Shared.Kernel.Configuration.Paths;

namespace Tessera.Shared.Authentication.Providers.Ldap;

/// <summary>
///     Options for the LDAP authentication provider. Backed by
///     <c>System.DirectoryServices.Protocols.LdapConnection</c> (BCL,
///     cross-platform since .NET 6). Required fields per ADR-0001
///     Decision 2 (<c>system.directory.services.protocols</c>).
///     <para>
///         Credentials are sourced from a bound configuration value —
///         either literal or a <see cref="SecretReference" />
///         (<c>env:VAR</c>, <c>file:/path</c>) resolved at startup by
///         <see cref="ResolveLdapCredentials" />. Holding credentials in
///         an <see cref="AuthenticationSchemeOptions" /> base is the
///         framework-idiomatic shape; post-configure guarantees the
///         resolved literal is in place before the first request, so the
///         handler does not need to read the environment at runtime.
///     </para>
///     <para>
///         Authentication mode: search-and-bind. The handler uses the
///         service account (bind_dn + bind_password) to look up the user
///         by <see cref="UserFilter" />, then attempts a second bind with
///         the user DN + presented password. This matches the enterprise
///         pattern where user DNs are scattered across multiple OUs and
///         can't be templated.
///     </para>
/// </summary>
public sealed class LdapAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>Configuration section name in <c>tessera.toml</c>.</summary>
    public const string SectionName = "auth.providers.ldap";

    /// <summary>
    ///     LDAP server URL. <c>ldaps://host:636</c> for SSL,
    ///     <c>ldap://host:389</c> for startTLS or plain.
    ///     Bound from <c>[auth.providers.ldap]/server</c>.
    /// </summary>
    [Required]
    public Uri Server { get; internal set; } = new("ldap://localhost:389");

    /// <summary>
    ///     Search base — the directory sub-tree under which user
    ///     accounts live. Bound from <c>[auth.providers.ldap]/base_dn</c>.
    ///     Example: <c>"dc=example,dc=com"</c>.
    /// </summary>
    [Required]
    public string BaseDn { get; internal set; } = "";

    /// <summary>
    ///     Service-account distinguished name used for the lookup phase.
    ///     Typically a read-only bind. Bound from
    ///     <c>[auth.providers.ldap]/bind_dn</c>; can be a literal DN or a
    ///     <see cref="SecretReference" />.
    /// </summary>
    [Required]
    public string BindDn { get; internal set; } = "";

    /// <summary>
    ///     Service-account password, populated from
    ///     <c>[auth.providers.ldap]/bind_password</c> via
    ///     <see cref="ResolveLdapCredentials.Apply" /> (PostConfigure).
    ///     <c>internal set</c> so the same-assembly hook can mutate it
    ///     after SecretReference resolution; external callers see
    ///     init-only semantics through the public installer.
    /// </summary>
    public string? BindPassword { get; internal set; }

    /// <summary>
    ///     LDAP search filter used to find the user, with <c>{0}</c>
    ///     substituted by the user identifier. Typical values:
    ///     <c>(uid={0})</c> (RFC 2307 / OpenLDAP),
    ///     <c>(sAMAccountName={0})</c> (Active Directory),
    ///     <c>(cn={0})</c> (rare). Bound from
    ///     <c>[auth.providers.ldap]/user_filter</c>.
    /// </summary>
    [Required]
    public string UserFilter { get; internal set; } = "(uid={0})";

    /// <summary>
    ///     LDAP attribute whose values become role names on the resulting
    ///     principal. AD default <c>"memberOf"</c>; OpenLDAP typically
    ///     <c>"memberOf"</c> or <c>"posixMemberOf"</c>.
    ///     Bound from <c>[auth.providers.ldap]/role_attribute</c>.
    /// </summary>
    [Required]
    public string RoleAttribute { get; internal set; } = "memberOf";

    /// <summary>
    ///     Header carrying the basic-auth credential.
    ///     Default <c>"Authorization"</c> (Basic auth).
    /// </summary>
    public string AuthorizationHeader { get; internal set; } = "Authorization";
}
