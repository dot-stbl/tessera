# HANDOFF — Tessera context for next session

> Скопируй содержимое этого файла в новую сессию pi (или передай в чат) чтобы продолжить работу над tessera без потери контекста.

## Что строим

**Tessera** — APM UI в стиле Grafana (multi-provider data sources), не Victoria-specific UI. Self-hosted web приложение. Trace viewer с waterfall, структурированный log search, service inventory, кастомные Grafana-style дашборды. MVP-01 ships with Victoria (Traces + Logs + Metrics) как **первый** provider; future phases добавят другие backends (Tempo, Jaeger, Loki) через тот же provider interface — без refactor модулей.

**Status:** pre-MVP scaffold. **Frontend работает с mock data. Backend skeleton landed, modules empty.**

### Strategic positioning (owner-set, 2026-07-19)

Tessera — **новый Grafana-аналог для APM**, не Victoria UI. Без своего collector/storage слоя (no Tempo/Mimir/etc.) — переиспользуем готовые коллекторы (VT/VL/VM сейчас, другие — потом).

Следствия для архитектуры:
- Новый top-level слой `src/providers/` для конкретных provider implementations
- `Tessera.Modules.<Feature>` consume provider **interfaces** из `Tessera.Shared.Kernel` (не знают о Victoria)
- Domain types (`Trace`, `Span`, `LogEntry`, `Service`) в shared/ в доменной форме, не Victoria/Jaeger DTO
- Mapping домен ↔ provider-specific DTO — внутри provider implementation
- Host composition root wires конкретного provider'а в DI

**Stack:**
- **Backend:** .NET 10 — ASP.NET Core minimal API, Refit + Polly + OpenTelemetry
- **Frontend:** React 19 + Vite 6 + TanStack Router + shadcn/ui (Tessera DS) + Tailwind 4
- **Data:** VictoriaMetrics + VictoriaLogs + VictoriaTraces (HTTP)
- **Storage:** SQLite + JSON files (no PostgreSQL)
- **Auth:** guest (anonymous) + admin (bearer token) в MVP; OIDC + LDAP в stretch

## Что сделано (19 commits)

### Документация (`.agents/docs/` + `.agents/rules/`)
- **20 rule файлов** — C# conventions, project structure, port pool, brand
- **17 doc файлов** — architecture, scope, victoria-stack, modules, dashboard-schema, ecosystem, ops (install/configure/troubleshoot/storage), design system, log format, config format, multi-tenancy, auth model, storage
- **1 submodule** — `assets/stbl/` (`.stbl/.github` repo) для brand assets

### Frontend scaffold (web/) — компилируется, тесты проходят
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

### Backend skeleton (src/) — собирается clean
- **`tessera.slnx`** — plexor-style Folder hierarchy (src/build, src/shared/{kernel,infra}, src/modules, src/host, tests/{unit,integration})
- **`Directory.Build.props`** — net10.0, nullable, warnings-as-errors, MinVer, container publish
- **`Directory.Packages.props`** — CPM disabled, central versions (Refit 12.1.0, OpenTelemetry 1.16.0, Scalar, xUnit, NSubstitute, Shouldly, Bogus, NetArchTest, Testcontainers, Respawn)
- **`Tessera.Build.Tools`** — MSBuild SDK project + placeholder `.targets` (gate fires once per solution build)
- **`Tessera.Host`** — ASP.NET Core minimal API stub (Program.cs возвращает version JSON)
- **`Tessera.Shared.{Kernel,Http,Telemetry,OpenApi,Validation}`** — 5 shared проектов (пустые, refs via ProjectReference)
- **`Tessera.Modules.{Traces,Logs,Discovery,Health}`** — 4 vertical-slice modules (пустые)
- **9 test проектов:**
  - `tests/unit/core/{Tessera.ArchitectureTests, Tessera.Host.UnitTests, Tessera.Shared.Unit}`
  - `tests/unit/modules/Tessera.Modules.{Traces,Logs,Discovery,Health}.Unit`
  - `tests/integration/{Tessera.Host.Integration, Tessera.Victoria.Integration}`

