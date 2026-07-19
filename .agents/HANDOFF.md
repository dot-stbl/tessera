# HANDOFF — Tessera context for next session

> Скопируй содержимое этого файла в новую сессию pi (или передай в чат) чтобы продолжить работу над tessera без потери контекста.

## Что строим

**Tessera** — APM UI в стиле Grafana (multi-provider data sources), не Victoria-specific UI. Self-hosted web приложение. Trace viewer с waterfall, структурированный log search, service inventory, кастомные Grafana-style дашборды. MVP-01 ships with Victoria (Traces + Logs + Metrics) как **первый** provider; future phases добавят другие backends (Tempo, Jaeger, Loki) через тот же provider interface — без refactor модулей.

**Status at handoff date (2026-07-19):** Phase 0/1/2/3 DONE. Phase 4 (architecture tests + integration) + Phase 5 (E2E + handoff to next). Frontend deferred to MVP-02 — Phase 3 runs on existing FE shell с mock data. Backend is functional: 4 controllers wire all 4 providers, ProblemDetails pipeline + admin bearer optional + OpenAPI doc + Scalar UI mounted at `/scalar/v1`.

### Strategic positioning (owner-set, 2026-07-19)

Tessera — **новый Grafana-аналог для APM**, не Victoria UI. Без своего collector/storage слоя (no Tempo/Mimir/etc.) — переиспользуем готовые коллекторы (VT/VL/VM сейчас, другие — потом).

Следствия для архитектуры:
- Новый top-level слой `src/providers/` для конкретных provider implementations
- `Tessera.Modules.<Feature>` consume provider **interfaces** из `Tessera.Shared.Kernel` (не знают о Victoria)
- Domain types (`Trace`, `Span`, `LogEntry`, `Service`) в shared/ в доменной форме, не Victoria/Jaeger DTO
- Mapping домен ↔ provider-specific DTO — внутри provider implementation
- Host composition root wires конкретного provider'а в DI

**Stack:**
- **Backend:** .NET 10 — ASP.NET Core controllers (matches plexor), Refit + Polly + OpenTelemetry
- **Frontend:** React 19 + Vite 6 + TanStack Router + shadcn/ui (Tessera DS) + Tailwind 4
- **Data:** VictoriaMetrics + VictoriaLogs + VictoriaTraces (HTTP)
- **Storage:** SQLite + JSON files (no PostgreSQL)
- **Auth:** guest (anonymous) + admin (bearer token) в MVP; OIDC + LDAP в stretch

## Что сделано (Phase 0/1/2/3, ~30 commits)

### Phase 0 — Documentation + scaffold
- **20+ rule файлов** в `.agents/rules/` — C# conventions, project structure, port pool, brand, commit format, build gate, worker-audit, agent-runtime-safety
- **17+ doc файлов** в `.agents/docs/` — architecture, scope, victoria-stack, modules, dashboard-schema, ecosystem, ops (install/configure/troubleshoot/storage), design system, log format, config format, multi-tenancy, auth model
- **1 submodule** — `assets/stbl/` (`.stbl/.github` repo) для brand assets
- **`tessera.slnx`** — plexor-style Folder hierarchy (src/build, src/shared/{kernel,infra}, src/modules, src/host, tests/{unit,integration})
- **`Directory.Build.props`** — net10.0, nullable, warnings-as-errors, MinVer, container publish, Riok.Mapperly source-gen
- **`Directory.Packages.props`** — CPM disabled, central versions (Refit 13.1.0, Microsoft.Extensions.Http.Resilience 10.8.0, Scalar 2.16, Mapperly 4.3.1, Tomlyn 2.x, Microsoft.AspNetCore.OpenApi 10.0.9, xUnit 2.9.x, NSubstitute, Shouldly, Bogus, NetArchTest, Testcontainers)
- **`Tessera.Build.Tools`** — MSBuild SDK project + `VerifyFormatOnBuild` target fires `dotnet format --verify-no-changes --severity hidden` once per solution build

### Phase 1 — Shared primitives + Tessera.Providers.Victoria
- **`Tessera.Shared.Kernel`** — domain primitives (`Tenant`, `TraceId`, `SpanId`, `LogLevel`, `TimeRange`, `Page<T>`, `Result<T>`, `Error`), domain models (`Trace`, `Span`, `LogEntry`, `ServiceSummary`), **4 provider interfaces** (`ITraceProvider`/`ILogProvider`/`IDiscoveryProvider`/`IHealthProvider`), `ProviderException` hierarchy, `Api/` namespace (`ApiRoutes` + `TesseraJsonOptions`), `Configuration/` namespace (TOML provider + path resolution + secret refs + `ServerOptions`).
- **`Tessera.Shared.Http`** — `RefitExtensions.AddTesseraRefitClient<T>` (bearer + Polly standard resilience + OTel), `BearerTokenHandler`, `HttpClientAuthOptions`.
- **`Tessera.Providers.Victoria`** — 4 provider implementations (Trace/Log/Discovery/Health), Jaeger + LogsQL DTOs, NDJSON parsing, span tree reconstruction (flat list → tree), `VictoriaServiceCollectionExtensions.AddVictoriaProvider(...)` DI extension. **34/34 unit tests passing.**

