# Plan: tessera-mvp

> Tessera MVP-01 — end-to-end backend + trace/log correlation UX. Drops Metrics
> entirely from MVP-01 scope (deferred to MVP-02+). Focus: Traces + Logs +
> Discovery + Health with Kibana-Observability-style trace/log correlation as
> the headline UX feature. Provider abstraction is mandatory (Victoria is first
> provider; future phases add Tempo/Jaeger/Loki without module refactor).
>
> Strategic context: `.agents/HANDOFF.md` § Strategic positioning +
> `.agents/STATE.md` Decisions.

## Goal

End-to-end working Tessera MVP-01: backend serves real VictoriaTraces +
VictoriaLogs data through 4 modules (Traces, Logs, Discovery, Health) behind
a provider abstraction; **frontend uses plexor SPA pattern — design-first
OpenAPI contract (`contracts/tessera.openapi.yaml`) + kubb codegen
(`web/tooling/codegen/`) + MSW via `VITE_USE_MOCKS`** — trace-detail page
shows waterfall + correlated logs side-by-side like Kibana Observability.
MVP-01 deliberately omits Metrics, Dashboards, and Service Map — those are
MVP-02+.

---

## Steps

### Phase 0 — Rule + solution wiring

**T0.1** Update `.agents/rules/coding/project-deps-and-tests.md`
- Add `providers/` to layer dependency diagram
- Add rule: `Tessera.Modules.*` cannot have `<ProjectReference>` on `Tessera.Providers.*`
- Add rule: `Tessera.Providers.*` can reference `Tessera.Shared.*` only
- Add rule: `Tessera.Host` is composition root, references all layers
- Acceptance: rule updated, builds clean

**T0.2** Update `.agents/rules/coding/project-naming-and-setup.md`
- Add `Tessera.Providers.<Name>` naming pattern to decision tree
- Add `providers/` branch to decision tree (§4)
- Add `providers/` example to folder cap tree (§2)
- Acceptance: rule updated

**T0.3** Update `.agents/docs/modules.md`
- Reframe each module's "Owns" section: module consumes `IXxxProvider` interface, not direct Refit client
- Move Refit + DTO mapping references to `Tessera.Providers.Victoria` (new doc section)
- All 4 module sections updated
- Acceptance: doc consistent with provider model

**T0.4** Create `src/providers/` directory + `Tessera.Providers.Victoria` csproj
- Empty project (`Microsoft.NET.Sdk`), TargetFramework=net10.0
- ProjectReference on `Tessera.Shared.Kernel` only
- Acceptance: `dotnet build src/providers/Tessera.Providers.Victoria` succeeds alone

**T0.5** Add `Tessera.Providers.Victoria` to `tessera.slnx`
- New solution folder `/src/providers/` with Victoria project inside
- Acceptance: `dotnet sln tessera.slnx list` shows the project

**T0.6** Verify Phase 0 builds clean
- `dotnet build tessera.slnx -c Debug` exit 0, 0 warnings, 0 errors
- Acceptance: build green

**Commit at end of Phase 0:** `[.stbl](feat/meta): add providers layer + rules`

---

### Phase 1 — Backend foundation (Tessera.Shared.Kernel + Shared.Http + Tessera.Providers.Victoria)

**T1.1** `Tessera.Shared.Kernel` — primitives
- `Tenant`, `TraceId`, `SpanId`, `LogLevel`, `TimeRange`, `Page<T>`, `Result<T>`, `Error`
- Exception types: `ProviderException`, `ProviderNotFoundException`, `ProviderTimeoutException`
- File: `src/shared/Tessera.Shared.Kernel/Primitives/*.cs`
- Acceptance: types compile, 0 warnings, all sealed records/classes per rules

**T1.2** `Tessera.Shared.Kernel` — domain models
- `Trace` (TraceId, RootService, RootOperation, StartTime, DurationMs, Status, Spans)
- `Span` (SpanId, ParentSpanId?, Service, Operation, StartTime, DurationMs, Status, Tags, Children)
- `LogEntry` (Timestamp, Level, Service, TraceId?, SpanId?, Message, Fields)
- `Service` (Name, Sources — list of providers that reported it)
- File: `src/shared/Tessera.Shared.Kernel/Domain/*.cs`
- Acceptance: types compile, sealed per naming-and-types rule

