# Tessera state

## Milestone
MVP-01 — backend-only (Traces + Logs + Discovery + Health + Victoria provider + Tessera.Host)

## Status
**Phase 1 (Backend foundation) — DONE.**
**Phase 2 (4 backend modules as controllers) — DONE** (7 commits, see commit log)
**Phase 3 (Host composition root + TOML config + admin bearer + OpenAPI) — DONE** (4 commits this session)
**Phase 4 (architecture tests + Testcontainers integration) — NOT STARTED**
**Phase 5 (E2E verify + handoff) — NOT STARTED**

## Progress
- **Phase 0** (rules + provider scaffold) — DONE
- **Phase 1** (shared primitives + provider impls) — DONE (19 commits)
- **Phase 2** (4 backend modules as controllers with Mapperly mappers) — DONE (7 commits, see commit log below)
- **Phase 3** (Host composition root + TOML config + admin bearer + OpenAPI doc + Scalar UI) — DONE (4 commits: `9711702` provider, `3615a00` bearer/example, `1238c3f` OpenAPI; plus 4 module commits from earlier)
- **Phase 4** (architecture tests + Testcontainers integration) — NOT STARTED
- **Phase 5** (E2E verify + handoff) — NOT STARTED

Phase 1 deliverable: 22-project solution builds clean, 34/34 unit tests passing.
Phase 2 deliverable: HealthController, DiscoveryController, TracesController (with ILogProvider log correlation), LogsController — all wired via Tessera.Host AddXxxModule() chain.
Phase 3 deliverable: Tessera.Host has full composition root — TOML config (main + local override + secret prefixes), admin bearer scheme (optional), ProblemDetails global pipeline, OpenAPI doc with ProblemDetails responses injected via transformer, Scalar UI mounted at /scalar/v1, AddVictoriaProvider wiring all 4 provider interfaces.

## Working agreement
- Owner confirms strategic decisions (architectural forks) before code work begins
- PLAN.md scaffolded via `soly_workflow new <slug>`; fleshed out via `discuss` then `plan`
- Each plan task has acceptance criteria — implemented exactly
- Decisions logged here for audit trail
- HANDOFF.md updated when strategic context shifts
- **Owner instruction 2026-07-19:** "делай до конца план, меня не спрашивай" — execute full MVP-01 without re-asking

