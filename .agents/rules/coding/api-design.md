---
description: API design — minimal API endpoints, route naming, OpenAPI attributes, ProblemDetails for errors
globs: ["**/*.cs"]
always: true
---

# API design

Tessera uses **ASP.NET Core minimal API** (not controllers). Endpoints are
small, focused, and grouped by feature.

## 1. Minimal API over controllers

```csharp
// ✅ Tessera pattern — minimal API
public static class TracesEndpoint
{
    public static void MapTracesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/traces").WithTags("Traces");

        group.MapGet("/", ListTracesAsync);
        group.MapGet("/{traceId}", GetTraceAsync);
        group.MapGet("/{traceId}/logs", ListTraceLogsAsync);
    }

    private static async Task<Ok<TraceSummary[]>> ListTracesAsync(
        [AsParameters] ListTracesRequest request,
        IVictoriaTracesClient client,
        IOptions<VictoriaOptions> options,
        CancellationToken ct)
    {
        var tenant = options.Value.Tenant;
        var response = await client.SearchTracesAsync(
            tenant, request.Service, request.Operation,
            request.StartUnixMs, request.EndUnixMs,
            request.MinDuration, request.MaxDuration, request.Limit, ct);
        return TypedResults.Ok(ToTraceSummaries(response));
    }
}
```

**Why:** less boilerplate than controllers, faster compile, easier to
co-locate with module.

## 2. Endpoint organization — one file per feature

```
src/modules/Tessera.Modules.Traces/
├── Endpoints/
│   └── TracesEndpoint.cs        # MapTracesEndpoints + handler methods
```

**One file per endpoint group.** Avoid monolithic `Endpoints.cs`.

## 3. Route naming

```
GET    /api/traces                       # list
GET    /api/traces/{traceId}            # get single
GET    /api/traces/{traceId}/logs       # sub-resource (logs for trace)
POST   /api/traces                       # create (admin)
PUT    /api/traces/{traceId}            # update (admin)
DELETE /api/traces/{traceId}            # delete (admin)
```

- Plural nouns (`/traces`, `/logs`, `/services`)
- Lowercase kebab-case for multi-word: `/api/trace-logs`, `/api/service-maps`
- Sub-resources via nested path: `/api/traces/{traceId}/logs`
- Auth via attributes, not path: `/api/admin/...` is **wrong**, use `[Authorize]`

## 4. Request/Response DTOs

```csharp
// Request — query parameters via [AsParameters]
public sealed record ListTracesRequest(
    string? Service,
    string? Operation,
    long StartUnixMs,
    long EndUnixMs,
    int? MinDurationMs,
    int? MaxDurationMs,
    int? Limit = 50);

// Response
public sealed record ListTracesResponse(
    IReadOnlyList<TraceSummary> Items,
    string? Cursor,
    bool HasMore);

// Pagination convention: cursor-based, not offset
public sealed record PaginationRequest(string? Cursor, int? Limit = 50);
```

## 5. Error responses — ProblemDetails

```csharp
private static async Task<Results<Ok<TraceDetail>, NotFound, ProblemHttpResult>> GetTraceAsync(
    string traceId,
    IVictoriaTracesClient client,
    CancellationToken ct)
{
    try
    {
        var response = await client.GetTraceAsync(traceId, ct);
        return TypedResults.Ok(ToTraceDetail(response));
    }
    catch (VictoriaTracesNotFoundException)
    {
        return TypedResults.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to fetch trace {TraceId}", traceId);
        return TypedResults.Problem(
            title: "Trace fetch failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
}
```

Standard HTTP error semantics:
- `400` — bad request (validation)
- `401` — unauthorized
- `403` — forbidden
- `404` — not found
- `409` — conflict (e.g., dashboard already exists)
- `422` — unprocessable (semantic validation)
- `500` — internal error
- `502`/`503`/`504` — upstream errors (Victoria)

## 6. OpenAPI / Scalar

Tessera exposes OpenAPI doc + Scalar UI:

```csharp
// Program.cs
builder.Services.AddOpenApi();   // Microsoft.AspNetCore.OpenApi (built-in)
builder.Services.AddScalar();     // Tessera.Shared.OpenApi

var app = builder.Build();
app.MapOpenApi();                 // /openapi/v1.json
app.MapScalarApiReference();      // /scalar/v1 (UI)
```

Endpoint annotations:

```csharp
group.MapGet("/{traceId}", GetTraceAsync)
    .WithName("GetTrace")
    .WithSummary("Get trace by ID")
    .WithDescription("Returns full trace with reconstructed span tree")
    .Produces<TraceDetail>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status502BadGateway);
```

## 7. Validation

Via FluentValidation, automatic via `IEndpointFilter`:

```csharp
// src/shared/Tessera.Shared.Validation/ValidationFilter.cs
public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        var validator = ctx.HttpContext.RequestServices
            .GetService<IValidator<TRequest>>();
        if (validator is null) return await next(ctx);

        var request = ctx.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null) return await next(ctx);

        var result = await validator.ValidateAsync(request, ctx.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(result.ToDictionary());
        }

        return await next(ctx);
    }
}

// Registration:
builder.Services.AddTransient<IEndpointFilter, ValidationFilter<CreateDashboardRequest>>();
```

## 8. Authorization

```csharp
group.MapGet("/", ListTracesAsync).AllowAnonymous();
group.MapPost("/", CreateDashboardAsync).RequireAuthorization("admin");
```

See `../../docs/security/auth-model.md` for full model.

## 9. Versioning (stretch)

When API changes break backward compatibility, version via header or path:

```
GET /api/v1/traces     (URL-based — preferred)
GET /api/traces        (header Accept: application/vnd.tessera.v2+json)
```

For MVP, single version. Add `v1` prefix only when needed.

## 10. Anti-patterns

```csharp
// ❌ Wrong — controllers (we use minimal API)
public class TracesController : ControllerBase { /* ... */ }

// ❌ Wrong — endpoint in random file
public class SomeRandomFile
{
    public static void Map(IEndpointRouteBuilder app) { /* ... */ }
}

// ❌ Wrong — return type is dynamic / object / Tuple
app.MapGet("/api/foo", () => new { foo = "bar" });  // OK for trivial, bad for typed API
app.MapGet("/api/foo", () => (Foo, Bar));          // banned — see anti-patterns.md

// ❌ Wrong — ad-hoc JSON serialization in endpoint
app.MapGet("/api/foo", (HttpContext ctx) =>
{
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsync("{\"foo\":\"bar\"}");
});

// ❌ Wrong — magic strings for routes
app.MapGet("/api/v1/traces-v2-final", Handler);  // use /api/traces/{id}
```

## Self-audit grep

```bash
# Controller classes (banned — we use minimal API)
rg -n ": ControllerBase\b" src/ --type cs

# Tuples in return types
rg -n "public\s+\([^)]+\)\s+\w+\(" src/ --type cs

# Magic strings in routes
rg -n 'Map(Get|Post|Put|Delete)\("[^"]*"' src/ --type cs
```

## Related rules

- `naming-and-types.md` — sealed classes for endpoints
- `code-shape.md` — var, braces
- `anti-patterns.md` — tuple ban
- `../../docs/security/auth-model.md` — auth model