**T1.3** `Tessera.Shared.Kernel` — `ITraceProvider` interface
- `Task<TracePage> SearchAsync(TraceSearchQuery query, CancellationToken ct)`
- `Task<Trace?> GetByIdAsync(TraceId traceId, CancellationToken ct)`
- Support types: `TraceSearchQuery`, `TracePage`, `TraceStatus`
- File: `src/shared/Tessera.Shared.Kernel/Providers/ITraceProvider.cs`
- Acceptance: interface compiles, XML doc comments present

**T1.4** `Tessera.Shared.Kernel` — `ILogProvider` interface
- `Task<LogPage> QueryAsync(LogQuery query, CancellationToken ct)`
- `Task<IReadOnlyList<LogEntry>> ListByTraceAsync(TraceId traceId, TimeRange range, CancellationToken ct)`
- Support types: `LogQuery`, `LogPage`
- File: `src/shared/Tessera.Shared.Kernel/Providers/ILogProvider.cs`

**T1.5** `Tessera.Shared.Kernel` — `IDiscoveryProvider` interface
- `Task<IReadOnlyList<Service>> ListServicesAsync(CancellationToken ct)`
- File: `src/shared/Tessera.Shared.Kernel/Providers/IDiscoveryProvider.cs`

**T1.6** `Tessera.Shared.Kernel` — `IHealthProvider` interface
- `Task<ProviderHealthReport> CheckAsync(CancellationToken ct)`
- Support types: `ProviderHealthReport`, `HealthStatus` enum (Healthy/Degraded/Unhealthy)
- File: `src/shared/Tessera.Shared.Kernel/Providers/IHealthProvider.cs`

**T1.7** `Tessera.Shared.Kernel` — unit tests for primitives
- `Result<T>` tests (Ok/Err paths, Map/Bind)
- `TimeRange` tests (intersection, contains, Last(span) factory)
- `Page<T>` tests
- File: `tests/unit/core/Tessera.Shared.Unit/Primitives/`
- Acceptance: tests pass via `dotnet test`

**T1.8** `Tessera.Shared.Http` — Refit base extension
- `AddTesseraRefitClient<TClient>(this IServiceCollection, string baseUrl)` extension
- Wires Refit + OTel HTTP instrumentation + Polly resilience via `Microsoft.Extensions.Http.Resilience`
- File: `src/shared/Tessera.Shared.Http/RefitExtensions.cs`
- Acceptance: extension compiles, registration works (smoke test in T1.19)

**T1.9** `Tessera.Shared.Http` — `BearerTokenHandler`
- DelegatingHandler that adds `Authorization: Bearer <token>` from `IOptions<AuthOptions>`
- File: `src/shared/Tessera.Shared.Http/BearerTokenHandler.cs`

**T1.10** `Tessera.Providers.Victoria` — Refit `IVictoriaTracesClient`
- Jaeger-shaped endpoints: `GET /select/{tenant}/jaeger/api/services`, `GET /select/{tenant}/jaeger/api/traces`, `GET /select/{tenant}/jaeger/api/traces/{traceId}`
- Per `.agents/docs/victoria-stack.md` API reference
- File: `src/providers/Tessera.Providers.Victoria/Clients/IVictoriaTracesClient.cs`

**T1.11** `Tessera.Providers.Victoria` — Refit `IVictoriaLogsClient`
- LogsQL endpoints: `POST /select/{tenant}/jsonline/query` and/or `/select/{tenant}/logs/api/v1/query`
- Per `.agents/docs/victoria-stack.md`
- File: `src/providers/Tessera.Providers.Victoria/Clients/IVictoriaLogsClient.cs`

**T1.12** `Tessera.Providers.Victoria` — Jaeger / LogsQL DTOs
- Jaeger DTOs: `JaegerTrace`, `JaegerSpan`, `JaegerProcess`, `JaegerTag`, `JaegerLog`
- LogsQL DTOs: `VLLogEntry` (with `_msg`, `_stream`, `_time`, `_field` keys)
- File: `src/providers/Tessera.Providers.Victoria/Dto/*.cs`

**T1.13** `Tessera.Providers.Victoria` — `VictoriaTraceProvider` (impl)
- Implements `ITraceProvider`
- Calls `IVictoriaTracesClient`, maps Jaeger DTOs → domain `Trace`/`Span`
- Span tree NOT reconstructed here (modules do that)
- File: `src/providers/Tessera.Providers.Victoria/VictoriaTraceProvider.cs`