## Decisions

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-07-19 | Tessera positioning: Grafana-analogue APM (multi-provider), NOT Victoria-specific UI | MVP-01 ships with Victoria as first provider via new `src/providers/` layer; future phases add Tempo/Jaeger/Loki/Mimir without refactoring modules. Architecture: provider interfaces in `Tessera.Shared.Kernel`, concrete implementations in new `src/providers/` top-level layer. No own collector/storage layer — reuse external collectors. |
| 2026-07-19 | New top-level `src/providers/` folder for concrete provider implementations | Mirrors Grafana datasource model: providers pluggable, modules provider-agnostic. Composition root wires active provider(s) in DI. |
| 2026-07-19 | Provider interfaces (`ITraceProvider`, `ILogProvider`, `IMetricsProvider`, `IDiscoveryProvider`, `IHealthProvider`) in `Tessera.Shared.Kernel` | Kernel already holds domain primitives; provider contracts are natural extension. Keeps `shared/` at 5-project cap. |
| 2026-07-19 | MVP-01 module scope: 4 modules — Traces + Logs + Discovery + Health | Matches HANDOFF baseline. Health as DI verification canary, Discovery aggregates services + streams. |
| 2026-07-19 | Victoria is MVP-01 first-and-only provider; second provider deferred to MVP-02+ | User explicitly chose "build abstraction now" over "defer to MVP-02". Cheap upfront, no refactor later. |
| 2026-07-19 | MVP-01 is backend-only; frontend integration deferred to MVP-02 | Owner direction 2026-07-19 ("без моков, сразу бек нормальный делаем, а потом ui уже"): MVP-01 ships backend (4 modules + provider abstraction + host composition root), testable end-to-end via Testcontainers + curl. FE integration deferred to MVP-02. |
| 2026-07-19 | Folder organization rule: max 3 .cs files per folder, 1 type per file, namespace mirrors folder path, file name = type name | Prevents God Object files, makes IDE tree views collapse cleanly, enforces grep-discoverability. New rule at `.agents/rules/coding/folder-organization.md`. |
| 2026-07-19 | Ban private methods globally — extract helpers to file-static classes | No `private` business logic in production classes; helpers go in `internal static class XxxMapper.cs` files. Single Responsibility Principle enforcement. Allowlist: framework overrides (Equals, Dispose), controller `private static` endpoint handlers. |
| 2026-07-19 | No Shouldly/NSubstitute/Bogus in MVP-01 tests; plain xUnit `Assert.*` + hand-written doubles | Transitive deps (Castle.Core, DiffEngine, Newtonsoft.Json) have net10 incompat. Reintroduce when fixed. |
| 2026-07-19 | Extract project-neutral C#/process rules to global `~/.agents/rules/` | Plexor + tessera share same core C# rules. Project-specific bits (theme names, ports, slnx layout) stay in each project's `.agents/rules/`. Verified location: pi-soly loads from `~/.agents/rules/` (NOT `~/.pi/agent/rules/` which I tried first by mistake). |
| 2026-07-19 | Backend switched from minimal API to controllers (Plexor-aligned) | Tessera originally proposed minimal API for "lighter, faster, MVP scope"; Plexor uses controllers. Phase 2 modules built as `[ApiController] : ControllerBase` instead of `MapXxxEndpoints` extensions. AddApplicationPart pattern in Tessera.Host wires per-module controllers without direct type references. Routes prefixed `/api/v1/` (single source `Tessera.Shared.Kernel.Api.ApiRoutes`). Per-module `Errors/<Module>Errors.cs` static class with dot.case code constants passed to `ProviderException.Code`. `IExceptionHandler` (TesseraExceptionHandler) + `AddProblemDetails()` + `UseExceptionHandler()`/`UseStatusCodePages()` global pipeline — no per-endpoint try/catch, no `Result<T>` on HTTP boundary. JSON wire format: `JsonStringEnumConverter` for enum-as-string. Mapperly source generator (Riok.Mapperly 4.3.1) for entity → DTO projections in `Mapping/`. All rules in `.agents/rules/coding/api-design.md` rewritten; `CLAUDE.md` line 92, `AGENTS.md`, `README.md`, `.agents/docs/{architecture,scope}.md`, `.agents/docs/security/auth-model.md`, `.agents/HANDOFF.md` updated to match. |
| 2026-07-19 | TOML config (replacing appsettings.json) | Tessera.Shared.Kernel.Configuration: `TomlConfigurationProvider` (FileConfigurationProvider override flattening TomlTable → IConfiguration keys via `Tomlyn 2.x.TomlSerializer.Deserialize<TomlTable>`); `TesseraConfigPaths.ResolveMainPath` (4-level lookup: `TESSERA_CONFIG` env > `/etc/tessera/tessera.toml` > `$XDG_CONFIG_HOME/tessera/tessera.toml` > `./tessera.toml`); `ResolveLocalOverridePath` (always `tessera.local.toml` in same dir as main); `SecretReference.Resolve` parses `env:VAR_NAME` and `file:/path` prefixes. `AddTesseraConfiguration` extension on both `IConfigurationBuilder` and `HostApplicationBuilder` wires main + local override files in that precedence order. `tessera.toml` and `tessera.local.toml` are gitignored; `*.example` files committed as templates. |
| 2026-07-19 | Admin bearer auth optional in MVP-01 | `AdminBearerHandler : AuthenticationHandler<AdminBearerOptions>` registers the "admin" scheme unconditionally (so future `[Authorize(Policy = "admin")]` annotations work without host changes). When `TESSERA_ADMIN_TOKEN` env var is unset, handler returns `AuthenticateResult.NoResult()` for every request — admin endpoints reject with 401, host starts cleanly. Token comparison uses `CryptographicOperations.FixedTimeEquals` after a length pre-check (length mismatch short-circuits without leaking the position of the first differing byte via timing). `AuthenticationSchemeOptions.AdminToken` is nullable. |
| 2026-07-19 | OpenAPI doc via Microsoft.AspNetCore.OpenApi + Scalar UI | `AddOpenApi(o => o.AddOperationTransformer<ProblemDetailsResponsesTransformer>())` wires the source-gen document provider; `ProblemDetailsResponsesTransformer : IOpenApiOperationTransformer` injects 400/404/409/500/502/503/504 ProblemDetails responses onto every operation via `Responses.TryAdd` (explicit per-action `[ProducesResponseType<ProblemDetails>]` wins). `app.MapOpenApi()` exposes `/openapi/v1.json` for FE codegen; `app.MapScalarApiReference()` mounts `/scalar/v1` UI for human exploration. Scalar.AspNetCore 2.16 auto-discovers the AddOpenApi document — no separate `AddScalar()` service registration needed (the API was simplified in 2.x). |

