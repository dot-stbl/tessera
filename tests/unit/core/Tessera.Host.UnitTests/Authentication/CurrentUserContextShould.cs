using System.Security.Claims;
using Tessera.Shared.Authentication.Context;

namespace Tessera.Host.UnitTests.Authentication;

/// <summary>
///     Tests for <see cref="CurrentUserContext" /> — the scoped
///     per-request view the middleware populates from the chosen
///     scheme's <see cref="ClaimsPrincipal" />.
/// </summary>
public sealed class CurrentUserContextShould
{
    /// <summary>
    ///     Default state (before <see cref="CurrentUserContext.Initialize" />
    ///     is called) reports <c>IsAuthenticated=false</c> and
    ///     <c>TenantId="0"</c> per MVP-01 single-tenant default.
    /// </summary>
    [Fact]
    public void HaveSensibleDefaultStateBeforeInitialize()
    {
        var context = new CurrentUserContext();

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.DisplayName);
        Assert.Empty(context.Roles);
        Assert.Equal(CurrentUserContext.DefaultTenantId, context.TenantId);
    }

    /// <summary>
    ///     After <see cref="CurrentUserContext.Initialize" /> with a
    ///     populated <see cref="ClaimsPrincipal" />, every property
    ///     reflects the underlying claims: <c>sub</c> for UserId,
    ///     <c>preferred_username</c> for DisplayName, claim types
    ///     <c>role</c> + <see cref="ClaimTypes.Role" /> for Roles,
    ///     <c>tenant</c> claim for TenantId.
    /// </summary>
    [Fact]
    public void ReflectClaimsAfterInitialize()
    {
        var claims = new[]
        {
            new Claim("sub", "user-1234"),
            new Claim(ClaimTypes.Name, "user-1234"),
            new Claim("preferred_username", "alice@example.com"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("role", "operator"),
            new Claim("tenant", "acme"),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "test-scheme");
        var context = new CurrentUserContext();

        context.Initialize(new ClaimsPrincipal(identity));

        Assert.True(context.IsAuthenticated);
        Assert.Equal("user-1234", context.UserId);
        Assert.Equal("alice@example.com", context.DisplayName);
        Assert.Equal(ExpectedRolesFull, context.Roles);
        Assert.Equal("acme", context.TenantId);
    }

    /// <summary>
    ///     When <c>sub</c> + <c>preferred_username</c> are absent,
    ///     <see cref="ICurrentUserContext.UserId" /> and
    ///     <see cref="ICurrentUserContext.DisplayName" /> fall back to
    ///     <see cref="ClaimTypes.Name" /> (LDAP / AdminBearer style).
    /// </summary>
    [Fact]
    public void FallBackToNameWhenSubAndPreferredUsernameAreAbsent()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "fallback-name"),
            new Claim("cn", "Common Name"),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "test-scheme");
        var context = new CurrentUserContext();

        context.Initialize(new ClaimsPrincipal(identity));

        Assert.Equal("fallback-name", context.UserId);
        Assert.Equal("fallback-name", context.DisplayName);
    }

    /// <summary>
    ///     Preferred display rule is
    ///     <c>preferred_username</c> &gt; <c>name</c> &gt;
    ///     <c>cn</c>. Real-world Keycloak tokens carry both
    ///     <c>sub</c> and <c>preferred_username</c>; this test pins
    ///     the precedence so a future Keycloak config tweak that
    ///     drops one claim does not silently swap which one wins.
    /// </summary>
    [Fact]
    public void PreferPreferredUsernameOverClaimNameForDisplay()
    {
        var claims = new[]
        {
            new Claim("sub", "user-9999"),
            new Claim(ClaimTypes.Name, "legacy-name"),
            new Claim("preferred_username", "alice@example.com"),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "test-scheme");
        var context = new CurrentUserContext();

        context.Initialize(new ClaimsPrincipal(identity));

        Assert.Equal("user-9999", context.UserId);
        Assert.Equal("alice@example.com", context.DisplayName);
    }

    /// <summary>
    ///     Single-tenant MVP-01 default — no <c>tenant</c> claim
    ///     means TenantId resolves to the
    ///     <see cref="CurrentUserContext.DefaultTenantId" />
    ///     constant (<c>"0"</c>). The middleware must always
    ///     produce a string so that the <c>TenantId</c> property
    ///     is never <c>null</c> for downstream consumers.
    /// </summary>
    [Fact]
    public void UseDefaultTenantIdWhenNoTenantClaim()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice")],
            authenticationType: "test-scheme");
        var context = new CurrentUserContext();

        context.Initialize(new ClaimsPrincipal(identity));

        Assert.Equal(CurrentUserContext.DefaultTenantId, context.TenantId);
    }

    /// <summary>
    ///     The same role emitted under <see cref="ClaimTypes.Role" />
    ///     and the <c>"role"</c> short name is de-duplicated.
    ///     Distinct values stay distinct.
    /// </summary>
    [Fact]
    public void DeDuplicateRolesAcrossClaimTypes()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("role", "admin"),
            new Claim("role", "operator"),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "test-scheme");
        var context = new CurrentUserContext();

        context.Initialize(new ClaimsPrincipal(identity));

        Assert.Equal(ExpectedRolesAdminOperator, context.Roles);
    }

    private static readonly string[] ExpectedRolesFull = ["admin", "operator"];

    private static readonly string[] ExpectedRolesAdminOperator = ["admin", "operator"];

    /// <summary>
    ///     Double-initialize is a misuse: middleware should only
    ///     call <see cref="CurrentUserContext.Initialize" /> once
    ///     per request. We surface accidental re-entry as an
    ///     exception rather than silently overwriting state.
    /// </summary>
    [Fact]
    public void ThrowOnDoubleInitialize()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice")],
            authenticationType: "test-scheme");
        var context = new CurrentUserContext();

        context.Initialize(new ClaimsPrincipal(identity));

        Assert.Throws<InvalidOperationException>(
            () => context.Initialize(new ClaimsPrincipal(identity)));
    }
}
