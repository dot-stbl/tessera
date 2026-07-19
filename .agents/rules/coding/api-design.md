---
description: API design — controllers (matches plexor), route prefix v1, JSON conventions, ProblemDetails for errors, [Tags] multi-element, no Result<T> at HTTP boundary
globs: ["**/*.cs"]
always: true
---

# API design

Tessera uses **ASP.NET Core controllers** (`[ApiController] + ControllerBase`),
matching plexor's convention. Endpoints are grouped by feature, route templates
are kebab-case literals composed from `ApiRoutes.Base = "api/v1"`.

The "minimal API vs controllers" decision was reversed on 2026-07-19 — see
`.agents/STATE.md` decision log. The Plexor-style controllers win on tooling,
testability, and consistency with the reference codebase.

## 1. Controller skeleton

```csharp
[ApiController]
[Route(ApiRoutes.Traces)]
[Tags(["traces"])]
public sealed class TracesController(
    ITraceProvider traceProvider,
    ILogProvider logProvider,
    ITracesMapper mapper) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Search traces by service / operation / time / duration")]
    [ProducesResponseType<Page<TraceSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Page<TraceSummary>>> ListAsync(
        [FromQuery] ListTracesRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await traceProvider.SearchAsync(
            request.ToTraceSearchQuery(), cancellationToken);
        return Ok(page);
    }

    [HttpGet("{traceId:length(32)}")]
    [EndpointSummary("Trace detail with correlated logs (single response)")]
    [ProducesResponseType<GetTraceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetTraceResponse>> GetAsync(
        [FromRoute] string traceId,
        CancellationToken cancellationToken = default)
    {
        var parsed = new TraceId(traceId);
        var trace = await traceProvider.GetByIdAsync(parsed, cancellationToken);
        if (trace is null)
        {
            throw new ProviderNotFoundException(
                TracesErrors.TraceNotFound, $"trace {traceId} not found");
        }

        var range = TracesEndpointHelpers.ToLogCorrelationRange(trace);
        var logs = await logProvider.ListByTraceAsync(parsed, range, cancellationToken);
        return Ok(mapper.ToResponse(trace, logs));
    }
}
```

Key points:

- `sealed class`, primary-constructor injection (no private fields).
- `[ApiController]` enables automatic 400 on model-binding failures + ProblemDetails.
- `[Route(ApiRoutes.Traces)]` — never literal route strings.
- `[Tags(["module", "resource"])]` on the **class** (multi-element, kebab-case).
- `[EndpointSummary(...)]` on each action.
- `[ProducesResponseType<T>(StatusCodes.X)]` — **only for 2xx** (4xx/5xx wired
  globally via `IExceptionHandler` + `ProblemDetailsResponsesTransformer`).
- Returns `ActionResult<T>` with raw DTOs (`Ok(dto)`) — no `Result<T>` envelope.

## 2. URL prefix — `v1`

`ApiRoutes.Base = "api/v1"`. Every `[Route]` composes from `ApiRoutes.*`
constants; never literal `"/api/v1/traces"` strings.

```csharp
public static class ApiRoutes
{
    public const string ApiVersion = "v1";
    public const string Base = "api/" + ApiVersion;
    public static string Resource(string name) => Base + "/" + name;

    public const string Health    = Base + "/health";
    public const string Services  = Base + "/services";
    public const string Traces    = Base + "/traces";
    public const string Trace     = Base + "/traces/{traceId:length(32)}";
    public const string TraceLogs = Base + "/traces/{traceId:length(32)}/logs";
    public const string Logs      = Base + "/logs";
}
```

Tessera docs originally said *"MVP single version, add v1 when needed"* —
MVP-01+ ships with `v1` baked in to match plexor.

## 3. Attribute ordering convention

On each action, attributes appear in this order (top to bottom):

