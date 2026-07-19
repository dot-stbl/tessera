# Tessera — Architecture

> APM UI for the Victoria stack. Trace viewer with log correlation, like Kibana APM.

**Status:** Phase 0/1/2/3 of MVP-01 DONE. Backend functional: 4 controllers + composition root + ProblemDetails pipeline + admin bearer optional + TOML config + OpenAPI doc/Scalar UI. Phase 4 (architecture tests + integration) in progress; Phase 5 (E2E verify + doc refresh) pending. Frontend deferred to MVP-02 (works with mock data via `web/playbook/index.html`).

## What is Tessera

Self-hosted web app that proxies the Victoria telemetry stack
(`vtselect` / `vlselect` / `vmselect`) into a Kibana-APM-like experience:

- **Trace list** — searchable, filterable (service, status, duration range, time)
- **Trace detail** — waterfall of parent/child spans with timing + correlated logs
- **Log correlation** — per-trace log panel via `trace_id` join (MVP-01 only)
- **Service inventory** — aggregated from traces + logs
- **Composite health** — per-provider status (degraded/unhealthy → 502 ProblemDetails)

Stack:

| Layer | Tech |
|-------|------|
| Backend | **.NET 10** ASP.NET Core **controllers** ([ApiController] + ControllerBase, Plexor-aligned), single-binary Linux deploy |
| Frontend | **React 19** + Vite + shadcn/ui + Tailwind, bun monorepo (works with mock data in MVP-01) |
| Data | **VictoriaMetrics / VictoriaLogs / VictoriaTraces** over HTTP (no ingest) |

## Module structure — 5-project cap enforced

```
src/
├── host/              # 2 projects
│   ├── Tessera.Host/              composition root ONLY (Program.cs + config templates)
│   └── Tessera.Build.Tools/       MSBuild SDK + VerifyFormatOnBuild target
├── shared/            # 5 projects (at cap, Phase 3 post-renames)
│   ├── Tessera.Shared.Kernel/     domain primitives, provider interfaces,
│   │                              ApiRoutes, TesseraJsonOptions,
│   │                              Configuration/{TOML provider, paths, secret refs, ServerOptions}
│   ├── Tessera.Shared.Http/       Refit base + Polly resilience + BearerTokenHandler
│   ├── Tessera.Shared.Web/        RFC 9457 ProblemDetails pipeline + OpenAPI source-gen
│   │                              + IOpenApiOperationTransformer + Scalar mount
│   ├── Tessera.Shared.Authentication/  admin bearer scheme (optional in MVP-01)
│   └── Tessera.Shared.Validation/ shell, no content yet (Phase 5+ options validators)
├── modules/           # 4 projects (MVP controllers)
│   ├── Tessera.Modules.Traces/    TracesController + ITracesMapper + TracesErrors + ITraceProvider consumer
│   ├── Tessera.Modules.Logs/      LogsController + LogsErrors + ILogProvider consumer
│   ├── Tessera.Modules.Discovery/ DiscoveryController + IDiscoveryProvider consumer
│   └── Tessera.Modules.Health/    HealthController + IHealthMapper + HealthErrors + IHealthProvider consumer
└── providers/         # Grafana datasource model (1 per backend)
    └── Tessera.Providers.Victoria/  concrete impls of ITrace/Log/Discovery/HealthProvider
```

**Layer boundaries (NetArchTest-enforced, Phase 4):**
- `modules/` → `shared/` only (never `providers/`, never `host/`, never each other)
- `providers/` → `shared/` only (never `modules/`, never `host/`, never each other)
- `host/` → everything (composition root wires providers in DI only)

**Theme words for internal naming** (see `.agents/rules/coding/naming-tessera-theme.md`):
`tessera`, `tile`, `mosaic`, `mortar`, `weave`, `capstone`, `pattern`, `fragment`, `veneer`, `join`.

## Wire format (MVP-01)

