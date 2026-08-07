# Tessera state

> **Authoritative decisions:** see [`.agents/docs/decisions/0001-mvp01-locked-decisions.md`](docs/decisions/0001-mvp01-locked-decisions.md).
> STATE.md tracks progress + open questions; ADRs lock architectural choices.

## Milestone
MVP-02 (platform) — backend-only MVP-01 + multi-provider auth + EF Core storage layer + FS layout abstraction + telemetry pipeline + FE bundle.

## Status (branch `tessera/mvp-2-platform`, ADR-0001 locked 2026-07-20)
**MVP-01 (Phase 0–5) — DONE** (commits `2cb9533`..`bbd97fb`). Backend ready.
**MVP-02 workstreams:**
- ✅ **Phase 1** — AGENTS.md force-loading + new global rules (multi-provider-auth, filesystem-paths, cross-platform)
- ✅ **Phase 2** — config schema: ServerOptions + StorageOptions + SecretReference + double-underscore routing for `TESSERA_ADMIN_TOKEN`
- ✅ **Phase 3** — `IFileSystemLayout` + Linux (`/etc/tessera`, `/var/lib/tessera`, XDG) + Windows (`%ProgramData%`, `%LOCALAPPDATA%`) impls
- ✅ **Phase 4** — multi-provider auth: `IAuthProvider` framework, `Guest` + `AdminBearer` + `Ldap` (search-and-bind via `System.DirectoryServices.Protocols`) + `Keycloak` (JWT bearer via `Microsoft.AspNetCore.Authentication.JwtBearer`); `ICurrentUserContext` middleware
- ✅ **Phase 5** — EF Core + SQLite + Repository<T> + Specification<T, TResult> + `Microsoft.Data.Sqlite` 10.0.10 wired only into `Tessera.Host` (binary-compat workaround for the no-net10-build issue); Preferences module + initial migration
- ✅ **Phase 6** — vite FE bundle into `Tessera.Host/wwwroot/` via multi-stage Dockerfile (bun-builder + .NET publish)
- ✅ **Phase 7a** — OTel pipeline (`Tessera.Shared.Telemetry` installer + host wiring)
- ✅ **Phase 7b** — CI workflow (self-hosted Linux) + Phase 7 draft issue for Windows L1-L4
- ⏸ **Phase 7 Windows L1-L4** — deferred. Tracked in `.github/ISSUE_DRAFT_phase-7-windows.md` (no Windows runner; GH-paid plan unavailable)

**Tests:** 119/119 passing across 9 test projects (`dotnet test tessera.slnx`).
**Build:** `dotnet build tessera.slnx -c Debug` → exit 0, 0 warnings.

## Progress (Phase 0–6 already in MVP-01 history)
- ✅ **Phase 0** — pre-existing format cleanup + VSTHRD111 disable + rule sync (`2cb9533`, `8215500`)
- ✅ **Phase 1** — `tessera/mvp-2-platform` baseline + global C# rules
- ✅ **Phase 2** — config schema + `TESSERA__ADMIN__TOKEN` routing (`c5dcb60`, `4ec7dec`)
- ✅ **Phase 3** — `IFileSystemLayout` + LayoutProvider + `TesseraConfigPaths` migration (`1125eb0`, `38fb3c7`)
- ✅ **Phase 4** — multi-provider auth wiring (`4cc752c`, `618341a`, `f1c0f3c`, `bf652c3`, `cc8f3cd`)
- ✅ **Phase 5** — Repository/Spec primitives + Preferences module + `InitialSchema` migration (`55b269b`, `4fb5526`, `0e56fee`, `f7c9146`, `c5dcb60`-storage, `4ec7dec`, `e23385e`)
- ✅ **Phase 6** — FE bundle (`064f5f5`, `dc70e7a`)
- ✅ **Phase 7a/b** — OTel + Linux CI (`b30da28`, `c122ecd`)
- ⏸ **Phase 7 Windows** — deferred, tracked by `.github/ISSUE_DRAFT_phase-7-windows.md`

See ADR-0001 for the architectural decisions that drive Phase 1–7.