1. `[HttpXxx(template, Name = RouteNamesConst)]` — `Name` is optional in MVP-01.
2. `[EndpointSummary("...")]` — required for OpenAPI doc.
3. `[Authorize]` / `[AllowAnonymous]` / `[RequirePermission(...)]` (when present).
4. `[ProducesResponseType<T>(StatusCodes.X)]` — 2xx shapes only.

`[Tags]` and `[ApiController]` go on the **class** only, never on actions.

## 4. Request / Response DTOs

Records with **init-only properties** (Mapperly source-generator requirement).
Positional records banned for DTOs that flow through mappers.

```csharp
public sealed record ListTracesRequest
{
    public string? Service { get; init; }
    public string? Operation { get; init; }
    public long StartUnixMs { get; init; }
    public long EndUnixMs { get; init; }
    public int? MinDurationMs { get; init; }
    public int? MaxDurationMs { get; init; }
    public string? Cursor { get; init; }
    public int? Limit { get; init; } = 50;
}

public sealed record HealthResponse
{
    public required string Provider { get; init; }
    public required HealthStatus Status { get; init; }
    public string? Detail { get; init; }
}
```

Cursor-based pagination via `Page<T>` (already in `Tessera.Shared.Kernel.Pagination`).
No envelope beyond `Page<T>` itself.

## 5. Error responses — ProblemDetails (RFC 9457)

**Every** error response is `application/problem+json`. Boundaries:

| Layer | Mechanism |
|-------|-----------|
| Provider (Tessera.Providers.\*) | Throws typed `ProviderException` / `ProviderTimeoutException` / `ProviderNotFoundException` (in `Tessera.Shared.Kernel.Exceptions`) |
| Host composition root | One global `IExceptionHandler` (`TesseraExceptionHandler`) maps typed exceptions → `ProblemDetails` by stable `Code` |
| OpenAPI doc | `ProblemDetailsResponsesTransformer` injects 400/404/409/500/502/503/504 ProblemDetails responses on every operation |

**Per-endpoint try/catch is banned.** Controllers throw, never wrap.

### 5.1 — NO `Result<T>` at HTTP boundary

`Tessera.Shared.Kernel.Results.Result<T>` exists for **internal** operations
where the caller branches on outcome inline (rare in MVP-01). It is **not**
the HTTP-boundary failure mode — that's what typed exceptions + ProblemDetails
are for. New controller code does **not** use `Result<T>` for endpoint
returns. Status code is the wire-level result indicator; `ProblemDetails.code`
is the machine-readable discriminator.

```csharp
// ❌ WRONG — Result<T> at HTTP boundary
public async Task<ActionResult<Result<TraceDetail>>> GetAsync(...)
{
    var detail = await provider.GetByIdAsync(id, ct);
    return detail is null
        ? Result<TraceDetail>.Err(new Error(TracesErrors.TraceNotFound, "..."))
        : Result<TraceDetail>.Ok(detail);
}

// ✅ CORRECT — throw, let IExceptionHandler map
public async Task<ActionResult<TraceDetail>> GetAsync(...)
{
    var detail = await provider.GetByIdAsync(id, ct);
    return detail is null
        ? throw new ProviderNotFoundException(TracesErrors.TraceNotFound, $"trace {id} not found")
        : Ok(detail);
}
```

### 5.2 — Error code constants

Per-module static classes in `Tessera.Modules.<X>.Errors.<X>Errors`:

```csharp
namespace Tessera.Modules.Traces.Errors;

public static class TracesErrors
{
    public const string TraceNotFound       = "trace.not_found";
    public const string SearchInvalid       = "trace.search.invalid";
    public const string BackendUnreachable  = "provider.network_error";
}
```

Dot.case, three-segment (`<module>.<entity>.<condition>`). Used as `Code` on
`ProviderException`/`Error`. The first segment mirrors the module name so a
client can branch on category without parsing.

## 6. OpenAPI — `Microsoft.AspNetCore.OpenApi` + transformer

```csharp
// Program.cs
builder.Services.AddOpenApi(o => o.AddOperationTransformer<ProblemDetailsResponsesTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TesseraExceptionHandler>();

// app pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();   // not MapGroup / MapGet
```