**Time:** UTC unix milliseconds (`long`, named `*UnixMs`) across every HTTP boundary
(`TimeRange.StartUnixMs/EndUnixMs`, `LogEntry.UnixMs`, `TraceSummary.StartTimeUnixMs`).
Internally `DateTimeOffset` (UTC); `TimeProvider` injected, never `DateTime.UtcNow`.
Matches VT/VL/VM native format — zero tz bugs, no millisecond/microsecond mix-ups.

**Enums:** string in JSON wire format (`"Healthy"`, `"Error"`, ...) via `JsonStringEnumConverter`
added globally in `AddJsonOptions(...)` at composition root (Host).
Plexor convention; allows FE case-insensitive deserialization.

**Errors:** RFC 9457 ProblemDetails — `application/problem+json` body with
`type`, `title`, `status`, `detail`, `instance`, plus extension `code`. Status code map:

| Exception                  | HTTP |
|----------------------------|------|
| `ProviderNotFoundException`| 404  |
| `ProviderTimeoutException` | 504  |
| `ProviderException` (base) | 502  |
| MVC ValidationProblem      | 400  |

No `Result<T>` on HTTP boundary — handlers throw `ProviderException(Errors.X, "msg")`,
global `IExceptionHandler` (`TesseraSharedWebErrorsTesseraExceptionHandler`) writes the
body. `error.code` is stable dot.case (`trace.not_found`, `logs.trace_id_required`,
`health.provider.unreachable`, etc.) per `.agents/rules/csharp/error-mapping.md`.

**Auth:** admin bearer scheme **optional** in MVP-01. Handler
(`TesseraSharedAuthenticationAdminAdminBearerHandler`) returns
`AuthenticateResult.NoResult()` for every request when `TESSERA_ADMIN_TOKEN` env var
is unset — admin endpoints reject 401, host starts cleanly. Token comparison uses
`CryptographicOperations.FixedTimeEquals` for constant-time. No endpoints annotated
with `[Authorize(Policy="admin")]` in MVP-01; admin scheme registered unconditionally
so future annotations work без host changes.

**Routes:** all controllers under `/api/v1/` from `TesseraSharedKernelApiApiRoutes`:
- `GET /api/v1/health` → HealthController.GetAsync (200 OK with composite body, or ProblemDetails 502)
- `GET /api/v1/services` → DiscoveryController.ListAsync (200)
- `GET /api/v1/traces` → TracesController.ListAsync (200, paginated)
- `GET /api/v1/traces/{traceId:length(32)}` → TracesController.GetAsync (200 with logs, or ProblemDetails 404)
- `GET /api/v1/logs` → LogsController.ListAsync (200, MVP-01 requires `traceId` query param, or ProblemDetails 400 if missing)

**OpenAPI:** `Microsoft.AspNetCore.OpenApi` source-generated document at
`/openapi/v1.json` (FE codegen source for MVP-02). All operations auto-injected with
`400/404/409/500/502/503/504` ProblemDetails responses via `ProblemDetailsResponsesTransformer`
(global, not per-endpoint). Scalar UI at `/scalar/v1`.

## Key flows

### "View trace → see logs" (core MVP)

```
1. User opens /traces. SPA calls GET /api/v1/traces?service=...&startUnixMs=...&endUnixMs=...
2. Backend → ITraceProvider.SearchAsync → Tessera.Providers.Victoria
   → vtselect /select/jaeger/api/traces
3. Returns Page<TraceSummary> → rendered as table

4. User clicks a row → SPA navigates to /traces/<trace_id>
5. SPA calls GET /api/v1/traces/{traceId}
6. Backend → ITraceProvider.GetByIdAsync(traceId)
   → vtselect /select/jaeger/api/traces/{trace_id}
   → Tessera.Providers.Victoria reconstructs span tree (flat → parent/child)
7. Backend also calls ILogProvider.ListByTraceAsync(traceId, range) in parallel
   → vlselect /select/logsql/query?query=trace_id:"<id>"
8. Returns GetTraceResponse { trace, logs } in single payload
   → rendered as waterfall + log panel

   // If upstream 404 on trace:
   → throw ProviderNotFoundException(TracesErrors.TraceNotFound, ...)
   → TesseraExceptionHandler writes 404 ProblemDetails body
```