### Phase 2 — 4 backend modules as controllers (Plexor-aligned)
- **`Tessera.Modules.{Health,Discovery,Traces,Logs}`** — каждый с `[ApiController] : ControllerBase`, primary-ctor injection, `AddXxxModule()` extension method, `AddApplicationPart(...)` для discovery без host-side type reference, Mapperly-based `I<Module>Mapper` для entity → DTO projections, route template константа в `ApiRoutes.*` (`:length(32)` constraint на trace id в `Traces/[HttpGet(ApiRoutes.Trace)]`).
- Per-module **`Errors/<Module>Errors.cs`** static class with dot.case constants (`health.provider.unreachable`, `trace.not_found`, `logs.trace_id_required`).
- `ProviderException` thrown для не-2xx (404 / 502 / 504), per-endpoint try/catch **banned**, global `IExceptionHandler` (см. Phase 3) → ProblemDetails.

### Phase 3 — Host composition root (DONE 2026-07-19, 4 commits)
- **`Tessera.Host/Program.cs`** — declarative composition chain: `AddTesseraConfiguration()` (TOML main + local override) → `AddControllers().AddJsonOptions` (`JsonStringEnumConverter`) → `AddApplicationPart(...)` × 4 modules → `AddTesseraWebInfrastructure()` (ProblemDetails + IExceptionHandler + OpenAPI + Scalar) → `AddTesseraAdminAuthentication(builder.Configuration)` (bearer optional) → `AddAuthorization()` → `Add<Module>Module()` × 4 → `AddVictoriaProvider(builder.Configuration)`. Runtime pipeline: `UseExceptionHandler`/`UseStatusCodePages`/`UseAuthentication`/`UseAuthorization` → `MapGet("/")` version banner → `UseTesseraOpenApi()` → `MapControllers()`.
- **`Tessera.Shared.Web`** (renamed from `Tessera.Shared.OpenApi`) — `TesseraExceptionHandler` + `ProblemDetailsResponsesTransformer` + `ProblemDetailsResponseShims`. `WebInstallerExtensions.AddTesseraWebInfrastructure()` + `UseTesseraOpenApi()`. Holds `Microsoft.AspNetCore.OpenApi`/`Microsoft.OpenApi`/`Scalar.AspNetCore` PackageReferences with `PrivateAssets="all"` so source generator Microsoft.AspNetCore.OpenApi не утекает в Host.
- **`Tessera.Shared.Authentication`** (renamed from `Tessera.Shared.Telemetry`) — `AdminBearerHandler` (CryptographicOperations.FixedTimeEquals + отключается когда TESSERA_ADMIN_TOKEN unset → NoResult для всех запросов → 401 на admin endpoints), `AdminBearerConstants`, `AdminBearerCryptography`, `AdminBearerOptions`. `AuthenticationInstallerExtensions.AddTesseraAdminAuthentication(IConfiguration)`.
- **TOML config** — `TesseraConfigPaths.ResolveMainPath` (4-level lookup: TESSERA_CONFIG env > /etc/tessera > XDG > cwd) + `ResolveLocalOverridePath` (всегда tessera.local.toml рядом). `TesseraConfigurationExtensions.AddTesseraConfiguration(this IConfigurationBuilder)` chained на host `WebApplicationBuilder`. `SecretReference.Resolve` парсит `env:VAR` / `file:/path` prefixes inline. `tessera.toml.example` + `tessera.local.toml.example` committed, реальные `tessera.toml`/`tessera.local.toml` gitignored.
- **OpenAPI + ProblemDetails globally** — `AddOpenApi` с `ProblemDetailsResponsesTransformer : IOpenApiOperationTransformer` инжектит 400/404/409/500/502/503/504 problem-details responses on every operation (через `Responses.TryAdd`). Non-2xx `[ProducesResponseType]` per-endpoint **redundant**. Source generator Microsoft.AspNetCore.OpenApi работает в Web, не в Host.