## Phase 4 deliverable (now updated)
107/107 unit tests passing across 8 test projects (`dotnet test tessera.slnx`):
- `Tessera.Providers.Victoria.Unit` — 34
- `Tessera.Host.UnitTests` — 8
- `Tessera.Shared.Unit` — 28
- `Tessera.ArchitectureTests` — 13
- `Tessera.Modules.Health.Unit` — 9
- `Tessera.Modules.Discovery.Unit` — 4
- `Tessera.Modules.Traces.Unit` — 7
- `Tessera.Modules.Logs.Unit` — 4

Build: `dotnet build tessera.slnx -c Debug` → exit 0, 0 warnings.

## Phase 5 deliverable (corrected)
`/compose/` as single source of truth:
- `compose/Dockerfile` (multi-stage build + runtime ALPINE with `apk add libldap` for Phase 4b LDAP — already added in `075174e` for HEALTHCHECK wget, will be reused)
- `compose/.dockerignore`
- `compose/docker-compose.yml` (3 separate Victoria containers + OTel collector + Tessera)
- `compose/otel-collector/config.yaml`
- `compose/config/tessera.conf/tessera.toml`
- `compose/README.md`

**Correction 2026-07-20:** earlier prose claimed `grafana/provisioning/{datasources,dashboards}/...` files were added in Phase 5. **They were not** — Grafana is not part of the compose stack. The State text is corrected to drop that claim.

`tests/Tessera.LoadGen/` console app (OpenTelemetry SDK + 3 scenarios: simple, fanout, saga) — DONE.

Rule-audit Task(general) found 11 critical / 8 high / 25 medium issues; session-introduced violations fixed. Pre-existing critical violations (HttpClientFactory / EnsureSuccessStatusCode / VerifyFormatOnBuild placeholder) remain in `src/providers/Tessera.Providers.Victoria/` and `src/host/Tessera.Build.Tools/` — tracked in `.planning/BACKEND-ISSUES.md`.

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
| 2026-07-21 | Core observability model locked — **ADR-0002** | OTel-only for logs+traces; canonical domain = OTel semconv; providers map `wire→OTel` and the correlation/errors/deps/grouping logic lives above them (written once). Trace-detail = request-view keyed by `trace_id` (union spans+logs+metrics; degrades to log-only/spans-only). Errors by `status.code` + `exception` events (grouping message → fingerprint later). RED from OTel metrics via PromQL (light span fallback, no heavy engine; needs `IMetricsProvider`, MVP-02). Service deps derived from spans (CLIENT→SERVER + `peer.service`/`db.system` nodes; per-trace in core, global map = cached rollup post-core). Background rollup cache = derived views only, time-bucketed, rebuildable, never a telemetry store (consistent with ADR-0001 D8). Projects = declared attribute-predicates over OTel attrs (bounded → bounds aggregation; future RBAC/tenancy). Full spec: `.agents/docs/core-design.md`. |
| 2026-08-07 | Core module cut + smart-logic placement — **ADR-0003** | A3: pure derivation in `Kernel/Analysis/`, orchestration in modules, provider = wire→OTel only. A1 fat slices: stay at 5 modules (Traces/Logs/Discovery/Health/Preferences) — no Errors/Metrics/Projects modules in P5–P9. Errors HTTP → Traces; RED → Discovery. Projects + rollup cache post-core only. FE OpenAPI free to evolve until core stabilizes; kubb/MSW later. |

## Tech stack status

