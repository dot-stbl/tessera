namespace Tessera.Shared.Authentication.Context;

/// <summary>
///     Scoped per-request view onto the authenticated principal.
///     Populated once at the start of each request by
///     <see cref="CurrentUserContextMiddleware" /> from
///     <c>HttpContext.User</c>. Handlers and feature code depend on this
///     shape instead of reaching into <c>HttpContext.User</c> directly —
///     keeps them unit-testable without HTTP plumbing.
///     <para>
///         Per ADR-0001 Decision 1 — the framework registers each auth
///         scheme (guest, admin-bearer, ldap, keycloak) and the
///         middleware calls the default scheme via
///     <c>HttpContext.AuthenticateAsync</c>. The result is folded into a
///     single <see cref="System.Security.Claims.ClaimsPrincipal" /> so
///         downstream policy checks see exactly one identity.
///     </para>
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    ///     <c>true</c> when the request carries a non-anonymous
    ///     principal. Always <c>true</c> under the
    ///     <c>"guest"</c> scheme (Guest emits an authenticated
    ///     principal with role <c>"guest"</c>); <c>true</c>
    ///     under admin-bearer / ldap / keycloak.
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    ///     Stable user identifier. The claim preferred for this
    ///     value is the JWT <c>sub</c> claim (Keycloak / OIDC),
    ///     falling back to <see cref="System.Security.Claims.ClaimTypes.NameIdentifier" />,
    ///     then <see cref="System.Security.Claims.ClaimTypes.Name" />.
    ///     Returns <c>null</c> for fully anonymous principals.
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    ///     Human-readable display name. Uses
    ///     <c>"preferred_username"</c> (Keycloak),
    ///     <c>"name"</c>, or the LDAP <c>"cn"</c> attribute. Falls
    ///     back to <see cref="UserId" /> when no display name is
    ///     present.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    ///     Roles attached to the principal across all schemes
    ///     (claim type <c>"role"</c> plus
    ///     <see cref="System.Security.Claims.ClaimTypes.Role" />).
    ///     Includes <c>"guest"</c> when the Guest scheme
    ///     authenticated.
    /// </summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>
    ///     Tenant identifier. MVP-01 is single-tenant — returns
    ///     <c>"0"</c> always. Multi-tenant (ADR-0001 §11 stretch)
    ///     will source this from a <c>"tenant"</c> claim populated
    ///     by the auth provider (e.g.
    ///     <c>memberOf</c> in LDAP, <c>tenant</c> in custom Keycloak
    ///     mapper).
    /// </summary>
    public string? TenantId { get; }
}