**T1.14** `Tessera.Providers.Victoria` — `VictoriaLogProvider` (impl)
- Implements `ILogProvider`
- Calls `IVictoriaLogsClient`, maps LogsQL DTOs → domain `LogEntry`
- File: `src/providers/Tessera.Providers.Victoria/VictoriaLogProvider.cs`

**T1.15** `Tessera.Providers.Victoria` — `VictoriaDiscoveryProvider`
- Implements `IDiscoveryProvider`
- Aggregates VT services + VL `_stream` values → deduplicated `Service[]`
- File: `src/providers/Tessera.Providers.Victoria/VictoriaDiscoveryProvider.cs`

**T1.16** `Tessera.Providers.Victoria` — `VictoriaHealthProvider`
- Implements `IHealthProvider`
- Probes `/health` (or `/metrics`) on each VT/VL backend; aggregates per-provider status
- File: `src/providers/Tessera.Providers.Victoria/VictoriaHealthProvider.cs`

**T1.17** `Tessera.Providers.Victoria` — `VictoriaOptions` + `AddVictoriaProvider`
- `VictoriaOptions { Traces.Url, Logs.Url, Tenant, TimeoutMs, AuthToken? }`
- `IValidateOptions<VictoriaOptions>` implementation
- `AddVictoriaProvider(IServiceCollection, IConfiguration)` registers all 4 providers
- File: `src/providers/Tessera.Providers.Victoria/VictoriaOptions.cs`, `VictoriaServiceCollectionExtensions.cs`

**T1.18** `Tessera.Providers.Victoria` — provider unit tests
- DTO ↔ domain mapping tests for each provider
- Tests in `tests/unit/providers/Tessera.Providers.Victoria.Unit/`
- NSubstitute for `IVictoriaTracesClient`/`IVictoriaLogsClient`
- Acceptance: tests pass

**T1.19** Phase 1 build + tests verification
- `dotnet build tessera.slnx -c Debug` exit 0, 0 warnings
- `dotnet test tests/unit/core/Tessera.Shared.Unit tests/unit/providers/Tessera.Providers.Victoria.Unit --no-build` exit 0
- Acceptance: build + tests green

**Commits during Phase 1:**
- `[.stbl](feat/shared-kernel): add primitives, domain types, provider interfaces`
- `[.stbl](feat/shared-http): add refit base + bearer handler`
- `[.stbl](feat/providers/victoria): add victoria client + mapping`
- `[.stbl](feat/providers/victoria): add health + discovery providers`

---

### Phase 2 — Backend modules (Health → Traces → Logs → Discovery)

Each module = endpoints + handlers + DI extension + unit tests. Modules reference
`Tessera.Shared.Kernel` only (consume provider interfaces, no direct reference to
`Tessera.Providers.Victoria`).

**T2.1** `Tessera.Modules.Health` — structure + endpoint
- AddProjectReference on `Tessera.Shared.Kernel`
- Folder: `Endpoints/`, `Handlers/`
- `GET /api/health` → composite of `IHealthProvider.CheckAsync()`
- File: `src/modules/Tessera.Modules.Health/Endpoints/HealthEndpoint.cs`

**T2.2** `Tessera.Modules.Health` — `HealthHandler` + DI + tests
- `HealthHandler` aggregates results from all registered `IHealthProvider`s
- `AddHealthModule(IServiceCollection)` extension
- 1-2 unit tests
- File: `src/modules/Tessera.Modules.Health/Handlers/HealthHandler.cs`, `DependencyInjection.cs`
- Acceptance: module registered, tests pass

**T2.3** `Tessera.Modules.Traces` — structure + DTOs
- Folder: `Models/`, `Handlers/`, `Endpoints/`
- DTOs: `TraceSummary`, `TraceDetail`, `ListTracesRequest`, `ListTracesResponse`
- File: `src/modules/Tessera.Modules.Traces/Models/*.cs`

**T2.4** `Tessera.Modules.Traces` — `ListTracesHandler`
- Maps query params (`service`, `operation`, `start`, `end`, `minDurationMs`, etc.) to `TraceSearchQuery`
- Calls `ITraceProvider.SearchAsync`, returns `ListTracesResponse`
- File: `src/modules/Tessera.Modules.Traces/Handlers/ListTracesHandler.cs`
- Unit tests: query mapping, response shape