**Build verification:** `dotnet build tessera.slnx` → **20/20 проектов, 0 errors, 0 warnings**

**Pitfalls resolved (не повторять):**
- `Microsoft.Build.NoTargets` SDK не резолвится без версии → используем `Microsoft.NET.Sdk` (plexor pattern)
- Все `.csproj` ProjectReference-пути проверены; глубина для tests — 4 уровня (`..\..\..\..\src\...`), для host — 3 уровня (`..\..\..\src\...`), для shared — 2 уровня (`..\..\src\...`), для modules — 2 уровня (`..\..\src\...`)

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

## Что осталось сделать

### Backend — наполнить модули
**Shared (по приоритету):**
1. `Tessera.Shared.Kernel` — реальные типы (`Tenant`, `TraceId`, `SpanId`, `LogLevel`, `TimeRange`, `Page<T>`) + `Result<T>` + exception types
2. `Tessera.Shared.Http` — Refit-интерфейсы клиента к VT/VL/VM + `HttpClient` registration с Polly retries
3. `Tessera.Shared.Validation` — адаптеры `IValidateOptions<T>` для всех options classes
4. `Tessera.Shared.Telemetry` — `DotCaseLogRecordProcessor` + resource attributes
5. `Tessera.Shared.OpenApi` — Scalar UI wiring

**Modules (vertical slices, каждый = `core/` + `extended/` если >5 файлов):**
6. `Tessera.Modules.Traces` — `/api/traces*` endpoints + Refit → VictoriaTraces + 1-2 unit теста
7. `Tessera.Modules.Logs` — `/api/logs*` endpoints + Refit → VictoriaLogs (LogsQL passthrough)
8. `Tessera.Modules.Discovery` — `/api/services` (объединяет VT services + VL streams)
9. `Tessera.Modules.Health` — `/api/health` + per-Victoria probes
10. `Tessera.Modules.Dashboards` — `/api/dashboards*` (CRUD на JSON files)

**Host:**
11. `Tessera.Host/Program.cs` — composition root: TOML config + auth middleware + DI registration + endpoint mapping
12. Health endpoint с per-Victoria probes
13. Admin bearer token middleware (`TESSERA_ADMIN_TOKEN` env)

### Frontend — оставшиеся страницы
- **Trace detail page** (`/traces/$traceId`) — Waterfall + log panel + span detail
- **Settings page** — Victoria endpoints config, theme toggle, language switcher (mock пока)
- **Dashboard editor** — JSON schema implementation + panel renderers (trace_list/log_view/metric_chart/markdown/service_map/flame_graph)

### FE pipeline (MVP-01, plexor model)
- **Design-first OpenAPI contract** — `contracts/tessera.openapi.yaml` (hand-authored, source of truth for MVP-01 endpoints)
- **kubb codegen workspace** — `web/tooling/codegen/` runs kubb 4.x (7 plugins: TS / Client / Zod / React Query / Faker / MSW / custom Filter) to generate `web/apps/console/src/shared/api/src/`
- **MSW mock layer** — `web/apps/console/src/shared/api/mocks/{browser,handlers}.ts` composed from kubb-generated per-operation handlers + hand-curated sample traces/logs
- **Toggle** — `VITE_USE_MOCKS=true` runs MSW worker, `false` (default) hits real `Tessera.Host`
- **Replacement** — hand-written `mock-data.ts` + `client.ts` deleted; features import only from `@/shared/api` (public barrel)

### Тесты
- **Integration tests** — Testcontainers Victoria (`GenericContainer` для `victoriametrics/victoria-traces:v0.X.X` и др.), `WebApplicationFactory<Program>` для backend
- **Component tests** для оставшихся APM компонентов (Waterfall, LogEntry, TraceId)

### Operations
- **Сборка Docker image** — multi-stage Dockerfile (build + runtime)
- **CI** — GitHub Actions (build gate per .stbl conventions)
- **Docker compose** — `tessera + vt + vl + vm` стек для self-hosted

