# Tessera state

## Milestone
MVP-01 — backend-only (Traces + Logs + Discovery + Health + Victoria provider + Tessera.Host)

## Status
**Phase 1 (Backend foundation) — DONE.**
**Phase 2 (4 backend modules as controllers) — DONE** (7 commits, see commit log)
**Phase 3 (Host composition root + v1 prefix + ProblemDetails + Victoria wiring) — in progress**

## Progress
- **Phase 0** (rules + provider scaffold) — DONE
- **Phase 1** (shared primitives + provider impls) — DONE (19 commits)
- **Phase 2** (4 backend modules as controllers with Mapperly mappers) — DONE (7 commits, see commit log below)
- **Phase 3** (Host composition root + ApiVersion v1 + ProblemDetails + AddVictoriaProvider) — IN PROGRESS (this commit)
- **Phase 4** (architecture tests + Testcontainers integration) — NOT STARTED
- **Phase 5** (E2E verify + handoff) — NOT STARTED

Phase 1 deliverable: 22-project solution builds clean, 34/34 unit tests passing.
Phase 2 deliverable: HealthController, DiscoveryController, TracesController (with ILogProvider log correlation), LogsController — all wired via Tessera.Host AddXxxModule() chain.

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

## Tech stack status

| Component | Status | Notes |
|-----------|--------|-------|
| **Tessera.Shared.Kernel** | ✅ DONE | Domain primitives (Tenant, TraceId, SpanId, TimeRange, LogLevel, Page<T>, Result<T>, Error), domain models (Trace, Span, LogEntry, ServiceSummary), 4 provider interfaces + support types, ProviderException hierarchy. All in category subfolders (max 3 files/folder). `Api/` namespace holds `ApiRoutes` (single URL source, `ApiVersion = "v1"`) and `TesseraJsonOptions` (singleton ProblemDetails serialization). |
| **Tessera.Shared.Http** | ✅ DONE | `RefitExtensions.AddTesseraRefitClient<T>`, `BearerTokenHandler`, `HttpClientAuthOptions`. Polly standard resilience wired through `Microsoft.Extensions.Http.Resilience` 10.8.0. |
| **Tessera.Providers.Victoria** | ✅ DONE | 4 provider implementations (Trace/Log/Discovery/Health), Jaeger + LogsQL DTOs, NDJSON parsing, span tree reconstruction (flat list), `VictoriaServiceCollectionExtensions.AddVictoriaProvider()` DI extension. 34/34 unit tests passing. |
| **Tessera.Host** | 🟡 IN PROGRESS | Composition root wired with `AddControllers().AddApplicationPart(...)` × 4 modules, `AddXxxModule()` chain. Upcoming: `AddOpenApi + AddProblemDetails + AddExceptionHandler<TesseraExceptionHandler>` + `UseExceptionHandler/UseStatusCodePages` + `AddVictoriaProvider(builder.Configuration)`. |
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

## Open questions for Phase 3-5

- TOML config location precedence (cwd vs /etc/tessera/ vs env TESSERA_CONFIG)?
- Admin bearer auth model: middleware or endpoint filter?
- CORS policy: permissive in dev only, strict in prod?
- Module test project failure mode: testhost can't find NuGet packages with `lib/net10.0/` paths (env-specific, separate task) — known issue from Phase 2, deferred
- Endpoint `/api/v1` prefix? — **RESOLVED 2026-07-19: `ApiRoutes.Base = "api/v1"` baked in from MVP-01**
- Tessera.Host wiring: one `AddTracesModule()` per module? — **RESOLVED: Add<Module>Module() chain in DI + AddApplicationPart for controllers**

## Next step

Phase 3 commit `feat/host: wire ProblemDetails + IExceptionHandler + AddOpenApi + AddVictoriaProvider`. Then re-evaluate tests (Tessera.Modules.*.Unit blocked by env testhost issue — separate task).