### Frontend scaffold (web/) — компилируется, тесты проходят (MVP-02 deferred)
- **Config:** package.json, tsconfig.base.json, bunfig.toml, vite.config.ts, vitest.config.ts, eslint.config.js, components.json
- **Workspace structure:** bun workspaces (`apps/*`, `tooling/*`)
- **Static assets:** `assets/{lockup-tessera.svg, og-tessera.svg, mark-tessera.svg, mark-tessera-transparent.svg, favicon.svg}` + `.stbl` submodule
- **84 primitives** (Plexor shadcn DS, адаптировано) — button, card, dialog, table, etc.
- **5 data-table** (TanStack Table wrapper)
- **8 lib** (utils, hooks, theme, preferences, i18n, document-title, app-name)
- **9 APM компонентов** (log-level, duration, time-format, trace-id, span-row, waterfall, log-entry, dashboard-grid, index)
- **4 app-shell** (topnav, sidebar, page-template, nav-config)
- **4 pages** (traces, logs, services, dashboards) с mock data
- **API client** (`shared/api/`) с mock fallback — работает без backend (`VITE_API_BASE_URL` пустой → mock)
- **i18n:** English + Russian (full translation)
- **26 unit tests** passing (duration, log-level, time-format)
- **Playbook:** `web/playbook/index.html` — static HTML visual catalog, открывается в браузере без dev-сервера

### Solution structure at handoff
```
src/
├── host/
│   ├── Tessera.Host/                   composition root (Program.cs only)
│   │   ├── Program.cs                  declarative composition chain
│   │   ├── Tessera.Host.csproj         only ProjectReferences + analyzer-suppressions
│   │   ├── tessera.toml.example        TOML config template (gitignored real file)
│   │   ├── tessera.local.toml.example  TOML local override template
│   │   └── .gitignore                  ignores real tessera.toml/tessera.local.toml
│   └── Tessera.Build.Tools/            MSBuild SDK + VerifyFormatOnBuild target
├── shared/
│   ├── Tessera.Shared.Kernel/          domain primitives, provider interfaces, Api/, Configuration/
│   ├── Tessera.Shared.Http/            Refit base + Polly resilience + BearerTokenHandler
│   ├── Tessera.Shared.Web/             RFC 9457 ProblemDetails + OpenAPI source-gen + Scalar
│   ├── Tessera.Shared.Authentication/  admin bearer scheme (optional in MVP-01)
│   └── Tessera.Shared.Validation/      options validators (shell, no content yet)
├── modules/
│   ├── Tessera.Modules.Traces/         GET /api/v1/traces, GET /api/v1/traces/{traceId:length(32)}
│   ├── Tessera.Modules.Logs/           GET /api/v1/logs?traceId=... (MVP-01: trace correlation only)
│   ├── Tessera.Modules.Discovery/      GET /api/v1/services
│   └── Tessera.Modules.Health/         GET /api/v1/health (composite across providers)
├── providers/
│   └── Tessera.Providers.Victoria/     concrete impls of ITrace/Log/Discovery/HealthProvider
└── (Build.Tools  — см. host/)
```

### Текущая build + test status
- **`dotnet build tessera.slnx -c Debug`** → **22 проекта, 0 errors, 0 warnings**
- **`dotnet test Tessera.Shared.Unit`** → **13/13 passing** (primitives — TimeRange/Page/Result/Error/etc.)
- **`dotnet test Tessera.Providers.Victoria.Unit`** → **34/34 passing** (NDJSON, span tree, mappings)
- **Modules + Host + Architecture + Integration unit tests** — **blocked by .NET 10.0.110 testhost bug** (`lib/net10.0/` paths not loadable). Runs when SDK поднимается до 10.0.200+. Environment-specific issue, не code. Отслеживается в `.agents/STATE.md` Open questions.