## Tech stack status

| Component | Status | Notes |
|-----------|--------|-------|
| **Tessera.Shared.Kernel** | ✅ DONE | Domain primitives (Tenant, TraceId, SpanId, TimeRange, LogLevel, Page<T>, Result<T>, Error), domain models (Trace, Span, LogEntry, ServiceSummary), 4 provider interfaces + support types, ProviderException hierarchy. All in category subfolders (max 3 files/folder). `Api/` namespace holds `ApiRoutes` (single URL source, `ApiVersion = "v1"`) and `TesseraJsonOptions` (singleton ProblemDetails serialization). `Configuration/` namespace holds TOML provider, path resolution (`TesseraConfigPaths`), secret-reference parser (`SecretReference`), `ServerOptions` (host/port). |
| **Tessera.Shared.Http** | ✅ DONE | `RefitExtensions.AddTesseraRefitClient<T>`, `BearerTokenHandler`, `HttpClientAuthOptions`. Polly standard resilience wired through `Microsoft.Extensions.Http.Resilience` 10.8.0. |
| **Tessera.Providers.Victoria** | ✅ DONE | 4 provider implementations (Trace/Log/Discovery/Health), Jaeger + LogsQL DTOs, NDJSON parsing, span tree reconstruction (flat list), `VictoriaServiceCollectionExtensions.AddVictoriaProvider()` DI extension. 34/34 unit tests passing. |
| **Tessera.Host** | ✅ DONE | Full composition root: `AddTesseraConfiguration()` (TOML main + local override), `AddControllers().AddApplicationPart(...)` × 4 modules + `JsonStringEnumConverter`, `AddProblemDetails() + AddExceptionHandler<TesseraExceptionHandler>()`, `AddAuthentication("admin")` + `AddAuthorization()` (optional bearer, TESSERA_ADMIN_TOKEN env var), `AddXxxModule()` chain, `AddVictoriaProvider(builder.Configuration)`. Runtime pipeline: `UseExceptionHandler/UseStatusCodePages/Authentication/Authorization`. Endpoint mapping: `MapOpenApi()` + `MapScalarApiReference()` + `MapControllers()`. Sample config files `tessera.toml.example` + `tessera.local.toml.example` committed; real `tessera.toml`/`tessera.local.toml` gitignored. |
| **4 Tessera.Modules.*** | ✅ DONE | HealthController, DiscoveryController, TracesController (with `ITraceProvider` + `ILogProvider` correlation), LogsController — `[ApiController] : ControllerBase`, wired via `AddXxxModule()`, Mapperly mappers, per-module `Errors/<Module>Errors.cs`. |
| **Architecture tests** | ❌ NOT STARTED | Tessera.ArchitectureTests project empty. |
| **Testcontainers integration** | ❌ NOT STARTED | Tessera.Host.Integration + Tessera.Victoria.Integration empty. |
| **Format gate** | ✅ GREEN | dotnet build exit 0; 46 hidden/info warnings remaining (pre-existing noise, none affect build). RCS1141/RCS1142/IDE0320 suppressed via `.editorconfig` last-resort [*.cs] block. |

## Build state

```
$ dotnet build tessera.slnx -c Debug
22 projects, 0 errors, 0 warnings

$ dotnet test tests/unit/providers/Tessera.Providers.Victoria.Unit
34/34 passing
```

## Naming audit (per worker-audit.md self-audit grep)