**T2.5** `Tessera.Modules.Traces` — `GetTraceHandler` WITH LOG CORRELATION (KEY FEATURE)
- Calls `ITraceProvider.GetByIdAsync(traceId)`
- Calls `ILogProvider.ListByTraceAsync(traceId, trace.TimeRange)` (separate call, NOT inside provider)
- Reconstructs span tree from flat spans
- Returns `{ trace: TraceDetail, correlatedLogs: LogEntry[] }` — Kibana Observability shape
- File: `src/modules/Tessera.Modules.Traces/Handlers/GetTraceHandler.cs`
- Unit tests: span tree reconstruction, log correlation join

**T2.6** `Tessera.Modules.Traces` — endpoint mapping + DI extension
- `MapTracesEndpoints(this IEndpointRouteBuilder)` — registers both endpoints
- `AddTracesModule(IServiceCollection)` extension
- OpenAPI annotations per `api-design.md`
- File: `src/modules/Tessera.Modules.Traces/Endpoints/TracesEndpoint.cs`, `DependencyInjection.cs`

**T2.7** `Tessera.Modules.Logs` — structure + DTOs
- Folder: `Models/`, `Handlers/`, `Endpoints/`
- DTOs: `LogEntryDto`, `ListLogsRequest`, `LogsResponse`

**T2.8** `Tessera.Modules.Logs` — `ListLogsByTraceHandler` + `ListLogsHandler`
- `ListLogsByTraceHandler`: requires `trace_id`, calls `ILogProvider.ListByTraceAsync`
- `ListLogsHandler` (stretch): arbitrary LogsQL passthrough
- File: `src/modules/Tessera.Modules.Logs/Handlers/*.cs`

**T2.9** `Tessera.Modules.Logs` — endpoint mapping + DI + tests
- `MapLogsEndpoints` + `AddLogsModule`
- Unit tests

**T2.10** `Tessera.Modules.Discovery` — structure + handler + endpoint
- `GET /api/services` → aggregates via `IDiscoveryProvider.ListServicesAsync`
- `AddDiscoveryModule` extension
- Unit tests

**T2.11** Phase 2 build + tests verification
- All 4 modules build, unit tests pass
- Architecture test: no module references `Tessera.Providers.*` (will be formalized in T4.1)
- Acceptance: build + tests green

**Commits during Phase 2:**
- `[.stbl](feat/health): add health module`
- `[.stbl](feat/traces): add list traces handler + endpoint`
- `[.stbl](feat/traces): add get trace handler with log correlation`
- `[.stbl](feat/traces): wire endpoint mapping + di extension`
- `[.stbl](feat/logs): add logs module`
- `[.stbl](feat/discovery): add discovery module`

---

### Phase 3 — Backend host + shared plumbing

**T3.1** `Tessera.Shared.Validation` — `VictoriaOptionsValidator`
- `IValidateOptions<VictoriaOptions>` implementation per `di-options.md`
- File: `src/shared/Tessera.Shared.Validation/VictoriaOptionsValidator.cs`

**T3.2** `Tessera.Shared.Validation` — DI extension
- `AddTesseraValidation(IServiceCollection)` registers all validators

**T3.3** `Tessera.Shared.Telemetry` — OTel setup extension
- `AddTesseraTelemetry(IServiceCollection)` — wires ASP.NET Core + HTTP instrumentation
- Resource attributes per `.agents/docs/architecture/log-format.md`

**T3.4** `Tessera.Shared.Telemetry` — `DotCaseLogRecordProcessor`
- Per `.agents/docs/architecture/log-format.md` (PascalCase source → dot.case export)
- File: `src/shared/Tessera.Shared.Telemetry/Logging/DotCaseLogRecordProcessor.cs`

**T3.5** `Tessera.Shared.OpenApi` — Scalar wiring extension
- `AddTesseraOpenApi(IServiceCollection)` — configures OpenAPI document + Scalar UI

**T3.6** `Tessera.Host` — TOML config loading
- Use Tomlyn to parse `tessera.toml` from `./`, `/etc/tessera/`, or `TESSERA_CONFIG` env
- Map to `VictoriaOptions`, `ServerOptions`, `AuthOptions`
- File: `src/host/Tessera.Host/Configuration/TomlConfigurationProvider.cs`

**T3.7** `Tessera.Host` — DI registration (composition root)
- `AddTesseraRefitClient<IVictoriaTracesClient>(...)` (twice — once for traces, once for logs)
- `AddVictoriaProvider(...)`
- `AddTracesModule()`, `AddLogsModule()`, `AddDiscoveryModule()`, `AddHealthModule()`
- `AddTesseraValidation()`, `AddTesseraTelemetry()`, `AddTesseraOpenApi()`