| Component | Status | Notes |
|-----------|--------|-------|
| **Tessera.Shared.Kernel** | ✅ DONE | Domain primitives (Tenant, TraceId, SpanId, TimeRange, LogLevel, Page<T>, Result<T>, Error), domain models (Trace, Span, LogEntry, ServiceSummary), 4 provider interfaces + support types, ProviderException hierarchy. All in category subfolders (max 3 files/folder). `Api/` namespace holds `ApiRoutes` (single URL source, `ApiVersion = "v1"`) and `TesseraJsonOptions` (singleton ProblemDetails serialization). `Configuration/` namespace holds TOML provider, path resolution (`TesseraConfigPaths`), secret-reference parser (`SecretReference`), `ServerOptions` (host/port). |
| **Tessera.Shared.Http** | ✅ DONE | `RefitExtensions.AddTesseraRefitClient<T>`, `BearerTokenHandler`, `HttpClientAuthOptions`. Polly standard resilience wired through `Microsoft.Extensions.Http.Resilience` 10.8.0. |
| **Tessera.Providers.Victoria** | ✅ DONE | 4 provider implementations (Trace/Log/Discovery/Health), Jaeger + LogsQL DTOs, NDJSON parsing, span tree reconstruction (flat list), `VictoriaServiceCollectionExtensions.AddVictoriaProvider()` DI extension. 34/34 unit tests passing. |
| **Tessera.Host** | ✅ DONE | Full composition root: `AddTesseraConfiguration()` (TOML main + local override), `AddControllers().AddApplicationPart(...)` × 4 modules + `JsonStringEnumConverter`, `AddProblemDetails() + AddExceptionHandler<TesseraExceptionHandler>()`, `AddAuthentication("admin")` + `AddAuthorization()` (optional bearer, TESSERA_ADMIN_TOKEN env var), `AddXxxModule()` chain, `AddVictoriaProvider(builder.Configuration)`. Runtime pipeline: `UseExceptionHandler/UseStatusCodePages/Authentication/Authorization`. Endpoint mapping: `MapOpenApi()` + `MapScalarApiReference()` + `MapControllers()`. Sample config files `tessera.toml.example` + `tessera.local.toml.example` committed; real `tessera.toml`/`tessera.local.toml` gitignored. |
| **4 Tessera.Modules.*** | ✅ DONE | HealthController, DiscoveryController, TracesController (with `ITraceProvider` + `ILogProvider` correlation), LogsController — `[ApiController] : ControllerBase`, wired via `AddXxxModule()`, Mapperly mappers, per-module `Errors/<Module>Errors.cs`. |
| **Architecture tests** | ✅ DONE | `Tessera.ArchitectureTests` (NetArchTest) — 13 facts: provider isolation (`NoModuleReferencesProviders`), cross-module ban, host isolation, 5-project folder cap, Kernel purity. 13/13 passing. |
| **Testcontainers integration** | ⏸️ DROPPED (MVP-01) | Integration tests deliberately dropped per owner decision; `tests/integration/` removed from the solution. Restore when wiring Phase 5 storage. |
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

Phase 4 (deployment + fresh-eye fixups, this session):
```
075174e [.stbl](feat/meta/cleanup): fix critical issues from fresh-eye review
075174e.n   [skip — covered above]
b98a3c2 [.stbl](feat/tests): recreate ArchitectureTests via dotnet new xunit + fix SrcRoot resolution
48ee179 [.stbl](feat/modules/discovery): add Discovery controller unit tests
4d6beXX [.stbl](feat/build): docker-compose for tessera + victoria-stack local verification
7c4d52b [.stbl](feat/build): multi-stage Dockerfile + .dockerignore for Tessera.Host
9f47c81 [.stbl](feat/tests/host): add Host unit tests + InternalsVisibleTo for shared helpers
205b119 [.stbl](feat/modules/logs): add Logs controller + errors unit tests
bcf99XX [.stbl](feat/modules/traces): add Traces controller + errors unit tests
1711e6b [.stbl](feat/tests): recreate test projects via dotnet new xunit + NSubstitute
```