All passing for tessera `src/`:
- ✅ No forbidden abbreviations (`ct`, `req`, `resp`, `err`, `msg`, `svc`, `u`, `x`, `tmp`)
- ✅ No underscore-prefixed fields
- ✅ No banned suffixes (Dto, Model, Impl, Util, ViewModel)
- ✅ No private methods (zero, after refactor)
- ✅ No `throw ex` (vs `throw`)
- ✅ No `async void`
- ✅ No `_ = discard`
- ✅ No `ArgumentNullException.ThrowIfNull`
- ✅ No `#region`
- ✅ No block-scoped namespaces
- ✅ All public types have XML `<summary>`

## Recent commits

Phase 3 (host composition + TOML + auth + OpenAPI):
```
1238c3f [.stbl](feat/host): OpenAPI doc + ProblemDetailsResponsesTransformer + Scalar UI
3615a00 [.stbl](feat/host): wire AddTesseraConfiguration + admin bearer (optional) + tessera.toml.example
9711702 [.stbl](feat/kernel): TOML config support (provider + multi-file + secrets + ServerOptions)
f44eb9a [.stbl](feat/tests): drop unused Bogus + NSubstitute + Shouldly from module test projects
```

Phase 2 (controllers migration):
```
8b31a03 [.stbl](feat/host): wire module controllers via AddApplicationPart
e0b43bc [.stbl](feat/modules/logs): add LogsController
469e17a [.stbl](feat/modules/traces): add TracesController + ITracesMapper with log correlation
2472ef5 [.stbl](feat/modules/discovery): add DiscoveryController
8e48a8b [.stbl](feat/modules/health): add HealthController + IHealthMapper
e597321 [.stbl](feat/kernel): add ApiRoutes constants for HTTP route templates
cf16d4b [.stbl](feat/meta): pin Refit 13.1.0 + Resilience 10.8.0, add Riok.Mapperly 4.3.1
bffb837 [.stbl](feat/fe): add TraceTable trace explorer (pre-existing FE work)
dea371a [.stbl](feat/fe): add SpanDetailPanel for trace detail (pre-existing FE work)
```

Phase 1 (provider + shared primitives):
```
cd52a37 [.stbl](feat/meta/format): suppress hidden RCS1141/1142/IDE0320 + fix trailing newlines
88ddded [.stbl](feat/meta/analyzers): remove ArgumentNullException.ThrowIfNull in DI extension
83c18e9 [.stbl](feat/providers/victoria): provider impls + DI extension + unit tests
9f6e482 [.stbl](feat/providers/victoria): add Refit clients + Jaeger/LogsQL DTOs
a12141a [.stbl](feat/shared-http): add Refit extension + BearerTokenHandler + auth options
f82dccc [.stbl](feat/shared-kernel): add 4 provider interfaces + support types
18ed55b [.stbl](feat/shared-kernel): add domain models (Trace, Span, LogEntry, Service)
```

## Open questions for Phase 4-5

- CORS policy: permissive in dev only, strict in prod?
- Tessera.ArchitectureTests (NetArchTest rules): no-module-references-providers, no-cross-module-refs, 5-project-folder-cap
- Tessera.Host.Integration + Tessera.Victoria.Integration: Testcontainers VT/VL, WebApplicationFactory end-to-end
- Tessera.Modules.*.Unit testhost fix (env-specific, .NET 10.0.110 SDK bug — fixed in 10.0.200+)
- Doc refresh: `.agents/HANDOFF.md` (still mentions minimal API stubs), `.agents/docs/architecture.md` (wire-format section), `.agents/docs/security/auth-model.md` (admin endpoints — none in MVP-01)

## Resolved (2026-07-19)

- TOML config location precedence: **TESSERA_CONFIG env > /etc/tessera > XDG > cwd/tessera.toml; tessera.local.toml override**
- Admin bearer auth model: **optional in MVP-01 — handler returns NoResult when env unset, host starts cleanly**
- Endpoint `/api/v1` prefix: **baked in from MVP-01 via `ApiRoutes.Base`**
- Tessera.Host wiring pattern: **`AddXxxModule()` chain in DI + `AddApplicationPart(...)` × 4 for controllers + `AddVictoriaProvider(builder.Configuration)` last**

## Next step

Phase 4: Tessera.ArchitectureTests with NetArchTest rules. Blocked by .NET 10 testhost bug (env-specific); rules written, run when SDK fixes land.
