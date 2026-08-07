using System.Security.Claims;

namespace Tessera.Shared.Authentication.Context;

/// <summary>
///     Scoped implementation of <see cref="ICurrentUserContext" />.
///     Mutated by <see cref="CurrentUserContextMiddleware" /> once at
///     the start of each request; thereafter, handlers read
///     <see cref="IsAuthenticated" />, <see cref="UserId" />,
///     <see cref="DisplayName" />, <see cref="Roles" />, and
///     <see cref="TenantId" />. The class is mutable in the
///     <c>Initialize</c> scope only — after middleware initialization
///     the state is read-only for the rest of the request.
///     <para>
///         Held as <c>Scoped</c> per
///         <c>csharp/di-lifetimes.md</c> §3 — per-request state
///         (<c>ICurrentUserContext</c>) and
///         <c>csharp/multi-provider-auth.md</c> §5.
///     </para>
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    /// <summary>
    ///     Default tenant identifier for MVP-01 deployments
    ///     (single-tenant). Multi-tenant will source this from
    ///     a <c>"tenant"</c> claim once the auth abstraction
    ///     grows a mapping layer.
    /// </summary>
    public const string DefaultTenantId = "0";

    private ClaimsPrincipal? principal;

    /// <inheritdoc />
    public bool IsAuthenticated => principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public string? UserId => principal?.FindFirstValue("sub")
        ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal?.FindFirstValue(ClaimTypes.Name);

    /// <inheritdoc />
    /// <remarks>
    ///     Both spellings of the name claim are probed. A JWT with inbound claim
    ///     mapping disabled carries the short <c>name</c>; LDAP and AdminBearer
    ///     identities are built with <see cref="ClaimTypes.Name" />, the long
    ///     WS-Federation URI. Checking only the short form let <c>cn</c> win for
    ///     those identities, so an LDAP user's display name came out as their
    ///     directory common name — <see cref="UserId" /> already probed both.
    /// </remarks>
    public string? DisplayName => principal?.FindFirstValue("preferred_username")
        ?? principal?.FindFirstValue("name")
        ?? principal?.FindFirstValue(ClaimTypes.Name)
        ?? principal?.FindFirstValue("cn")
        ?? UserId;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles
    {
        get
        {
            if (principal is null)
            {
                return [];
            }

            var roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var claim in principal.Claims)
            {
                if (claim.Type is ClaimTypes.Role or "role" or "roles")
                {
                    roles.Add(claim.Value);
                }
            }

            return roles.Count == 0 ? [] : roles.ToArray();
        }
    }

    /// <inheritdoc />
    public string? TenantId => principal?.FindFirstValue("tenant")
        ?? principal?.FindFirstValue("tenant_id")
        ?? DefaultTenantId;

    /// <summary>
    ///     Populates the context for the current request. Called by
    ///     <see cref="CurrentUserContextMiddleware" /> after
    ///     <c>HttpContext.AuthenticateAsync</c>; throws
    ///     <see cref="InvalidOperationException" /> when called more
    ///     than once per request to surface accidental re-entry.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Initialize(ClaimsPrincipal principal)
    {
        if (this.principal is not null)
        {
            throw new InvalidOperationException(
                "CurrentUserContext is already initialized for this request; "
                + "the middleware should only initialize once.");
        }

        this.principal = principal;
    }
}