### "Service inventory" (Discovery)

```
1. SPA calls GET /api/v1/services
2. Backend → IDiscoveryProvider.ListServicesAsync
   → vtselect /select/jaeger/api/services + /services/<svc>/operations (parallel)
3. Returns IReadOnlyList<ServiceSummary>
```

### "Health check" (composite)

```
1. SPA calls GET /api/v1/health
2. Backend → IHealthProvider.CheckAsync
3. Returns HealthResponse { status: Healthy|Degraded|Unhealthy, providers: {...}, detail }
4. If status != Healthy → throw ProviderException(HealthErrors.ProviderUnreachable, ...)
   → TesseraExceptionHandler writes 502 ProblemDetails body
```

### "Pipeline bypass" (TesseraExceptionHandler)

```
Controller throws ProviderException
  → Tessera.Shared.Web.Errors.TesseraExceptionHandler.TryHandleAsync
    → map to status (404/502/504 by subtype)
    → write ProblemDetails body via TesseraJsonOptions.Instance
    → LogWarning with structured fields (Path, Code, StatusCode)
    → return true (handled)

  → if exception is NOT ProviderException → return false
    → falls through to framework default (500 unhandled)
```

## Data flow

```
Browser (React SPA, MVP-02 — currently hand-written mock fallback in `web/apps/console/src/shared/api/`)
   │
   │ JSON (REST, RFC 9457 ProblemDetails for errors)
   ▼
ASP.NET Core host (Tessera.Host — composition root only)
   │
   │ ITraceProvider / ILogProvider / IDiscoveryProvider / IHealthProvider
   ▼
Tessera.Providers.Victoria (DI-injected as concrete impls)
   │
   │ Refit HTTP clients with Polly standard resilience + BearerTokenHandler
   ▼
┌──────────┐   ┌──────────┐   ┌──────────┐
│ vtselect │   │ vlselect │   │ vmselect │
│ (Jaeger) │   │ (LogsQL) │   │ (PromQL) │
└──────────┘   └──────────┘   └──────────┘
```

Backend is a thin proxy — no persistence, no business logic.
All state is in the user's browser (TanStack Query cache + zustand for filters, MVP-02).

## Composition root (Phase 3)

`src/host/Tessera.Host/Program.cs` reads as a declarative chain — **no business
logic, no controllers/middleware definitions** in this file. Each section is one
installer call. The composition style is set by `.agents/rules/csharp/di-installer.md`:

```csharp
builder.Configuration.AddTesseraConfiguration();                // TOML config (main + local override)

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddApplicationPart(typeof(Tessera.Modules.Health.Controllers.HealthController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Discovery.Controllers.DiscoveryController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Traces.Controllers.TracesController).Assembly)
    .AddApplicationPart(typeof(Tessera.Modules.Logs.Controllers.LogsController).Assembly);

builder.Services.AddTesseraWebInfrastructure();               // ProblemDetails + IExceptionHandler + OpenAPI(transformer)
builder.Services.AddTesseraAdminAuthentication(builder.Configuration);  // bearer scheme (optional)
builder.Services.AddAuthorization();

builder.Services
    .AddHealthModule()
    .AddDiscoveryModule()
    .AddTracesModule()
    .AddLogsModule()
    .AddVictoriaProvider(builder.Configuration);                // concrete impls wired ONLY here

var app = builder.Build();

app.UseExceptionHandler();   // reads TesseraExceptionHandler chain
app.UseStatusCodePages();    // 404/405/415 → ProblemDetails body
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => /* version banner */);
app.UseTesseraOpenApi();     // MapOpenApi + MapScalarApiReference
app.MapControllers();

app.Run();
```

