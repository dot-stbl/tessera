# Auth model

> **Decision (locked 2026-07-19):** MVP-01 ships **guest (anonymous read) + admin bearer optional** (handler returns NoResult when `TESSERA_ADMIN_TOKEN` unset — admin endpoints reject 401, host starts cleanly). Future: OIDC, LDAP.

Tessera has two auth tiers: **guest** (anonymous read access to APM data) and
**admin** (bearer token for settings + dashboard editing). Future phases
add OIDC (Google, Keycloak) and LDAP as pluggable authentication schemes.

## MVP scope (v0.1)

| Tier | Access | Auth |
|------|--------|------|
| **Guest** | All read endpoints (`/api/v1/health`, `/api/v1/services`, `/api/v1/traces`, `/api/v1/traces/{traceId}`, `/api/v1/logs`) | None — anonymous |
| **Admin** | Write endpoints (settings, dashboard edit, future ops) — **none in MVP-01; scheme wired for future use** | Bearer token via `TESSERA_ADMIN_TOKEN` env var (optional in MVP-01) |

Stretch (v0.2+): OIDC (Google, Keycloak), LDAP, multi-tenant tenant-from-claim.

## Endpoint authorization (MVP-01)

```csharp
// Tessera uses controllers ([ApiController] + ControllerBase, Plexor convention).
// Authorization is per-action attribute, applied before the action method runs.
// `[AllowAnonymous]` opts an action out of the class-level `[Authorize]` policy.
// `[Authorize(Policy = "admin")]` gates an action behind the admin bearer scheme.

[ApiController]
[Route(ApiRoutes.Traces)]                          // "/api/v1/traces"
public sealed class TracesController(...) : ControllerBase
{
    [HttpGet("{traceId:length(32)}")]
    public async Task<ActionResult<GetTraceResponse>> GetAsync(...) { ... }
    // ^ MVP-01: NO [Authorize] — guest anonymous access.

    [HttpPost]
    [Authorize(Policy = "admin")]                   // MVP-02+ example
    public async Task<ActionResult<TraceSummary>> CreateAsync(...) { ... }
}
```

**MVP-01 ships with all endpoints anonymous** (no `[Authorize]` attribute).
The admin scheme is **registered unconditionally** in the composition root
(`AddTesseraAdminAuthentication(builder.Configuration)` in `Program.cs`),
so future `[Authorize(Policy = "admin")]` annotations work без host changes.

## Admin bearer authentication

Source (Phase 3 commit `29485dc`):

```
src/shared/Tessera.Shared.Authentication/
├── Admin/
│   ├── AdminBearerHandler.cs           AuthenticationHandler<AdminBearerOptions> comparing bearer
│   ├── AdminBearerOptions.cs          AuthenticationSchemeOptions { string? AdminToken }
│   ├── AdminBearerConstants.cs        internal static — scheme name + header + prefix constants
│   └── AdminBearerCryptography.cs     internal static — length-equalizing FixedTimeEquals
└── AuthenticationInstallerExtensions.cs   AddTesseraAdminAuthentication(IServiceCollection, IConfiguration)
```

```csharp
// Tessera.Shared.Authentication.Admin.AdminBearerHandler.cs

public sealed class AdminBearerHandler(
    IOptionsMonitor<AdminBearerOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AdminBearerOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = Options.AdminToken;
        if (string.IsNullOrEmpty(expected))
        {
            // MVP-01: scheme disabled when env var unset.
            // Admin endpoints reject 401, host still starts.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(AdminBearerConstants.AuthorizationHeader, out var raw))
            return Task.FromResult(AuthenticateResult.NoResult());

        var headerValue = raw.ToString();
        if (!headerValue.StartsWith(AdminBearerConstants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Invalid scheme."));

        var presented = headerValue[AdminBearerConstants.BearerPrefix.Length..].Trim();
        if (!AdminBearerCryptography.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(expected)))
            return Task.FromResult(AuthenticateResult.Fail("Invalid token."));

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "admin"),
            ],
            AdminBearerConstants.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), AdminBearerConstants.SchemeName)));
    }
}
```

`AdminBearerCryptography.FixedTimeEquals` — length-equalizing constant-time byte
comparison via `CryptographicOperations.FixedTimeEquals` after pre-checking
length (mismatch short-circuits to false without leaking where the first
differing byte is via timing). Defined in file-local helper
(`code-shape.md §9` ban — `private` static methods must be extracted to sibling
`internal static` class).

## Registration (composition root)

```csharp
// src/host/Tessera.Host/Program.cs
builder.Services.AddTesseraAdminAuthentication(builder.Configuration);
// internally:
AddAuthentication("admin")
    .AddScheme<AdminBearerOptions, AdminBearerHandler>("admin", options =>
    {
        options.AdminToken = configuration["TESSERA_ADMIN_TOKEN"]
            ?? Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN");
    });
builder.Services.AddAuthorization();  // useAuthentication/Authorization in pipeline
```

When `TESSERA_ADMIN_TOKEN` is unset (default MVP-01 deploy): handler returns
`NoResult()` for every request — admin scheme effectively disabled but host
starts cleanly. No `[Authorize(Policy="admin")]` annotations exist in MVP-01,
so this is forward-compatibility.

## Admin token configuration

MVP-01 supports the token only via env var (no `[auth]` config section yet).
Resolution: `configuration["TESSERA_ADMIN_TOKEN"] ?? Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN")`.

The env var can alternatively be referenced inline in TOML via secret syntax
`env:TESSERA_ADMIN_TOKEN` (slightly redundant for this scheme — same resolution
happens — but documented for parity with future secrets in config). See
`.agents/docs/architecture/config-format.md` for the broader secret scheme.

Generate token at first deployment:

```bash
openssl rand -hex 32                    # 64-char hex token
# Store in env: TESSERA_ADMIN_TOKEN=abc123...
export TESSERA_ADMIN_TOKEN=abc123...
```

**MVP does NOT support token rotation or revocation.** Restart to change.
The handler reads from `IOptionsMonitor<AdminBearerOptions>` so reload on
change is supported (Phase 5+ may wire `ReloadOnChange` for TOML config), but
runtime token rotation is out of scope.

## Client usage (SPA → admin endpoints, MVP-02)

```typescript
// web/apps/console/src/api/client.ts (MVP-02)
const adminToken = localStorage.getItem('tessera.admin_token') ?? '';

export async function fetchAdmin<T>(url: string, init?: RequestInit): Promise<T>
{
    const response = await fetch(url, {
        ...init,
        headers: {
            ...init?.headers,
            Authorization: `Bearer ${adminToken}`,
            'Content-Type': 'application/json',
        },
    });
    if (response.status === 401) {
        window.location.href = '/settings?error=token_invalid';
        throw new Error('Unauthorized');
    }
    return response.json();
}
```

MVP-01: SPA runs on hand-written mock data fallback — admin endpoints don't
exist yet to call.

## Future providers (v0.2+)

Stretch architecture uses `Microsoft.AspNetCore.Authentication` with multiple
schemes registered conditionally:

```toml
# Stretch config — NOT MVP
[auth.providers.openid_connect]
enabled = true
authority = "https://accounts.google.com"
client_id = "..."
client_secret = "env:GCP_CLIENT_SECRET"
scopes = ["openid", "profile", "email"]

[auth.providers.ldap]
enabled = true
server = "ldap://ldap.server.com"
base_dn = "dc=example,dc=com"
bind_dn = "env:LDAP_BIND_DN"
bind_password = "env:LDAP_BIND_PASSWORD"
```

Provider registration (stretch):

```csharp
if (authOptions.Providers?.OpenIdConnect?.Enabled == true)
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect("oidc", opts =>
        {
            opts.Authority = authOptions.Providers.OpenIdConnect.Authority;
            opts.ClientId = authOptions.Providers.OpenIdConnect.ClientId;
            opts.ClientSecret = ResolveSecret(
                authOptions.Providers.OpenIdConnect.ClientSecret);
            opts.Scope.AddRange(authOptions.Providers.OpenIdConnect.Scopes);
        });
}

if (authOptions.Providers?.Ldap?.Enabled == true)
{
    builder.Services.AddAuthentication()
        .AddScheme<AuthenticationSchemeOptions, LdapAuthHandler>(
            "ldap", opts => { /* bind config */ });
}
```

## Multi-tenancy with auth (stretch)

When OIDC/LDAP added, tenant ID can come from claim:

```csharp
public sealed class TenantFromClaimsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var tenant = ctx.User.FindFirst("tenant")?.Value;
        if (!string.IsNullOrEmpty(tenant))
            ctx.Items["Tenant"] = tenant;
        await next(ctx);
    }
}
```

But MVP uses **single-tenant, config-time** (see `architecture/multi-tenancy.md`).
Hardcoded `0` throughout — no tenant routing middleware in MVP-01.

## Anti-patterns

```csharp
// ❌ WRONG — hardcoded admin token in source
opts.AdminToken = "abc123-hardcoded";

// ❌ WRONG — auth check in handler instead of endpoint metadata
[HttpPost]
public async Task<IActionResult> CreateAsync([FromBody] Dashboard d, CancellationToken ct)
{
    if (!User.Identity?.IsAuthenticated ?? true)
        return Unauthorized();
    // ...
}

// ❌ WRONG — logging tokens
logger.LogInformation("Admin token: {Token}", token);

// ❌ WRONG — guest endpoint with auth requirement in MVP-01
[HttpGet]
[Authorize(Policy = "admin")]
public async Task<ActionResult<TraceSummary>> GetAsync(...) { ... }

// ❌ WRONG — non-constant-time comparison (timing-attack recovery)
if (presented == expected)  // string equality leaks position of first differing byte
    return Success();

// ❌ WRONG — own authentication per-module (cross-cutting concern belongs in Tessera.Shared.Authentication)
namespace Tessera.Modules.TraceDashboard.Auth { ... }
```

## Test requirements (when tests un-block)

- `tests/unit/core/Tessera.Host.UnitTests/`:
  - `AdminBearerHandlerTests.InvalidToken_ReturnsFail`
  - `AdminBearerHandlerTests.MissingHeader_ReturnsNoResult`
  - `AdminBearerHandlerTests.UnsetEnvVar_ReturnsNoResult`
  - `AdminBearerHandlerTests.FixedTimeEquals_PreventsTimingAttack`
  - `AdminBearerHandlerTests.ValidToken_CreatesAdminClaims`
- Integration: `WebApplicationFactory<Program>` with real bearer flow, asserting 401/200 split

Currently **blocked by .NET 10.0.110 SDK testhost bug** (see `.agents/STATE.md`),
runs when SDK ≥ 10.0.200.

## Related docs

- `architecture/multi-tenancy.md` — single-tenant MVP, future claim-from-tenant (stretch)
- `architecture/config-format.md` — TOML config + `env:VAR` / `file:/path` secret prefixes
- `../rules/coding/anti-patterns.md` — no hardcoded secrets, no auth in handler body
- `../rules/csharp/api-design.md` — `[Authorize]` per-action attribute convention
- `.agents/docs/architecture.md` — wire format (RFC 9457 ProblemDetails для 401 из admin scheme)
- `.agents/HANDOFF.md` — Phase 3 commit `29485dc` (admin bearer composition)
- `.agents/STATE.md` — decision log + open questions