**T3.8** `Tessera.Host` — admin bearer middleware (stub for MVP-01)
- Reads `TESSERA_ADMIN_TOKEN` env
- MVP-01: all endpoints are reads → middleware logs the token presence, allows guest
- Future: per-endpoint policies

**T3.9** `Tessera.Host` — endpoint mapping
- `app.MapTracesEndpoints()`, `app.MapLogsEndpoints()`, `app.MapDiscoveryEndpoints()`, `app.MapHealthEndpoints()`
- `app.MapOpenApi()` → `/openapi/v1.json`
- `app.MapScalarApiReference()` → `/scalar/v1`

**T3.10** `Tessera.Host` — run locally verification
- `dotnet run --project src/host/Tessera.Host` starts on port 1990
- `curl http://localhost:1990/api/health` returns 200 with composite health
- `curl http://localhost:1990/openapi/v1.json` returns OpenAPI doc
- Acceptance: Tessera.Host runs, endpoints respond

**Commits during Phase 3:**
- `[.stbl](feat/shared-validation): add victoria options validator`
- `[.stbl](feat/shared-telemetry): add otel + dotcase processor`
- `[.stbl](feat/shared-openapi): add scalar wiring`
- `[.stbl](feat/host): wire composition root + endpoints`

---

### Phase 4 — Tests

**T4.1** Architecture test: modules don't reference providers
- `Tessera.ArchitectureTests.NoModuleReferencesProviders()`
- Use NetArchTest: scan `Tessera.Modules.*` assemblies, assert no `HaveDependencyOnAny` `Tessera.Providers.*`
- Acceptance: test passes when isolation holds, fails loudly when violated

**T4.2** Architecture test: providers implement interfaces from Kernel
- Verify each `*Provider` class implements the corresponding `IXxxProvider` from `Tessera.Shared.Kernel`

**T4.3** Architecture test: host is composition root
- Verify `Tessera.Host` has ProjectReferences on all modules + `Tessera.Providers.Victoria`
- Anti-pattern guard: no other project has both refs

**T4.4** `Tessera.Host.UnitTests` — composition root wiring
- Test that all module + provider DI registrations resolve correctly
- File: `tests/unit/core/Tessera.Host.UnitTests/`

**T4.5** `Tessera.Victoria.Integration` — Testcontainers fixture
- `IAsyncLifetime` fixture for `victoriametrics/victoria-traces` + `victoriametrics/victoria-logs` containers
- Wait-for-ready logic per container
- File: `tests/integration/Tessera.Victoria.Integration/Fixtures/`

**T4.6** `Tessera.Victoria.Integration` — VT canary test
- Ingest sample trace, `GET /api/traces/{id}`, verify shape
- Smoke test for the full HTTP → Tessera.Host → Victoria roundtrip

**T4.7** `Tessera.Victoria.Integration` — VL canary test
- Ingest sample logs, `GET /api/logs?trace_id=...`, verify shape

**T4.8** `Tessera.Victoria.Integration` — E2E trace + log correlation
- Ingest trace + logs with matching `trace_id`
- `GET /api/traces/{id}` returns `{ trace, correlatedLogs }` with logs that match trace_id

**T4.9** Phase 4 verification
- All architecture tests pass
- All integration tests pass (with Testcontainers)
- `dotnet test tessera.slnx -c Debug --no-build` exit 0

**Commits during Phase 4:**
- `[.stbl](feat/tests/architecture): provider isolation rules`
- `[.stbl](feat/tests/unit): host composition root wiring`
- `[.stbl](feat/tests/integration): testcontainers victoria + E2E trace+log correlation`

---

### Phase 5 — Frontend integration (plexor SPA pattern)

**T5.0** Author `contracts/tessera.openapi.yaml` (design-first MVP-01 contract)
- 5 endpoints for MVP-01:
  - `GET /api/traces` — list traces (query: service, operation, start, end, minDurationMs, maxDurationMs, cursor, limit)
  - `GET /api/traces/{traceId}` — trace detail (response includes `correlatedLogs: LogEntry[]` — Kibana Observability shape)
  - `GET /api/logs` — query logs (query: trace_id, start, end, limit)
  - `GET /api/services` — service inventory
  - `GET /api/health` — composite health