Endpoint-level attributes document **only 2xx** shapes. 4xx/5xx ProblemDetails
is added globally so per-endpoint `[ProducesResponseType<ProblemDetails>]` is
never needed.

`[EndpointName]` from `Microsoft.AspNetCore.Mvc` is **not** part of the standard
MVC surface — use `[HttpGet(..., Name = "...")]` for OpenAPI operationId.

## 7. Validation — FluentValidation → 400 ProblemDetails

```csharp
[HttpPost]
public async Task<ActionResult<CreateTraceRequest>> CreateAsync(
    [FromBody] CreateTraceRequest request,
    CancellationToken cancellationToken = default)
{
    // ValidationFilter (registered in Tessera.Host) emits ValidationProblem
    // before the action body runs on any failing validator.
    ...
}
```

The `ValidationFilter<TRequest>` returns `TypedResults.ValidationProblem(...)` —
never hand-rolled JSON, never per-endpoint try/catch on `ModelState`.

## 8. Authorization

MVP-01: all endpoints **anonymous** (no `[Authorize]` attribute). Guest tier
reads APM data without auth; admin bearer lands in MVP-02 with the auth model.

When auth arrives:

```csharp
[ApiController]
[Route(ApiRoutes.Traces)]
[Tags(["traces"])]
public sealed class TracesController(...) : ControllerBase
{
    [HttpGet("{traceId:length(32)}")]
    [AllowAnonymous]
    public async Task<ActionResult<GetTraceResponse>> GetAsync(...) { ... }

    [HttpPost]
    [Authorize(Policy = "admin")]
    public async Task<ActionResult<TraceSummary>> CreateAsync(...) { ... }
}
```

Per-action attributes; never `[Authorize]` on the class for MVP-01 (the
class-level attribute blocks the anonymous `[AllowAnonymous]` actions).

## 9. Versioning

URL prefix `v1` (constant in `ApiRoutes.ApiVersion`). No attribute-based
sub-versioning yet. Bumping the constant is a one-line change.

## 10. Anti-patterns

```csharp
❌ public class TracesController : ControllerBase { ... }              // not sealed
❌ app.MapGet("/api/traces", (ITraceProvider p) => ...)                  // minimal API, banned
❌ return TypedResults.Ok(mapper.ToDetail(detail));                      // in controllers — use Ok(dto)
❌ return Result<TraceDetail>.Err(new Error(...));                      // at HTTP boundary, use throw
❌ [HttpGet("{traceId}")]                                               // unconstrained, use :length(32)
❌ [Route("/api/traces")]                                               // literal, use ApiRoutes.Traces
❌ return Results.Json(new { error = "..." });                           // ad-hoc envelope, use ProblemDetails
❌ detail: ex.ToString()                                                 // leaks stack/internals
❌ 200 OK with an error-shaped body                                      // errors carry error status
```

## Self-audit grep

```bash
# Controllers not sealed
rg -n 'public class \w+Controller' src/modules --type cs

# Minimal API leftovers (banned)
rg -n 'app\.Map(Get|Post|Put|Delete)\(' src/ --type cs
rg -n 'MapXxxEndpoints' src/ --type cs

# Result<T> at controller boundary
rg -n 'ActionResult<Result<' src/modules --type cs

# Unconstrained route templates for trace ids
rg -n 'HttpGet\("\{traceId\}"' src/modules --type cs

# Literal route strings
rg -n '\[Route\("/' src/modules --type cs

# Ad-hoc error envelopes
rg -n 'new \{ error' src/ --type cs
```

## Related rules

- `naming-and-types.md` — sealed classes, init-only properties, Mapperly partials
- `code-shape.md` — file-scoped namespaces, var, block bodies
- `error-mapping.md` — exception → HTTP status table
- `problem-details.md` — RFC 9457 shape + wiring
- `api-route-constants.md` — `ApiRoutes` as single source
- `~/.agents/rules/csharp/anti-patterns.md` — DTO record placement