**Composition installer pattern:**
- `Tessera.Shared.Web.WebInstallerExtensions.AddTesseraWebInfrastructure()` + `UseTesseraOpenApi()`
- `Tessera.Shared.Authentication.AuthenticationInstallerExtensions.AddTesseraAdminAuthentication(IConfiguration)`
- `Tessera.Shared.Kernel.Configuration.Source.TesseraConfigurationExtensions.AddTesseraConfiguration()` (already shipped Phase 1)
- Each per-module assembly has its own `Add<Module>Module()` extension co-located with controllers
- `Tessera.Providers.Victoria.DependencyInjection.VictoriaServiceCollectionExtensions.AddVictoriaProvider(IConfiguration)` — wires all 4 concrete impls from config section `[victoria:*]`

## Architectural decisions

| Decision | Choice | Why |
|----------|--------|-----|
| API style | **Controllers** (`[ApiController]` + `ControllerBase`, matches Plexor) | Consistent with reference codebase, better tooling/testability. Minimal API was prior choice; reversed 2026-07-19 — see STATE.md. |
| URL prefix | `ApiRoutes.Base = "api/v1"` (v1 baked in from MVP-01) | Single-line bump for v2; matches Plexor convention |
| Wire format | **UTC unix ms** (`long *UnixMs`) across HTTP, `DateTimeOffset` internally, `TimeProvider` injected (never `DateTime.UtcNow`) | Matches VT/VL/VM native format, no tz bugs. `~/.agents/rules/csharp/time-and-wire-format.md` |
| HTTP client | **Refit** with interfaces | Type-safe, easy to mock in tests |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly AddStandardResilienceHandler) | Standard ASP.NET pattern |
| Error pipeline | IExceptionHandler + ProblemDetails (RFC 9457) globally; per-endpoint try/catch **banned**; no `Result<T>` on HTTP boundary | Plexor pattern; `~/.agents/rules/csharp/problem-details.md`, `error-mapping.md` |
| JSON conventions | `JsonStringEnumConverter` (enum-as-string), camelCase via `TesseraJsonOptions.Instance` (shared singleton), no `new JsonSerializerOptions` at call sites | Plexor pattern; OpenAPI emits lowercase status names. `~/.agents/rules/csharp/json-and-ndjson.md` |
| Auth | **admin bearer optional in MVP-01** (`TESSERA_ADMIN_TOKEN` unset → handler returns NoResult → admin endpoints reject 401, host starts cleanly) | Proxy pattern: SPA never sees backend token. Future OIDC/LDAP stretch. |
| Config | **TOML only** (`tessera.toml` + optional `tessera.local.toml` override), env-var prefix `TESSERA_*`, secrets via `env:VAR` / `file:/path` inline prefixes | `~/.agents/rules/csharp/configuration-toml-env.md` + `.agents/docs/architecture/config-format.md` |
| Tenant | **single, hardcoded `0`** in MVP | Multi-tenant routing is out of MVP scope |
| Pagination | **cursor-based** on trace list | VT uses `limit`, not offset — cursor is forward-compatible |
| Span tree | **reconstructed on backend** from VT flat list (parent/child via `references[CHILD_OF]`) | Saves frontend work |
| Log volume cap | **500 per trace view**, no streaming yet | Good enough for MVP; add SSE/websocket later if needed |
| Service discovery | from **VT `/services` + `/services/<svc>/operations` aggregated** | VT is source of truth for trace-backed services |
| State management (FE, MVP-02) | **zustand for filters, TanStack Query for server state** | Lightweight, no Redux ceremony |
| Frontend data viz (MVP-02) | **visx** (or hand-rolled SVG) for waterfall | jaeger-style waterfall is ~300 LOC with visx primitives |
| Provider model | **Grafana datasource** (interface in Shared.Kernel, impl in `Providers.<Name>`); concrete provider wired **only** in host composition root | Future Tempo/Jaeger/Loki без module refactor. `~/.agents/rules/csharp/project-layers.md` §Provider abstraction |
| Mapping | **Riok.Mapperly 4.3.1** source-gen for entity → DTO; `[Mapper(RequiredMappingStrategy = Target)]`; DTOs init-property record/class (NOT positional records) | Source-gen performance, strict mapping fail-fast at compile time. `~/.agents/rules/csharp/mapping.md` |
| Host project role | **composition root only** — Program.cs + config templates. No бизнес-логики, no controllers/middleware, no problem-details mapping. All infrastructure lives in `Shared.{Web,Authentication,Kernel}` with their own `*InstallerExtensions`. | Plexor pattern; enforces single-responsibility — change in error policy → Shared.Web, not Host. |
| OpenAPI doc | `Microsoft.AspNetCore.OpenApi` source-gen + `IOpenApiOperationTransformer` for global ProblemDetails response injection + Scalar UI mount | Single source for FE codegen + human exploration, no per-endpoint [ProducesResponseType&lt;ProblemDetails&gt;] |