### Планирование
- **PLAN.md** — файл с task-by-task acceptance criteria (scaffold через `soly_workflow new tessera-mvp`)
- **Phases:** MVP (4 модуля + работающие endpoints) → stretch (dashboards, metrics, service map) → out (SLO, alerts, multi-tenant auth)

## Ключевые решения (locked)

| Decision | Choice | Date | Source |
|----------|--------|------|--------|
| Multi-tenancy | single-tenant, config-time | 2026-07-19 | `.agents/docs/architecture/multi-tenancy.md` |
| Log format | dot.case at export, PascalCase in source | 2026-07-19 | `.agents/docs/architecture/log-format.md` |
| Config format | TOML only (no appsettings.json) | 2026-07-19 | `.agents/docs/architecture/config-format.md` |
| Storage | SQLite + JSON files (no Postgres) | 2026-07-19 | `.agents/docs/operations/storage.md` |
| Auth model | guest (anon) + admin bearer; OIDC/LDAP stretch | 2026-07-19 | `.agents/docs/security/auth-model.md` |
| Dashboard schema | custom JSON, versioned, Grafana-inspired | 2026-07-19 | `.agents/docs/dashboard-schema.md` |
| Port pool | 1990-2120 (Tessera-specific) | 2026-07-19 | `.agents/rules/coding/project-ports.md` |
| Accent color | deep red (NOT blue like plexor) | 2026-07-19 | `.agents/docs/ui/design-system.md` § 0 |
| Localization | English + Russian (plexor pattern) | 2026-07-19 | `web/apps/console/src/shared/lib/i18n/` |
| Asset sync | git submodule at `assets/stbl/` | 2026-07-19 | `.agents/docs/ecosystem.md` |
| Brand mark | 5-tile scatter (4 black + 1 red) | 2026-07-19 | `assets/mark-tessera.svg` |
| Build SDK | `Microsoft.NET.Sdk` (NOT `Microsoft.Build.NoTargets`) | 2026-07-19 | `src/host/Tessera.Build.Tools/Tessera.Build.Tools.csproj` |
| Module structure | `core/` + `extended/` if >5 files (NetArchTest enforced) | 2026-07-19 | `.agents/rules/coding/module-structure-5-cap.md` |
| Backend SDK | .NET 10 (10.0.109+, latestFeature rollForward) | 2026-07-19 | `global.json` |
| Strategic positioning | Grafana-analogue APM (multi-provider), NOT Victoria UI | 2026-07-19 | `.agents/HANDOFF.md` § Strategic positioning |
| Provider layer | New top-level `src/providers/` (Grafana datasource model) | 2026-07-19 | `.agents/HANDOFF.md` § Strategic positioning |
| Provider abstraction | Built in MVP-01 (interfaces in Shared.Kernel, Victoria impl in Providers) | 2026-07-19 | `.agents/HANDOFF.md` § Strategic positioning |
| MVP-01 module scope | 4 modules: Traces + Logs + Discovery + Health | 2026-07-19 | `.agents/HANDOFF.md` § Что осталось сделать |

## Структура репо (для навигации)