- Schemas: `TraceSummary`, `TraceDetail`, `Span`, `LogEntry`, `Service`, `ProviderHealthReport`, request/response envelopes
- Errors per RFC 7807 ProblemDetails
- File: `contracts/tessera.openapi.yaml`
- Acceptance: file is valid OpenAPI 3.0/3.1, parsable by kubb, generates types/schemas without errors

**T5.1** Set up `web/tooling/codegen/` workspace package (plexor pattern)
- Mirror plexor: `web/tooling/codegen/{kubb.config.ts, kubb-plugin-filter/, package.json}`
- kubb 4.x with 7 plugins: TS, Client (fetch), Zod, React Query, Faker, MSW, custom Filter
- Initially custom Filter is passthrough (no-op); specific filters added as needed
- Input: `contracts/tessera.openapi.yaml` (design-first)
- Output: `web/apps/console/src/shared/api/src/`
- Bun workspace entry in `web/package.json`
- File: `web/tooling/codegen/{kubb.config.ts, kubb-plugin-filter/, package.json}`
- Acceptance: `bun install` succeeds, kubb plugin builds

**T5.2** Generate API layer + replace hand-written
- Run `bun run generate` from `web/tooling/codegen/`
- Generates `web/apps/console/src/shared/api/src/{types,client,hooks,schemas,fixtures,msw,filters}`
- Delete hand-written: `web/apps/console/src/shared/api/{types.ts, mock-data.ts, client.ts}` (replaced by generated)
- Create public barrel `web/apps/console/src/shared/api/index.ts` (re-export from `./src`)
- File: `web/apps/console/src/shared/api/`
- Acceptance: kubb generates without errors, barrel imports clean, features import from `@/shared/api`

**T5.3** Author MSW mocks
- `web/apps/console/src/shared/api/mocks/browser.ts` — MSW worker setup (`setupWorker(...handlers)`, `http.all('*', passthrough)` for non-API routes)
- `web/apps/console/src/shared/api/mocks/handlers.ts` — composed from kubb-generated per-operation handlers (`listTracesHandler`, `getTraceHandler`, `listLogsHandler`, `listServicesHandler`, `getHealthHandler`) + hand-curated sample traces/logs (deterministic via `faker.seed`)
- File: `web/apps/console/src/shared/api/mocks/{browser,handlers}.ts`
- Acceptance: MSW handlers intercept fetches correctly when worker starts

**T5.4** Wire `enableMocking()` in `main.tsx`
- Async function checks `import.meta.env.VITE_USE_MOCKS`
- When `true` → start MSW worker before first render
- When `false` (default) → real fetch to backend
- File: `web/apps/console/src/main.tsx`
- Acceptance: `VITE_USE_MOCKS=true` shows mock data; `=false` (or unset) attempts real fetch

**T5.5** Trace detail page — generated React Query hooks
- Route `/traces/$traceId`
- Uses kubb-generated `useGetTrace(traceId)` hook
- Renders waterfall + log panel side-by-side (existing `waterfall` + `log-entry` components)
- File: `web/apps/console/src/features/traces/trace-detail-page.tsx`

**T5.6** Click correlation: log → focus span
- Click log entry → scroll waterfall to corresponding span + highlight
- File: `web/apps/console/src/features/traces/trace-detail-page.tsx`

**T5.7** Click correlation: span → filter logs
- Click span in waterfall → filter logs panel to that `spanId` only
- File: `web/apps/console/src/features/traces/trace-detail-page.tsx`

**T5.8** Traces list page — refactor to generated hooks
- Replace hand-written mock data import with `useListTraces(filters)` hook
- File: `web/apps/console/src/features/traces/traces-page.tsx`

**T5.9** Logs page — refactor to generated hooks
- Replace mock data with `useListLogs(query)` hook
- File: `web/apps/console/src/features/logs/logs-page.tsx`

**T5.10** Services page — refactor to generated hooks
- Replace mock data with `useListServices()` hook
- File: `web/apps/console/src/features/services/services-page.tsx`

**T5.11** Settings page — read-only `tessera.toml` display + theme/language toggle
- New route `/settings`
- Show `tessera.toml` values (port, Victoria URLs, tenant) — display only
- Theme toggle (uses existing `lib/preferences`)
- Language switcher (uses existing `lib/i18n`)
- File: `web/apps/console/src/features/settings/settings-page.tsx`