**Pitfalls resolved (не повторять):**
- `Microsoft.Build.NoTargets` SDK не резолвится без версии → используем `Microsoft.NET.Sdk` (plexor pattern)
- Все `.csproj` ProjectReference-пути проверены; глубина для tests — 4 уровня (`..\..\..\..\src\...`), для host — 3 уровня (`..\..\..\src\...`), для shared — 2 уровня (`..\..\src\...`), для modules — 2 уровня (`..\..\src\...`)
- TOML provider требует `Microsoft.Extensions.Configuration` (не `.File`) для `FileConfigurationProvider` base class
- Tomlyn v2 namespace — `Tomlyn.Model.TomlTable/TomlArray` + `Tomlyn.TomlSerializer.Deserialize<TomlTable>(string)`
- Mapperly source generator требует `IncludeAssets` (без `PrivateAssets`) в `Directory.Build.props` чтобы генератор кода видел `[Mapper]` атрибуты в дочерних проектах
- Microsoft.AspNetCore.OpenApi source generator привязан к проекту, где вызывается `AddOpenApi()` — добавлять `PrivateAssets="all"` на PackageReference в Web.csproj чтобы Host не получил её через transitive ProjectReference
- `[Tags([...])]` — collection initializer в атрибуте (C# 12 collection expressions), НЕ `Tags = "..."` (те-string не работает в .NET 10 attribute syntax для `[Tags]`)
- IIS-style `[FromServices]` в controllers, primary ctor для non-DI классов, `[MapPropertyFromSource]` для Mapperly в `TracesMapping` (VM property `Trace` → response shape)

### Порт pool (1990-2120)
- **1990** — Tessera.Host backend
- **1991** — Vite dev server
- **1992-1999** — reserved
- **2000-2120** — reserved (external dev tools)
- VT/VL/VM (10428/9428/8429) — НЕ в нашем pool (стандартные Victoria порты)

### Brand & design
- **5-tile scatter mark** (финальный дизайн): 4 black tiles + 1 accent red, viewBox 32×32, 6px tiles + 1px radius (~3px gaps)
- **Variants:**
  - `mark-tessera.svg` — 80×80 white background (canonical, для OG/печати)
  - `mark-tessera-transparent.svg` — 80×80 transparent background (theme-aware: black↔white tiles)
  - `favicon.svg` — 32×32 transparent + theme-aware через `@media (prefers-color-scheme: dark)`
  - `lockup-tessera.svg` — "tessera by .stbl" (для README)
  - `og-tessera.svg` — OG card 1200×630
- **Pure B&W surfaces** (OKLCH grayscale)
- **Deep red accent** (`oklch(35% 0.18 25)` light / `oklch(70% 0.20 25)` dark) — отличает tessera от plexor
- **Status semantics:** `ok/err/warn/idle/slow` + log levels (trace/debug/info/warn/error/fatal)
- **Typography:** Onest sans + JetBrains Mono (numerics)
- **Component library:** Tessera DS (port of Plexor DS + APM extensions)

### `.stbl` brand compliance
- Tessera — продукт GitHub org `github.com/dot-stbl`
- Brand assets vendored via git submodule: `assets/stbl/`
- README follows `.stbl` template: `.by-stbl` lockup, "Tessera is built by .stbl"
- Commit format: `[.stbl](<feat/...>): <subject>` — это `.stbl` convention
- Org profile: `pure B&W · monospace only`

## Что осталось сделать (MVP-01 final, MVP-02 deferred)

### Phase 4 — Architecture tests + integration (NOT STARTED)
1. **`Tessera.ArchitectureTests`** — NetArchTest rules:
   - `NoModuleReferencesProviders` — modules → `src/providers/*` is banned (Grafana datasource model)
   - `NoCrossModuleReferences` — `Tessera.Modules.X` → `Tessera.Modules.Y` is banned (cross-module through shared abstractions only)
   - `NoHostReferencesFromModules` — modules → `Tessera.Host` is banned (composition root boundary)
   - `FolderCap` — максимум 5 проектов в `src/shared/` (текущий cap = 5 достигнут)
   - `FolderMaxFiles` — максимум 3 .cs файлов в одной folder в `src/`
   - `DomainHasNoFrameworkReferences` — `Tessera.Shared.Kernel` не должен reference ASP.NET Core / EF Core / HttpClient
   - **Blocked by .NET 10.0.110 testhost bug** — rule code пишется сразу, run когда SDK ≥ 10.0.200

2. **Test integration infrastructure** — Testcontainers VT/VL/VM + `WebApplicationFactory<Program>` для E2E в MVP-01. **Blocked** тем же SDK-багом + нужен работающий Victoria single-binary container.

### Phase 5 — E2E verify + handoff к MVP-02
1. **Локальный E2E** — поднять Victoria single-container (`victoriametrics/victoria-stack:v0.x.y`), запустить backend (`dotnet run --project src/host/Tessera.Host`), curl-ать все endpoints, верифицировать ProblemDetails format, OpenAPI doc на `/openapi/v1.json`, Scalar UI на `/scalar/v1`. **ВНИМАНИЕ**: агент-runtime-safety запрещает запуск `dotnet run`. Это делает **пользователь**.

2. **Doc refresh** (Phase 5):
   - `.agents/HANDOFF.md` — финальное состояние (Phase 5).
   - `.agents/docs/architecture.md` — уточнить wire-format секцию (UTC unix ms, ProviderException → ProblemDetails, admin bearer optional).
   - `.agents/docs/security/auth-model.md` — уточнить admin endpoints (нет в MVP-01, `[Authorize(Policy="admin")]` в MVP-02+).
   - `.agents/docs/operations/{install,configure}.md` — TOML config doc + `TESSERA_ADMIN_TOKEN` env var.

3. **Container publish + CI (Phase 5+ stretch)** — Dockerfile multi-stage (build + runtime ALPINE), GitHub Actions build gate, docker-compose стек с VT/VL/VM.

### Frontend — отложено в MVP-02
- **MVP-02 model** — design-first OpenAPI contract (`contracts/tessera.openapi.yaml`) → kubb codegen workspace (7 plugins: TS/Client/Zod/ReactQuery/Faker/MSW/custom-filter) → MSW mock layer → `VITE_USE_MOCKS` toggle. Заменяет существующий hand-written `mock-data.ts` + `client.ts` mock fallback.
- **Trace detail page** (`/traces/$traceId`) — Waterfall + log panel + span detail (real backend integration).
- **Settings page** — TOML config UI for Victoria endpoints, theme toggle, language switcher.
- **Dashboard editor** — JSON schema implementation + panel renderers (`trace_list/log_view/metric_chart/markdown/service_map/flame_graph`).

## Ключевые решения (locked)

| Decision | Choice | Date | Source |
|----------|--------|------|--------|
| Multi-tenancy | single-tenant, config-time (hardcoded `0`) | 2026-07-19 | `.agents/docs/architecture/multi-tenancy.md` |
| Log format | dot.case at export, PascalCase in source | 2026-07-19 | `.agents/docs/architecture/log-format.md` |
| Config format | TOML only (no appsettings.json); `tessera.toml` + optional `tessera.local.toml`; env-var prefix `TESSERA_*`; secrets via `env:VAR` / `file:/path` inline prefixes | 2026-07-19 | `.agents/docs/architecture/config-format.md` |
| Wire format | UTC unix milliseconds (`long`, named `*UnixMs`) across HTTP boundary; `DateTimeOffset` internally; `TimeProvider` injected, never `DateTime.UtcNow` | 2026-07-19 | `~/.agents/rules/csharp/time-and-wire-format.md` |
| Error format | RFC 9457 ProblemDetails (status, title, detail, type, code extension); typed `ProviderException` → `IExceptionHandler` → ProblemDetails; no `Result<T>` at HTTP boundary | 2026-07-19 | `~/.agents/rules/csharp/problem-details.md`, `error-mapping.md` |
| Storage | SQLite + JSON files (no Postgres) | 2026-07-19 | `.agents/docs/operations/storage.md` |
| Auth model | guest (anon) + admin bearer optional in MVP-01 (handler returns NoResult when `TESSERA_ADMIN_TOKEN` unset; admin endpoints reject 401); OIDC/LDAP stretch MVP-02+ | 2026-07-19 | `.agents/docs/security/auth-model.md` |
| Dashboard schema | custom JSON, versioned, Grafana-inspired | 2026-07-19 | `.agents/docs/dashboard-schema.md` |
| Port pool | 1990-2120 (Tessera-specific; backend 1990, Vite 1991) | 2026-07-19 | `.agents/rules/coding/project-ports.md` |
| Accent color | deep red (NOT blue like plexor) | 2026-07-19 | `.agents/docs/ui/design-system.md` § 0 |
| Localization | English + Russian (plexor pattern) | 2026-07-19 | `web/apps/console/src/shared/lib/i18n/` |
| Asset sync | git submodule at `assets/stbl/` | 2026-07-19 | `.agents/docs/ecosystem.md` |
| Brand mark | 5-tile scatter (4 black + 1 red) | 2026-07-19 | `assets/mark-tessera.svg` |
| Build SDK | `Microsoft.NET.Sdk` (NOT `Microsoft.Build.NoTargets`) | 2026-07-19 | `src/host/Tessera.Build.Tools/Tessera.Build.Tools.csproj` |
| Module structure | `core/` + `extended/` if >5 files (NetArchTest enforced) | 2026-07-19 | `.agents/rules/coding/module-structure-5-cap.md` |
| Backend SDK | .NET 10 (10.0.110+, latestFeature rollForward) | 2026-07-19 | `global.json` |
| Strategic positioning | Grafana-analogue APM (multi-provider), NOT Victoria UI | 2026-07-19 | `.agents/HANDOFF.md` § Strategic positioning |
| Provider layer | Top-level `src/providers/` (Grafana datasource model); providers wired only in `Tessera.Host` composition root | 2026-07-19 | `.agents/HANDOFF.md` § Strategic positioning |
| Provider abstraction | Built in MVP-01 (interfaces in `Shared.Kernel`, concrete impls in `Providers.<Name>`) | 2026-07-19 | project-layers.md |
| MVP-01 module scope | 4 modules: Traces + Logs + Discovery + Health (routes prefixed `/api/v1/`) | 2026-07-19 | api-design.md (controllers) |
| Controller pattern | `[ApiController] : ControllerBase` (NOT minimal API); primary-ctor DI; `IExceptionHandler` global; `AddApplicationPart` для discovery | 2026-07-19 | api-design.md |
| Host project role | composition root only — только `Program.cs` + config шаблоны; нет бизнес-логики; все infrastructure в shared projects (Web/Authentication/Kernel) | 2026-07-19 | commit `29485dc` |
| Mapperly | Riok.Mapperly 4.3.1 для entity → DTO mappings; `[Mapper(RequiredMappingStrategy = Target)]`; DTOs init-only property records (NOT positional records) | 2026-07-19 | `~/.agents/rules/csharp/mapping.md` |
| Compose model | Each shared project has its own `*InstallerExtensions` static class (`AddTesseraConfiguration`, `AddTesseraWebInfrastructure`, `AddTesseraAdminAuthentication`); host reads as flat composition chain | 2026-07-19 | di-installer.md |

## Структура репо (навигация в текущем state)

```
tessera/
├── README.md                              public README (с .by-stbl lockup)
├── global.json                            .NET SDK pin (10.0.110, latestFeature)
├── Directory.Build.props                  C# strict mode (warnings-as-errors, analyzers, MinVer, Mapperly)
├── Directory.Packages.props               central package versions (CPM disabled)
├── tessera.slnx                           solution file (plexor-style Folder hierarchy)
├── assets/                                brand assets (lockup/og/mark/favicon) — git submodule `assets/stbl/`
├── .agents/
│   ├── HANDOFF.md                         ← THIS FILE
│   ├── STATE.md                           current progress log + recent commits + open questions
│   ├── docs/                              20 doc файлов (architecture, modules, ops, etc.)
│   └── rules/                             20+ rule файлов (C# conventions, project structure, api-design)
├── src/                                   BACKEND (.NET 10)
│   ├── host/
│   │   ├── Tessera.Host/                  composition root only (Program.cs + config templates)
│   │   │   ├── Program.cs                 declarative AddTesseraConfiguration + AddTesseraWebInfrastructure +
│   │   │   │                              AddTesseraAdminAuthentication + AddXxxModule() chain + AddVictoriaProvider
│   │   │   ├── tessera.toml.example       TOML config template (committed)
│   │   │   ├── tessera.local.toml.example override template (committed)
│   │   │   ├── .gitignore                 ignores real tessera.toml / tessera.local.toml
│   │   │   └── Tessera.Host.csproj        только ProjectReferences + analyzer-suppressions (no package-level concerns)
│   │   └── Tessera.Build.Tools/           MSBuild SDK + VerifyFormatOnBuild target
│   ├── shared/
│   │   ├── Tessera.Shared.Kernel/         domain primitives + provider interfaces + Api/TesseraJsonOptions +
│   │   │                                 Configuration/{Paths,Source,Options}
│   │   ├── Tessera.Shared.Http/           RefitExtensions + BearerTokenHandler + HttpClientAuthOptions
│   │   ├── Tessera.Shared.Web/            TesseraExceptionHandler + ProblemDetailsResponsesTransformer +
│   │   │                                 WebInstallerExtensions (AddTesseraWebInfrastructure + UseTesseraOpenApi)
│   │   ├── Tessera.Shared.Authentication/ AdminBearerHandler + AdminBearerOptions + AdminBearerConstants +
│   │   │                                 AdminBearerCryptography + AuthenticationInstallerExtensions
│   │   └── Tessera.Shared.Validation/     shell, no content yet (для Phase 5+ options validators)
│   ├── modules/
│   │   ├── Tessera.Modules.Traces/        Controllers/TracesController + Mapping/TracesMapper + Errors/TracesErrors
│   │   ├── Tessera.Modules.Logs/          Controllers/LogsController + Errors/LogsErrors
│   │   ├── Tessera.Modules.Discovery/     Controllers/DiscoveryController
│   │   └── Tessera.Modules.Health/        Controllers/HealthController + Mapping/HealthMapper + Errors/HealthErrors
│   └── providers/
│       └── Tessera.Providers.Victoria/    ITrace/Log/Discovery/HealthProvider impls + DTOs + NDJSON parsing +
│                                         span tree reconstruction + VictoriaServiceCollectionExtensions
├── tests/                                 BACKEND tests
│   ├── unit/
│   │   ├── core/
│   │   │   ├── Tessera.ArchitectureTests/ NetArchTest rules (Phase 4 — blocked by SDK testhost bug)
│   │   │   ├── Tessera.Host.UnitTests/    Host unit tests (blocked)
│   │   │   └── Tessera.Shared.Unit/       Shared unit tests — 13/13 passing
│   │   └── modules/
│   │       ├── Tessera.Modules.Traces.Unit/    (blocked)
│   │       ├── Tessera.Modules.Logs.Unit/      (blocked)
│   │       ├── Tessera.Modules.Discovery.Unit/ (blocked)
│   │       └── Tessera.Modules.Health.Unit/    (blocked)
│   ├── unit/providers/
│   │   └── Tessera.Providers.Victoria.Unit/    34/34 passing
│   └── integration/
│       ├── Tessera.Host.Integration/      WebApplicationFactory tests (Phase 4 — blocked)
│       └── Tessera.Victoria.Integration/  Testcontainers Victoria (Phase 4 — blocked)
└── web/                                   frontend monorepo (bun workspaces) — MVP-02 deferred
    ├── apps/console/                      @tessera/console (Vite + React)
    │   └── src/{shared/{api,lib,ui},features}
    ├── playbook/                          static HTML visual catalog (open in browser)
    ├── tooling/eslint-config/             @tessera/eslint-config workspace package
    └── package.json                       bun workspaces config
```

## Ключевые ссылки (для next session)

- **Architecture overview** — `.agents/docs/architecture.md`
- **Victoria API reference** — `.agents/docs/victoria-stack.md` (Jaeger endpoints, LogsQL, Prometheus)
- **Modules contracts** — `.agents/docs/modules.md` (per-module endpoints)
- **Dashboard schema** — `.agents/docs/dashboard-schema.md` (panel types, variables)
- **`.stbl` ecosystem** — `.agents/docs/ecosystem.md` (brand, commit format, asset sync)
- **Design system** — `.agents/docs/ui/design-system.md` (B&W + red accent, APM extensions)
- **Coding rules** — `.agents/rules/coding/` (naming, async, anti-patterns, project structure)
- **Process rules** — `.agents/rules/process/` (build gate, commit format, worker audit, runtime safety)

## Run dev / see UI

⚠️ **Агент-runtime-safety запрещает запуск `vite dev` / `dotnet run`** (long-lived processes — могут убить runtime самого агента). Запускать имеет право **только пользователь в собственном терминале**.

**Чтобы посмотреть UI в браузере (пользователь делает):**
1. Открой `web/playbook/index.html` двойным кликом (статическая visual catalog — топбар, сайдбар, все APM компоненты, mock data)
2. Или `cd web && bun install && bun --filter '@tessera/console' dev` (vite dev server на :1991)
3. **С mock data:** `vite dev` работает без backend (API client fallback)
4. **С real backend:** установи env `VITE_API_BASE_URL=http://localhost:1990` перед `vite dev`

**Чтобы запустить backend (пользователь делает):**
```sh
dotnet run --project src/host/Tessera.Host
# Слушает на :1990. Создаёт `tessera.toml` рядом с бинарником, или берёт из /etc/tessera/, или XDG, или cwd.
# Endpoints после старта:
#   GET http://localhost:1990/                              → version banner JSON
#   GET http://localhost:1990/api/v1/health                 → composite provider health (200/503)
#   GET http://localhost:1990/api/v1/services              → service inventory (200)
#   GET http://localhost:1990/api/v1/traces?service=...     → trace search (200)
#   GET http://localhost:1990/api/v1/traces/{id}            → trace detail + correlated logs (200/404)
#   GET http://localhost:1990/api/v1/logs?traceId=...       → logs by trace id (200/400)
#   GET http://localhost:1990/openapi/v1.json               → OpenAPI 3.0 doc (for FE codegen)
#   GET http://localhost:1990/scalar/v1                     → Scalar UI for human exploration
```

## Следующие шаги (рекомендованный порядок, MVP-01 final)

**Phase 4 — Architecture tests + integration (current focus):**
1. **`Tessera.ArchitectureTests`** — пиши rules сейчас (заблокировано testhost-багом). 7 правил (см. §Что осталось сделать выше).
2. **Testcontainers setup** (Phase 5, зависит от шага 1) — `victoriametrics/victoria-stack` single-binary container для `Tessera.Victoria.Integration`.
3. **Local end-to-end verify** (Phase 5) — пользователь запускает `dotnet run`, агент через curl-агенты НЕ запускает (runtime-safety).

**Phase 5 — Doc refresh + container publish:**
4. **Обновить** `.agents/docs/architecture.md` (wire-format секция — UTC unix ms, ProviderException → ProblemDetails mapping, admin bearer optional).
5. **Обновить** `.agents/docs/security/auth-model.md` (admin endpoints — нет в MVP-01; admin scheme registered unconditionally so future `[Authorize(Policy="admin")]` works без host changes).
6. **Обновить** `.agents/docs/operations/{install,configure}.md` — TOML config example (`TesseraHostExtensions` + secrets prefixes).
7. **Container publish (stretch)** — Dockerfile multi-stage (build SDK 10 + runtime alpine), GitHub Actions.
8. **MVP-02 handoff** — когда backend stable, передать в cubb codegen workspace (FE integration — см. §Frontend deferred выше).

## Команды

```sh
# === BACKEND ===
cd C:/Users/bradw/source/stbl/tessera

# format (запустить ПЕРЕД каждым build — VerifyFormatOnBuild target полагается)
dotnet format tessera.slnx --severity hidden

# canonical build (компиляция + analyzers + format-gate за один прогон)
dotnet build tessera.slnx -c Debug
# expected: 22 проекта, 0 errors, 0 warnings

# unit tests (на рабочих проектах; остальные заблокированы testhost-багом .NET 10.0.110)
dotnet test tests/unit/core/Tessera.Shared.Unit/Tessera.Shared.Unit.csproj --no-build --nologo        # 13/13
dotnet test tests/unit/providers/Tessera.Providers.Victoria.Unit --no-build --nologo                  # 34/34

# full test (только когда testhost-баг починят, SDK ≥ 10.0.200)
dotnet test tessera.slnx -c Debug --no-build

# отдельный проект
dotnet build src/shared/Tessera.Shared.Kernel/Tessera.Shared.Kernel.csproj

# === FRONTEND ===
cd web
bun install                                  # one-time
bun --filter '@tessera/console' test         # 26 unit tests passing
bun --filter '@tessera/console' typecheck    # tsc --noEmit
bun --filter '@tessera/console' build        # production build → apps/console/dist
bun --filter '@tessera/console' lint         # eslint --max-warnings 0

# === BRAND ===
git submodule update --remote assets/stbl    # pull latest .stbl org assets

# === GIT ===
git commit -m "[.stbl](feat/<area>): <subject>" -- обязательно feat/ префикс
# `feat/meta` для rules/CI/build/format, `feat/docs` для доки, прочие area по слою.
```

## Если ты новый агент

1. Прочитай `.agents/HANDOFF.md` (этот файл) целиком — current state + decisions + repo navigation
2. Прочитай `.agents/STATE.md` — decision log + open questions + recent commits
3. Прочитай `.agents/docs/architecture.md` для overview решений
4. Прочитай `.agents/rules/coding/{code-shape,async-and-tasks,anti-patterns,naming-and-types,constructors-and-fields}.md` — C# conventions (project-local дополняют ~/.agents/rules/csharp/)
5. Прочитай `.agents/rules/coding/{project-layers,project-deps-and-tests,module-structure-5-cap}.md` — layer isolation (host/shared/modules/providers)
6. Прочитай `.agents/rules/coding/api-design.md` — controllers pattern (как Plexor)
7. Прочитай `.agents/rules/process/{build-verification,worker-audit}.md` — build gate + self-audit перед commit
8. Прочитай `~/.agents/rules/process/commit-format.md` — commit format
9. **Соблюдай rules** при написании любого кода. Self-audit `worker-audit.md` перед commit.
10. **Не запускай** `vite dev` / `dotnet run` / любые long-lived processes (см. `agent-runtime-safety.md`). Curl к localhost — OK если сервер уже запущен пользователем.

---

Tessera state at HANDOFF date (2026-07-19): **~30 commits**, 20+ rules, 20+ docs, 1 submodule; **MVP-01 Phase 0/1/2/3 DONE**, Phase 4/5 in progress (architecture tests + integration). Frontend scaffold работает с mock data (MVP-02 deferred). Backend functional: 4 controllers consume 4 provider interfaces, ProblemDetails pipeline + admin bearer optional + TOML config + OpenAPI doc/Scalar UI. **22 .NET projects build clean** (0 errors, 0 warnings); **47/47 unit tests passing** в Tessera.Shared.Unit + Tessera.Providers.Victoria.Unit. Modules/Host/Architecture/Integration tests **blocked** by .NET 10.0.110 SDK testhost bug (env-specific, separate issue), runnable on SDK ≥ 10.0.200.