```
tessera/
├── README.md                              public README (с .by-stbl lockup)
├── global.json                            .NET SDK pin (10.0.109, latestFeature)
├── Directory.Build.props                  C# strict mode (warnings-as-errors, analyzers, MinVer)
├── Directory.Packages.props               central package versions (CPM disabled)
├── tessera.slnx                           solution file (plexor-style Folder hierarchy)
├── assets/                                brand assets (lockup/og/mark/favicon)
│   ├── lockup-tessera.svg                 "tessera by .stbl"
│   ├── og-tessera.svg                     OG card 1200×630
│   ├── mark-tessera.svg                   canonical 80×80 mark (white bg)
│   ├── mark-tessera-transparent.svg       80×80 transparent, theme-aware
│   ├── favicon.svg                        32×32 transparent + theme-aware
│   ├── by-stbl.css                        .stbl org profile CSS
│   └── stbl/                              SUBMODULE (.stbl brand assets)
├── .agents/
│   ├── HANDOFF.md                         ← THIS FILE
│   ├── docs/                              17 doc файлов (architecture, modules, ops, etc.)
│   └── rules/                             20 rule файлов (C# conventions, project structure)
├── src/                                   BACKEND (.NET 10)
│   ├── host/
│   │   ├── Tessera.Host/                  ASP.NET Core minimal API stub
│   │   │   ├── Program.cs                 returns version JSON
│   │   │   └── Tessera.Host.csproj
│   │   └── Tessera.Build.Tools/           MSBuild SDK + .targets
│   │       ├── Tessera.Build.Tools.csproj
│   │       └── Tessera.Build.Tools.targets (placeholder)
│   ├── shared/
│   │   ├── Tessera.Shared.Kernel/         contracts/abstractions (empty)
│   │   ├── Tessera.Shared.Http/           Refit client + Polly (empty)
│   │   ├── Tessera.Shared.Telemetry/      OpenTelemetry + log format (empty)
│   │   ├── Tessera.Shared.OpenApi/        Scalar/OpenAPI wiring (empty)
│   │   └── Tessera.Shared.Validation/     options validators (empty)
│   ├── modules/
│   │   ├── Tessera.Modules.Traces/        traces endpoints (empty)
│   │   ├── Tessera.Modules.Logs/          logs endpoints (empty)
│   │   ├── Tessera.Modules.Discovery/     services + streams discovery (empty)
│   │   └── Tessera.Modules.Health/        per-Victoria probes (empty)
│   └── providers/                         NEW layer (MVP-01)
│       └── Tessera.Providers.Victoria/    Victoria client + DTO mapping (to scaffold)
├── tests/                                 BACKEND tests
│   ├── unit/
│   │   ├── core/
│   │   │   ├── Tessera.ArchitectureTests/ NetArchTest rules
│   │   │   ├── Tessera.Host.UnitTests/    Host unit tests
│   │   │   └── Tessera.Shared.Unit/       Shared unit tests
│   │   └── modules/
│   │       ├── Tessera.Modules.Traces.Unit/
│   │       ├── Tessera.Modules.Logs.Unit/
│   │       ├── Tessera.Modules.Discovery.Unit/
│   │       └── Tessera.Modules.Health.Unit/
│   └── integration/
│       ├── Tessera.Host.Integration/      WebApplicationFactory tests
│       └── Tessera.Victoria.Integration/  Testcontainers Victoria
└── web/                                   frontend monorepo (bun workspaces)
    ├── apps/console/                      @tessera/console (Vite + React)
    │   ├── src/
    │   │   ├── main.tsx                   entry
    │   │   ├── router.tsx                 code-based TanStack Router
    │   │   ├── shared/
    │   │   │   ├── api/                   types + mock-data + client
    │   │   │   ├── lib/                   utils, hooks, i18n, preferences
    │   │   │   └── ui/
    │   │   │       ├── apm/               9 APM-specific components
    │   │   │       ├── app-shell/         topnav + sidebar + page-template
    │   │   │       ├── primitives/        84 shadcn primitives
    │   │   │       └── data-table/        5 TanStack Table components
    │   │   └── features/                  page components
    │   │       ├── traces/traces-page.tsx
    │   │       ├── logs/logs-page.tsx
    │   │       ├── services/services-page.tsx
    │   │       └── dashboards/dashboards-page.tsx
    │   └── tests/                         26 unit tests
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

⚠️ **Агент-runtime-safety запрещает запуск `vite dev`** (long-lived dev server).

**Чтобы посмотреть UI в браузере:**
1. Открой `web/playbook/index.html` двойным кликом (статическая visual catalog — топбар, сайдбар, все APM компоненты)
2. Или локально: `cd web && bun install && bun --filter '@tessera/console' dev` (займёт ~10s на install, потом dev server на :1991)
3. **С mock data:** `vite dev` работает без backend (API client fallback)
4. **С real backend:** установи env `VITE_API_BASE_URL=http://localhost:1990` перед `vite dev`