**T5.12** Phase 5 verification
- `cd web && bun --filter '@tessera/console' gate` exit 0
- `cd web && bun --filter '@tessera/console' build` exit 0
- `bun run generate` in `web/tooling/codegen/` produces no errors
- Acceptance: FE builds + tests green

**Commits during Phase 5:**
- `[.stbl](feat/fe/contract): author design-first OpenAPI contract`
- `[.stbl](feat/fe/codegen): kubb workspace + generated API layer`
- `[.stbl](feat/fe/mocks): MSW handlers from generated factories`
- `[.stbl](feat/fe/mocks): enable mocking toggle via VITE_USE_MOCKS`
- `[.stbl](feat/fe/traces): trace detail page with generated hooks + log correlation`
- `[.stbl](feat/fe/traces): click correlation log↔span`
- `[.stbl](feat/fe/refactor): traces/logs/services pages use generated hooks`
- `[.stbl](feat/fe/settings): settings page (read-only + theme/language)`

---

### Phase 6 — E2E verification + handoff

**T6.1** Document VT + VL local setup
- Add `docker run` commands for VT/VL to HANDOFF.md
- Include volume mounts for data persistence

**T6.2** Document `Tessera.Host` run
- `dotnet run --project src/host/Tessera.Host` command
- `tessera.toml` example

**T6.3** Manual E2E test procedure
- Checklist: launch VT/VL → start Tessera.Host → start `bun dev` → navigate console → see real traces + correlated logs

**T6.4** Update `.agents/HANDOFF.md`
- Refresh "Что сделано" section
- Mark MVP-01 done in status line
- List MVP-02 scope

**T6.5** Final verification
- `dotnet build tessera.slnx -c Debug` exit 0, 0/0
- `dotnet test tessera.slnx -c Debug --no-build` exit 0
- `cd web && bun run gate` exit 0
- E2E smoke: launch stack, navigate console, see traces + logs

**Commit at end of Phase 6:** `[.stbl](feat/docs): refresh handoff after MVP-01`

---

## Acceptance

**Definition of done — MVP-01:**

1. **Build gate green:** `dotnet build tessera.slnx -c Debug` exit 0, **0 errors, 0 warnings**.
2. **Backend tests green:** `dotnet test tessera.slnx -c Debug --no-build` exit 0.
3. **Frontend tests green:** `cd web && bun run gate` exit 0.
4. **Provider isolation:** Architecture test `NoModuleReferencesProviders()` passes — `Tessera.Modules.*` cannot have `<ProjectReference>` on `Tessera.Providers.*`.
5. **Victoria provider complete:** `Tessera.Providers.Victoria` implements all 4 provider interfaces (`ITraceProvider`, `ILogProvider`, `IDiscoveryProvider`, `IHealthProvider`).
6. **Trace+log correlation:** `GET /api/traces/{traceId}` returns `{ trace: TraceDetail, correlatedLogs: LogEntry[] }` in single response (Kibana Observability style).
7. **Host composition:** `Tessera.Host` runs on port 1990; OpenAPI at `/openapi/v1.json`; Scalar UI at `/scalar/v1`.
8. **Frontend UX:** Trace detail page renders waterfall + log panel side-by-side; logs clickable to focus span; spans clickable to filter logs. Uses kubb-generated React Query hooks; MSW fallback via `VITE_USE_MOCKS=true`.
9. **E2E smoke:** With local VT + VL running, `dotnet run` + `bun dev` shows real traces + correlated logs in console.
10. **Architecture docs current:** `.agents/HANDOFF.md` + `.agents/STATE.md` reflect MVP-01 completion.

---

## Out of scope (deferred to MVP-02+)

- **Metrics module** — `IMetricsProvider`, VM client, frontend metrics views, RED dashboards. Deferred entirely per owner direction 2026-07-19.
- **Dashboards module** + dashboard editor UI (HANDOFF stretch).
- **Service Map module** + service dependency graph UI (HANDOFF stretch).
- **Production deployment** — Dockerfile, GitHub Actions CI, docker-compose stack.
- **Multi-tenant auth** — OIDC, LDAP, per-tenant data isolation.
- **Full Testcontainers integration suite** — MVP-01 has canary tests (T4.6-T4.8); comprehensive suite deferred.
- **Auth RBAC / per-endpoint policies** — admin bearer is MVP-01 minimal; middleware is permissive for reads.
- **Full Victoria feature parity** — Victoria provider covers what's used by MVP-01 endpoints; stretch endpoints may not be exhaustive.
- **Dashboard editor / settings write-back / alert rules UI.**
- **Frontend Performance optimizations** — large trace list virtualization, pagination cursor UI.

