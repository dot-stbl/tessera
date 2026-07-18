# HANDOFF — Tessera context for next session

> Скопируй содержимое этого файла в новую сессию pi (или передай в чат) чтобы продолжить работу над tessera без потери контекста.

## Что строим

**Tessera** — APM UI для Victoria stack (Traces + Logs + Metrics). Self-hosted web приложение. Trace viewer с waterfall, структурированный log search, service inventory, кастомные Grafana-style дашборды.

**Status:** pre-MVP scaffold. Frontend работает с mock data. Backend ещё нет.

**Stack:**
- **Backend:** .NET 10 (планируется) — ASP.NET Core minimal API, Refit + Polly + OpenTelemetry
- **Frontend:** React 19 + Vite 6 + TanStack Router + shadcn/ui (Tessera DS) + Tailwind 4
- **Data:** VictoriaMetrics + VictoriaLogs + VictoriaTraces (HTTP)
- **Storage:** SQLite + JSON files (no PostgreSQL)
- **Auth:** guest (anonymous) + admin (bearer token) в MVP; OIDC + LDAP в stretch

## Что сделано (15 commits)

### Документация (`.agents/docs/` + `.agents/rules/`)
- **20 rule файлов** — C# conventions, project structure, port pool, brand
- **17 doc файлов** — architecture, scope, victoria-stack, modules, dashboard-schema, ecosystem, ops (install/configure/troubleshoot/storage), design system, log format, config format, multi-tenancy, auth model, storage
- **1 submodule** — `assets/stbl/` (`.stbl/.github` repo) для brand assets

### Frontend scaffold (web/)
- **Config:** package.json, tsconfig.base.json, bunfig.toml, vite.config.ts, vitest.config.ts, eslint.config.js, components.json
- **Workspace structure:** bun workspaces (`apps/*`, `tooling/*`)
- **Static assets:** `assets/{lockup-tessera.svg, og-tessera.svg, favicon.svg}` + `.stbl` submodule
- **84 primitives** (Plexor shadcn DS, адаптировано) — button, card, dialog, table, etc.
- **5 data-table** (TanStack Table wrapper)
- **8 lib** (utils, hooks, theme, preferences, i18n, document-title, app-name)
- **9 APM компонентов** (log-level, duration, time-format, trace-id, span-row, waterfall, log-entry, dashboard-grid, index)
- **4 app-shell** (topnav, sidebar, page-template, nav-config)
- **4 pages** (traces, logs, services, dashboards) с mock data
- **API client** (`shared/api/`) с mock fallback — работает без backend
- **i18n:** English + Russian (full translation)
- **26 unit tests** passing (duration, log-level, time-format)
- **Playbook:** `web/playbook/index.html` — static HTML visual catalog, открывается в браузере без dev-сервера

### Порт pool (1990-2120)
- **1990** — Tessera.Host backend
- **1991** — Vite dev server
- **1992-1999** — reserved
- **2000-2120** — reserved (external dev tools)
- VT/VL/VM (10428/9428/8429) — НЕ в нашем pool (стандартные Victoria порты)

### Brand & design
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

### Backend (.NET 10) — критично
- `tessera.slnx` + `Tessera.Host` + `Tessera.Build.Tools`
- `Directory.Build.props` (plexor strict mode: TreatWarningsAsErrors, analyzers)
- `Directory.Packages.props` (Refit, OpenTelemetry, Scalar, xUnit, NSubstitute, Shouldly, Bogus, NetArchTest, Testcontainers)
- `src/shared/Tessera.Shared.{Kernel,Http,Telemetry,OpenApi,Validation}` (по образцу plexor)
- `src/modules/Tessera.Modules.{Traces,Logs,Discovery,Health,Dashboards}` (каждый module = 1 .csproj, max 5 per folder)
- `src/host/Tessera.Host/Program.cs` (composition root, minimal API endpoints)
- Health endpoint с per-Victoria probes
- Auth middleware (admin bearer token)

### Frontend — оставшиеся страницы
- **Trace detail page** (`/traces/$traceId`) — Waterfall + log panel + span detail
- **Settings page** — Victoria endpoints config, theme toggle, language switcher
- **Dashboard editor** — JSON schema implementation + panel renderers (trace_list/log_view/metric_chart/markdown)

### Тесты
- **Integration tests** — Testcontainers Victoria (vt/vl/vm в Docker), WebApplicationFactory
- **E2E** — НЕТ (агент-runtime-safety запрещает Playwright/Chromium)
- **Component tests** для оставшихся APM компонентов (Waterfall, LogEntry, TraceId)