**Чтобы запустить тесты:**
```sh
cd web && bun --filter '@tessera/console' test
# 26 tests passing (duration, log-level, time-format)
```

**Backend build (после заполнения модулей):**
```sh
dotnet build tessera.slnx -c Debug
# 20/20 projects, 0 errors, 0 warnings
dotnet test tessera.slnx -c Debug --no-build
```

## Следующие шаги (рекомендованный порядок, MVP-01)

Стратегия: сначала foundation (shared kernel + provider abstraction + Victoria provider), потом modules поверх.

1. **PLAN.md** — scaffold `soly_workflow new tessera-mvp`, flesh out via `discuss` + `plan` (task-by-task acceptance criteria)
2. **`Tessera.Shared.Kernel`** — domain types (`Trace`, `Span`, `LogEntry`, `Service`, `TimeRange`, `Page<T>`, `Result<T>`) + **provider interfaces** (`ITraceProvider`, `ILogProvider`, `IMetricsProvider`, `IDiscoveryProvider`, `IHealthProvider`)
3. **`Tessera.Providers.Victoria`** — Victoria Refit clients (VT/VL/VM) + DTO mapping домен ↔ Jaeger/LogsQL/Prometheus + `AddVictoriaProvider(...)` extension
4. **`Tessera.Shared.Http`** — Refit base + Polly + OTel HTTP plumbing (используется Victoria provider)
5. **`Tessera.Modules.Health`** — simplest module, consume `IHealthProvider` (DI verification)
6. **`Tessera.Modules.Traces`** — `/api/traces*`, consume `ITraceProvider`
7. **`Tessera.Modules.Logs`** — `/api/logs*`, consume `ILogProvider`
8. **`Tessera.Modules.Discovery`** — `/api/services`, consume `IDiscoveryProvider`
9. **`Tessera.Host/Program.cs`** — composition root: TOML config + auth + DI (`AddVictoriaProvider`) + endpoint mapping
10. **Integration tests** — Testcontainers + WebApplicationFactory
11. **Trace detail page** + **Settings page** (UI)
12. **Dashboard editor** (stretch)
13. **Docker build** + **CI**
14. **Publish to github.com/dot-stbl/tessera** — transfer ownership (когда ready)

## Команды

```sh
# === FRONTEND ===
# install
cd web && bun install

# tests
bun --filter '@tessera/console' test

# typecheck
bun --filter '@tessera/console' typecheck

# build
bun --filter '@tessera/console' build

# === BACKEND ===
# build (full solution)
cd C:/Users/bradw/source/stbl/tessera
dotnet build tessera.slnx -c Debug

# test (после заполнения модулей)
dotnet test tessera.slnx -c Debug --no-build

# отдельный проект
dotnet build src/shared/Tessera.Shared.Kernel/Tessera.Shared.Kernel.csproj

# === BRAND ===
# submodule update
git submodule update --remote assets/stbl

# === GIT ===
# commit format
git commit -m "[.stbl](feat/<area>): <subject>"
```

## Если ты новый агент

1. Прочитай `.agents/HANDOFF.md` (этот файл) целиком
2. Прочитай `.agents/docs/architecture.md` для overview
3. Прочитай `.agents/docs/ecosystem.md` для `.stbl` контекста
4. Прочитай `.agents/rules/coding/code-shape.md` + `async-and-tasks.md` + `anti-patterns.md` для C# conventions
5. Прочитай `.agents/rules/process/build-verification.md` для build gate
6. Прочитай `.agents/rules/coding/module-structure-5-cap.md` — понимать ограничения `core/`/`extended/` split
7. **Соблюдай rules** при написании любого кода. Self-audit перед commit (см. `worker-audit.md`)
8. **Не запускай** `vite dev` / `dotnet run` / любые long-lived processes (см. `agent-runtime-safety.md`)

---

Tessera state at HANDOFF date: **19 commits**, 20 rules, 17 docs, 26 frontend tests passing, 1 submodule, full frontend scaffold with mock data, **backend skeleton (20 .NET projects) builds clean**, modules empty (нужно наполнять).