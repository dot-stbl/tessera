# Multi-tenancy

> **Decision (locked 2026-07-19):** Single-tenant, config-time.

Tessera MVP is single-tenant. The tenant ID is **hardcoded in `tessera.toml`**,
not derived from request headers or user identity. One Tessera deployment = one
tenant.

## Why single-tenant

- Self-hosted APM deployments are typically one tenant (one team, one org).
- Simplifies config, auth, and Refit clients (no dynamic tenant resolution).
- Matches Grafana's default deployment model (multi-tenancy is a separate
  product feature in Grafana Cloud, not in self-hosted).

## Tenant in URL

Victoria exposes tenant as path prefix: `/select/<tenant>/...`. Tessera sends
the configured tenant on every call:

```
GET http://vt:10428/select/0/jaeger/api/services
GET http://vl:9428/select/0/logsql/query?query=...
```

## Configuration

```toml
[victoria]
tenant = "0"            # single tenant, MVP default
```

`Tenant` is a required option with default `"0"`. Validator fails startup if
empty.

## Refit client pattern

```csharp
namespace Tessera.Modules.Traces;

public sealed record ServicesResponse(string[] Data);

public interface IVictoriaTracesClient
{
    [Get("/select/{tenant}/jaeger/api/services")]
    Task<ServicesResponse> GetServicesAsync(
        string tenant,
        CancellationToken ct);

    [Get("/select/{tenant}/jaeger/api/services/{service}/operations")]
    Task<OperationsResponse> GetOperationsAsync(
        string tenant,
        string service,
        CancellationToken ct);

    [Get("/select/{tenant}/jaeger/api/traces")]
    Task<TracesResponse> SearchTracesAsync(
        string tenant,
        [Query] string? service,
        [Query] string? operation,
        [Query] long? start,
        [Query] long? end,
        [Query] string? minDuration,
        [Query] string? maxDuration,
        [Query] int? limit,
        CancellationToken ct);

    [Get("/select/{tenant}/jaeger/api/traces/{traceId}")]
    Task<TraceResponse> GetTraceAsync(
        string tenant,
        string traceId,
        CancellationToken ct);
}
```

Tenant is a **method parameter**, not captured in the base URL. The host
handler reads it from `IOptions<VictoriaOptions>` and passes on every call.

## Handler pattern

```csharp
public sealed class GetServicesHandler(
    IVictoriaTracesClient client,
    IOptions<VictoriaOptions> options)
{
    public async Task<ServicesResponse> HandleAsync(CancellationToken ct)
    {
        var tenant = options.Value.Tenant;
        return await client.GetServicesAsync(tenant, ct);
    }
}
```

`IOptions<VictoriaOptions>` is registered in `Tessera.Host` composition root.
Handlers in modules receive it via DI.

## Anti-patterns

```csharp
// ❌ WRONG — hardcoded tenant in Refit attribute
[Get("/select/0/jaeger/api/services")]
Task<ServicesResponse> GetServicesAsync(CancellationToken ct);

// ❌ WRONG — tenant from request header (multi-tenant MVP not supported)
var tenant = ctx.Request.Headers["X-Tessera-Tenant"].FirstOrDefault();

// ❌ WRONG — tenant from user claim (requires auth infrastructure)
var tenant = ctx.User.FindFirst("tenant")?.Value;
```

## Future: path to multi-tenant (stretch)

When tessera grows to multi-tenant (v0.2+), the path is:

1. Add OIDC/LDAP providers (see `security/auth-model.md`)
2. Extract tenant from user claim: `ctx.User.FindFirst("tenant")?.Value`
3. Add tenant allowlist in `tessera.toml` for security
4. Add tenant selector in UI
5. Refit clients remain unchanged (tenant still a parameter)

The Refit pattern is **already correct** — we just change WHERE the tenant
value comes from (config → claim). No breaking changes to clients.

## Test requirements

- `Tessera.Modules.Traces.Unit/`:
  - `GetServicesHandlerTests.Passes_Tenant_From_Options`
  - `IVictoriaTracesClient` mocked via NSubstitute, verify tenant arg
- Integration: end-to-end request → response → verify URL contains correct tenant

## Related docs

- `security/auth-model.md` — future tenant from claim (stretch)
- `architecture.md` — overall architecture
- `../rules/coding/naming-and-types.md` — sealed classes, primary constructors