Phase 3 (host composition + TOML + auth + OpenAPI):
```
672284f [.stbl](feat/meta/docs): refresh docs for Phase 3 state (composition root + auth + TOML)
4064eea [.stbl](feat/meta): host = composition root only; finalize Program.cs extraction
29485dc [.stbl](feat/meta): host = composition root only; extract web infrastructure to shared
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

## Open questions for Phase 5 (pending human-side runtime verification + CI workflow)

- **Verify `.dockerignore` composition against actual build context** — when user runs `docker compose build`, check the context size; .dockerignore currently excludes .git/, web/, node_modules/, etc. but may need tweaking.
- **Verify `victoria-stack:v1.10.0` supports all 3 selection roles in one binary** — fresh-eye flagged that official image matrix may differ. If `/select/jaeger/api/traces` doesn't work, split into `victoria-traces` + `victoria-logs` + `victoria-metrics` per `install.md` Compose variant. User-side docker compose up needed.
- **Local end-to-end verify** — `dotnet run --project src/host/Tessera.Host` + curl `/api/v1/health` etc., then `docker compose up` + verify same endpoints through Victoria proxy. Agent-runtime-safety forbids these from the agent; runs on user side.
- **CI workflow file** — `feat/ci: GitHub Actions build + format + test gate`. Deferred per owner direction in MVP-01 final session ("cicd потом").
- **`TESSERA__ADMIN__TOKEN` (double-underscore) env-var routing** — referenced in `.agents/docs/operations/configure.md:41` but the implementation in `AuthenticationInstallerExtensions` reads only `configuration["TESSERA_ADMIN_TOKEN"]` (`AddEnvironmentVariables()` reads both shapes — single-underscore as default, double-underscore as nested section). Doc-vs-code drift; either fix the doc or wire explicit `TESSERA__ADMIN__TOKEN` to make it discoverable.
- **`SecretReference.Resolve` is defined but not invoked** — `.agents/docs/architecture/config-format.md` documents `token = "env:VAR"` inline syntax on the TOML side, but `VictoriaOptions` and `AdminBearerOptions` don't run their values through `SecretReference.Resolve`. Inline replacement works for simple `env:VAR` but doesn't resolve `file:/path`. Architectural follow-up for MVP-02 (real Victoria bearer + sidecar-mounted token file).
- **Integration tests** — `Tessera.Host.Integration` + `Tessera.Victoria.Integration` (Testcontainers VT/VL, `WebApplicationFactory<Program>`) — deferred per owner direction ("integration в MVP-01 skip"). Folder `tests/integration/` removed from solution; restore when wiring Phase 5.
- **Folder cap (R1/R2) — 4 files in `Admin/` + `Configuration/Source/` exceeds the 3-file-per-folder rule** — strictly violating; conceptually cohesive so deferred. Nest as `<Module>/{Options,Handler,Crypto,Constants}/` or similar at MVP-02.
- **R7: bash-only syntax `${TESSERA_ADMIN_TOKEN:-}` in docker-compose.yml** — fails on Windows docker-compose v1; works on Linux/macOS v1 and Docker Desktop v2+. Cosmetic; document or switch to explicit `.env`.
- **R8: fragile `Returns<...>(_ => throw)` in DiscoveryControllerTests** — works today; prefer `Returns(Task.FromException<>(upstream))` for resilience to future NSubstitute version bumps.

## Resolved (2026-07-19) — and through this MVP-01 final session

- TOML config location precedence: **TESSERA_CONFIG env > /etc/tessera > XDG > cwd/tessera.toml; tessera.local.toml override**
- Admin bearer auth model: **optional in MVP-01 — handler returns NoResult when env unset, host starts cleanly**
- Endpoint `/api/v1` prefix: **baked in from MVP-01 via `ApiRoutes.Base`**
- Tessera.Host wiring pattern: **`Add<Module>Module()` chain in DI + `AddApplicationPart(...)` × 4 for controllers + `AddVictoriaProvider(builder.Configuration)` last**
- **Testhost runtime (`Microsoft.Extensions.AmbientMetadata.Application` `lib/net10.0/` path resolution) — RESOLVED via user's manual install of .NET SDK 10.0.302.** All 8 test projects run; 92/92 passing. Recreating test projects via `dotnet new xunit` template (instead of hand-rolling csproj) was the key unblock — template-defaulted test projects have correct test SDK and runtimeconfig that the older hand-written csproj lacked.
- **LogsController validation contract** — RESOLVED. `ListLogsRequest.ToLogQuery()` now returns null when `TraceId` is missing, so `LogsController`'s `is not { } query` branch does fire and throws `ProviderException(LogsErrors.TraceIdRequired)` → 400 ProblemDetails body (per `architecture.md` Wire format section). Committed in `075174e`.
- **Dockerfile HEALTHCHECK wget missing** — RESOLVED. Added `RUN apk add --no-cache wget` in runtime stage. `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` doesn't ship wget by default; without this, the container reports `unhealthy` forever despite the host being up. Committed in `075174e`.
- **AdminBearerOptions immutability (R3)** — RESOLVED. `AdminToken` is now `init` per `di-options.md` §3.

## Next step

MVP-02 platform track (Phase 1 done: AGENTS.md + global rules `4e2fe1d`; FE/TS starter rules + doc reconcile 2026-07-21). Next code work: config-schema fix + `SecretReference` wiring (Phase 2) → `IFileSystemLayout` + Windows (Phase 3). See ADR-0001.

_Reconciled 2026-07-21: the "Tech stack status" table above previously showed arch-tests + Testcontainers as "NOT STARTED" and this section pointed at a resolved .NET 10 testhost bug — both contradicted the top status block (107/107 tests, 13 arch facts). Corrected to DONE / DROPPED._