### Operations
- **Сборка Docker image** — multi-stage Dockerfile
- **CI** — GitHub Actions (build gate per .stbl conventions)

### Планирование
- **PLAN.md** — файл с task-by-task acceptance criteria (scaffold через soly_workflow)
- **Phases:** MVP (4 модуля) → stretch (dashboards, metrics, service map) → out (SLO, alerts, multi-tenant auth)

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

## Структура репо (для навигации)

```
tessera/
├── README.md                              public README (с .by-stbl lockup)
├── assets/                                brand assets (lockup/og/favicon)
│   ├── lockup-tessera.svg                 "tessera by .stbl"
│   ├── og-tessera.svg                     OG card 1200×630
│   ├── favicon.svg                        Tessera mark (4-tile mosaic)
│   └── stbl/                              SUBMODULE (.stbl brand assets)
├── .agents/
│   ├── HANDOFF.md                         ← THIS FILE
│   ├── docs/                              17 doc файлов (architecture, modules, ops, etc.)
│   └── rules/                             20 rule файлов (C# conventions, project structure)
├── web/                                   frontend monorepo (bun workspaces)
│   ├── apps/console/                      @tessera/console (Vite + React)
│   │   ├── src/
│   │   │   ├── main.tsx                   entry
│   │   │   ├── router.tsx                 code-based TanStack Router
│   │   │   ├── shared/
│   │   │   │   ├── api/                   types + mock-data + client
│   │   │   │   ├── lib/                   utils, hooks, i18n, preferences
│   │   │   │   └── ui/
│   │   │   │       ├── apm/               9 APM-specific components
│   │   │   │       ├── app-shell/         topnav + sidebar + page-template
│   │   │   │       ├── primitives/        84 shadcn primitives
│   │   │   │       └── data-table/        5 TanStack Table components
│   │   │   └── features/                  page components
│   │   │       ├── traces/traces-page.tsx
│   │   │       ├── logs/logs-page.tsx
│   │   │       ├── services/services-page.tsx
│   │   │       └── dashboards/dashboards-page.tsx
│   │   └── tests/                         26 unit tests
│   ├── playbook/                          static HTML visual catalog (open in browser)
│   ├── tooling/eslint-config/             @tessera/eslint-config workspace package
│   └── package.json                       bun workspaces config
└── (backend/ НЕ СУЩЕСТВУЕТ)               ← следующий приоритет
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

## Следующие шаги (рекомендованный порядок)

1. **Backend .NET 10 skeleton** — `tessera.slnx` + Tessera.Host + Tessera.Build.Tools + Directory.Build.props
2. **Tessera.Shared.{Kernel,Http,Telemetry,OpenApi,Validation}** — базовые shared libraries
3. **Tessera.Modules.{Traces,Logs,Discovery,Health}** — 4 модуля MVP
4. **Tessera.Host endpoints** — minimal API с auth + Victoria clients
5. **Integration tests** — Testcontainers Victoria + WebApplicationFactory
6. **PLAN.md** — formal task-by-task через `soly_workflow new tessera-mvp`
7. **Settings page** + **Trace detail page** (UI)
8. **Dashboard editor** (stretch)
9. **Docker build** + **CI**
10. **Publish to github.com/dot-stbl/tessera** — transfer ownership (когда ready)

## Команды

```sh
# install
cd web && bun install

# tests
bun --filter '@tessera/console' test

# typecheck
bun --filter '@tessera/console' typecheck

# build
bun --filter '@tessera/console' build

# submodule update (brand assets)
git submodule update --remote assets/stbl

# commit format
git commit -m "[.stbl](feat/<area>): <subject>"

# check
dotnet build tessera.slnx -c Debug   # when backend exists
```

## Если ты новый агент

1. Прочитай `.agents/HANDOFF.md` (этот файл) целиком
2. Прочитай `.agents/docs/architecture.md` для overview
3. Прочитай `.agents/docs/ecosystem.md` для `.stbl` контекста
4. Прочитай `.agents/rules/coding/code-shape.md` + `async-and-tasks.md` + `anti-patterns.md` для C# conventions
5. Прочитай `.agents/rules/process/build-verification.md` для build gate
6. **Соблюдай rules** при написании любого кода. Self-audit перед commit (см. `worker-audit.md`)
7. **Не запускай** `vite dev` / `dotnet run` / любые long-lived processes (см. `agent-runtime-safety.md`)

---

Tessera state at HANDOFF date: 15 commits, 20 rules, 17 docs, 26 tests passing, 1 submodule, full frontend scaffold with mock data, backend pending.
