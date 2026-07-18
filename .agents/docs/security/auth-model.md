# Auth model

> **Decision (locked 2026-07-19):** Guest (anonymous read) + Admin (bearer token) in MVP. Future: OIDC, LDAP.

Tessera has two auth tiers: **guest** (anonymous read access to APM data) and
**admin** (bearer token for settings + dashboard editing). Future phases
add OIDC (Google, Keycloak) and LDAP as pluggable authentication schemes.

## MVP scope (v0.1)

| Tier | Access | Auth |
|------|--------|------|
| **Guest** | All read endpoints (`/api/traces/*`, `/api/logs/*`, `/api/services/*`, `/api/health`) | None — anonymous |
| **Admin** | All write endpoints (settings, dashboard edit, future ops) | Bearer token from config |

Stretch (v0.2+): OIDC (Google, Keycloak), LDAP.

## Endpoint authorization

```csharp
// src/host/Tessera.Host/Program.cs

// Guest endpoints — read-only APM data
app.MapGet("/api/traces", /* ... */).AllowAnonymous();
app.MapGet("/api/traces/{id}", /* ... */).AllowAnonymous();
app.MapGet("/api/logs", /* ... */).AllowAnonymous();
app.MapGet("/api/services", /* ... */).AllowAnonymous();
app.MapGet("/api/health", /* ... */).AllowAnonymous();

// Admin endpoints — require bearer token
app.MapPut("/api/settings", /* ... */).RequireAuthorization("admin");
app.MapPost("/api/dashboards", /* ... */).RequireAuthorization("admin");
app.MapPut("/api/dashboards/{id}", /* ... */).RequireAuthorization("admin");
app.MapDelete("/api/dashboards/{id}", /* ... */).RequireAuthorization("admin");

// Static SPA — guest (login is client-side for future providers)
app.MapFallbackToFile("index.html").AllowAnonymous();
```

## Admin bearer authentication

```csharp
// src/host/Tessera.Host/Auth/AdminBearerHandler.cs
namespace Tessera.Host.Auth;

public sealed class AdminBearerOptions : AuthenticationSchemeOptions
{
    public string? AdminToken { get; set; }
}

public sealed class AdminBearerHandler(
    IOptionsMonitor<AdminBearerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AdminBearerOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var raw = authHeader.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Invalid scheme"));

        var token = raw[7..].Trim();
        var expected = Options.AdminToken;

        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(expected)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "admin")],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

## Registration

```csharp
// src/host/Tessera.Host/Program.cs
var adminToken = Environment.GetEnvironmentVariable("TESSERA_ADMIN_TOKEN")
    ?? throw new InvalidOperationException("TESSERA_ADMIN_TOKEN is required");

builder.Services.AddAuthentication("admin")
    .AddScheme<AdminBearerOptions, AdminBearerHandler>("admin", opts =>
    {
        opts.AdminToken = adminToken;
    });

builder.Services.AddAuthorization();
```

## Admin token configuration

```toml
[auth]
guest_enabled = true                     # MVP: always true
admin_token_source = "env:TESSERA_ADMIN_TOKEN"
```

Generate token at first deployment:

```bash
openssl rand -hex 32                    # 64-char hex token
# Store in env: TESSERA_ADMIN_TOKEN=abc123...
```

**MVP does NOT support token rotation or revocation.** Restart to change.

## Client usage (SPA → admin endpoints)

```typescript
// web/apps/console/src/api/client.ts
const adminToken = localStorage.getItem('tessera.admin_token') ?? '';

export async function fetchAdmin<T>(
  url: string,
  init?: RequestInit
): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: {
      ...init?.headers,
      Authorization: `Bearer ${adminToken}`,
      'Content-Type': 'application/json',
    },
  });
  if (response.status === 401) {
    // token invalid — redirect to settings
    window.location.href = '/settings?error=token_invalid';
    throw new Error('Unauthorized');
  }
  return response.json();
}
```

## Future providers (v0.2+)

Stretch architecture uses `Microsoft.AspNetCore.Authentication` with multiple
schemes registered conditionally:

```toml
# Stretch config — NOT MVP
[auth.providers.openid_connect]
enabled = true
authority = "https://accounts.google.com"
client_id = "..."
client_secret_source = "env:GCP_CLIENT_SECRET"
scopes = ["openid", "profile", "email"]

[auth.providers.ldap]
enabled = true
server = "ldap://ldap.example.com"
base_dn = "dc=example,dc=com"
bind_dn_source = "env:LDAP_BIND_DN"
bind_password_source = "env:LDAP_BIND_PASSWORD"
```

Provider registration in `Program.cs` (stretch):

```csharp
if (authOptions.Providers?.OpenIdConnect?.Enabled == true)
{
    builder.Services.AddAuthentication()
        .AddOpenIdConnect("oidc", opts =>
        {
            opts.Authority = authOptions.Providers.OpenIdConnect.Authority;
            opts.ClientId = authOptions.Providers.OpenIdConnect.ClientId;
            opts.ClientSecret = ResolveSecret(
                authOptions.Providers.OpenIdConnect.ClientSecretSource);
            opts.Scope.AddRange(authOptions.Providers.OpenIdConnect.Scopes);
        });
}

if (authOptions.Providers?.Ldap?.Enabled == true)
{
    builder.Services.AddAuthentication()
        .AddScheme<AuthenticationSchemeOptions, LdapAuthHandler>(
            "ldap",
            opts => { /* bind config */ });
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

## Audit logging (stretch)

Sensitive admin operations should be logged for audit:

```csharp
public sealed class AdminAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.User.Identity?.IsAuthenticated ?? true)
        {
            await next(ctx);
            return;
        }

        var path = ctx.Request.Path;
        var method = ctx.Request.Method;

        await next(ctx);

        if (IsSensitive(method, path))
        {
            logger.LogInformation(
                "Admin {User} performed {Method} {Path} -> {StatusCode}",
                ctx.User.Identity.Name, method, path, ctx.Response.StatusCode);
        }
    }
}
```

## Anti-patterns

```csharp
// ❌ WRONG — hardcoded admin token in source
opts.AdminToken = "abc123-hardcoded";

// ❌ WRONG — auth check in handler instead of endpoint metadata
app.MapPost("/api/dashboards", (Dashboard d) =>
{
    if (!ctx.User.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();
    // ...
});

// ❌ WRONG — logging tokens
logger.LogInformation("Admin token: {Token}", token);

// ❌ WRONG — guest endpoint with auth requirement
app.MapGet("/api/traces", /* ... */).RequireAuthorization("admin");
```

## Test requirements

- `tests/unit/core/Tessera.Host.UnitTests/`:
  - `AuthTests.GuestEndpoints_AllowAnonymous`
  - `AuthTests.AdminEndpoints_RequireBearerToken`
  - `AdminBearerHandlerTests.InvalidToken_ReturnsFail`
  - `AdminBearerHandlerTests.MissingHeader_ReturnsNoResult`
  - `AdminBearerHandlerTests.FixedTimeEquals_PreventsTimingAttack`
- Integration: WebApplicationFactory with real bearer flow

## Related docs

- `architecture/multi-tenancy.md` — future tenant from claim (stretch)
- `architecture/config-format.md` — token source in TOML
- `../rules/coding/naming-and-types.md` — sealed classes, primary constructors