---

## Open questions (resolve during Phase 0 / discuss)

1. **Auth model granularity** — is admin bearer required for ANY MVP-01 endpoint, or are all MVP-01 reads guest-accessible?
   - **Default:** all MVP-01 endpoints are reads → guest-only; admin bearer wired in middleware but not enforced (logs presence). Future writes will gate on admin.

2. **Trace detail response shape** — flat `correlatedLogs: LogEntry[]` vs grouped-by-span `correlatedLogs: Record<SpanId, LogEntry[]>`?
   - **Default:** flat array with `spanId` field per entry; client groups / filters as needed. Keeps provider boundary clean.

3. **TOML schema** — single `tessera.toml` vs split `tessera.toml` + `tessera.local.toml`?
   - **Default:** single file, env-var overrides only (`TESSERA_VICTORIA__TRACES__URL` etc.).

4. **`Tessera.Providers.Victoria` package references** — does it pull Refit directly, or only via `Tessera.Shared.Http` extension?
   - **Default:** pulls Refit directly (it's a concrete client impl, not a shared abstraction). `Tessera.Shared.Http` provides the wiring pattern + DelegatingHandlers, not the package.

## Resolved during Phase 0 / discuss

Decisions confirmed interactively with owner before Phase 0 implementation. Plexor SPA pattern (T5) adopted from plexor repo after owner hint.

- **Log correlation strategy** → N+1 fetch in `GetTraceHandler`. Parallel `Task.WhenAll` of `ITraceProvider.GetByIdAsync` + `ILogProvider.ListByTraceAsync`. Clean provider abstraction, ~10-50ms latency cost acceptable for MVP-01.
- **Span tree reconstruction** → server-side in `Tessera.Modules.Traces.GetTraceHandler`. Flat spans in, nested tree out. Saves bandwidth (1KB vs 100KB for large traces).
- **OTel exporter destination** → console exporter only for MVP-01. OTLP/gRPC added in MVP-02.
- **Health on Victoria unreachable** → start degraded. `/api/health` returns 503 until Victoria responds, then auto-recovers.
- **CORS policy** → permissive in `Development`, restrictive in `Production` (allowed origins from `tessera.toml`).
- **Frontend mock fallback** → MSW service worker via `VITE_USE_MOCKS=true` (plexor model). Hand-written `mock-data.ts` deleted.
- **Frontend API client** → kubb codegen from design-first `contracts/tessera.openapi.yaml` (plexor model). Hand-written `client.ts` deleted.

---

## Notes

- **Provider abstraction is non-negotiable.** Modules reference `Tessera.Shared.Kernel` only. Concrete provider (Victoria) is wired via DI in `Tessera.Host/Program.cs`. Architecture test enforces (T4.1).
- **Metrics deferred entirely** — owner direction 2026-07-19. Do NOT scaffold `IMetricsProvider` or VM client in MVP-01. VictoriaMetrics package NOT added to `Directory.Packages.props` for MVP-01.
- **Span tree reconstruction lives in `Tessera.Modules.Traces` handler** (not in provider) — providers return flat spans, modules reconstruct tree. Matches Grafana model.
- **Log correlation is fetch-on-demand in the trace detail handler** — `ITraceProvider.GetByIdAsync` returns trace, handler separately calls `ILogProvider.ListByTraceAsync(traceId, range)`, combines in response. No cross-provider coupling inside providers.
- **FE pipeline = plexor model** — design-first OpenAPI contract (`contracts/tessera.openapi.yaml`) is source of truth for MVP-01. `web/tooling/codegen/` workspace runs kubb to generate types/client/hooks/schemas/fixtures/msw. FE consumes via React Query hooks; MSW intercepts via `VITE_USE_MOCKS=true`. When `Tessera.Host` emits real OpenAPI to `artifacts/openapi.json`, switch kubb input from contract to artifacts — screens stay untouched.
- **All work happens on branch `tessera-mvp`.** PLAN.md is committed first (per workflow); subsequent task commits follow `[.stbl](feat/...): subject` per `commit-format.md`.
- **Commits small and atomic.** Each task = one commit (or one PR-ready batch when grouped). Build gate (`dotnet build tessera.slnx -c Debug` → 0/0) runs before every commit per `build-verification.md`.