## Things explicitly out of MVP

See `scope.md` for full list. Highlights:
- Service map / dependency graph (stretch)
- RED metrics panel per service (stretch)
- Flame graph view (stretch)
- Ad-hoc LogsQL (deferred to MVP-02 — MVP-01 only supports trace correlation)
- Custom dashboards / panels (deferred)
- SLO budgets, deployment markers (out)
- Alerting, anomaly detection (out)
- Multi-tenancy routing (out)
- SSE/log streaming (out — 500-cap per request instead)

## Resolved questions (2026-07-19)

| Question | Resolution |
|----------|------------|
| Backend SDK | **.NET 10** (10.0.110+, latestFeature rollForward, `global.json`) |
| Build SDK | **`Microsoft.NET.Sdk`** (NOT `Microsoft.Build.NoTargets`) |
| Config format | **TOML only** (`tessera.toml` + optional `tessera.local.toml`), no `appsettings.json` |
| Auth in MVP-01 | **admin bearer optional**; guest endpoints anonymous; OIDC/LDAP stretch |
| TOML location precedence | **TESSERA_CONFIG env > /etc/tessera > XDG > cwd** (4-level lookup, `TesseraConfigPaths.ResolveMainPath`) |
| Time format | **UTC unix milliseconds** across HTTP, `DateTimeOffset` internally |
| Error format | **RFC 9457 ProblemDetails** + `code` extension (stable dot.case) |
| Modules format | **Controllers** ([ApiController] + ControllerBase), primary ctor + `AddApplicationPart` |
| Host format | Composition chain of `AddTesseraConfiguration()` + `AddTesseraWebInfrastructure()` + `AddTesseraAdminAuthentication(cfg)` + `Add<Module>Module()` + `AddVictoriaProvider(cfg)` |

## Open questions (carry-over to Phase 5)

1. **Local E2E verify** — single-machine Victoria stack (`victoriametrics/victoria-stack`)
   vs separate VT/VL/VM containers. Pick when wiring Testcontainers.
2. **PII redaction in logs** — currently logs pass through raw from VL.
   Add filter if compliance requires.
3. **Admin endpoints** — none in MVP-01; `[Authorize(Policy="admin")]` annotations land in MVP-02 (debug surface, dangerous actions like reload).
4. **FE codegen contract source** — single source of truth for MVP-02 kubb codegen = the live `GET /openapi/v1.json` from the host. Confirm before MVP-02.

## Related docs

- `modules.md` — per-module HTTP contracts
- `scope.md` — what's in / out for MVP
- `config-format.md` — TOML config schema + secret prefixes
- `auth-model.md` — auth scheme matrix
- `victoria-stack.md` — VT/VL/VM API reference
- `.agents/STATE.md` — current progress + decision log + open questions
- `.agents/HANDOFF.md` — context for next session
- `ui/` — UI design docs (MVP-02)
