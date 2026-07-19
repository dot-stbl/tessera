---
description: project layers overview — host/shared/modules with 5-project cap per folder
globs: ["**/*.csproj", "**/tessera.slnx"]
always: true
---

# Project layers

Этот файл — обзор слоёв solution. Naming/decision tree/setup — в
`project-naming-and-setup.md`. Layer dependencies + testing structure —
в `project-deps-and-tests.md`. Folder cap — в `module-structure-5-cap.md`.

## 1. Layers overview

Tessera — multi-provider APM UI в стиле Grafana (MVP-01 ships with Victoria
как первый provider; future phases добавят Tempo/Jaeger/Loki/etc. через
provider interfaces). Структура — **host / shared / modules / providers**,
без DDD-слоёв внутри модулей.

```
host/        ← entry points: Tessera.Host + Tessera.Build.Tools
shared/      ← cross-cutting: kernel (domain types + provider interfaces), http, telemetry, openapi, validation
modules/     ← vertical-slice features: traces, logs, discovery, health
providers/   ← concrete data source implementations (MVP-01: Tessera.Providers.Victoria)
tests/       ← разделено на unit/ + integration/
deploy/      ← docker, systemd, k8s manifests
web/         ← frontend (bun + Turbo monorepo)
.agents/     ← soly state (rules, docs, plans)
```

## 2. Folder cap — max 5 projects per folder

**Hard rule:** в любой папке в `src/` или `tests/` максимум **5 `.csproj`-файлов**.
Если нужно больше — **nest** папку (см. `module-structure-5-cap.md`).

```
src/
├── host/                       (2 projects)
├── shared/                     (5 projects — at cap)
├── modules/                    (4 projects — MVP, under cap)
└── providers/                  (1 project — MVP-01; grows as new backends added)
```

## 3. Layer responsibilities

### `host/` — entry points

**Tessera.Host** — ASP.NET Core controllers (matches plexor), DI composition, options binding, middleware (`UseExceptionHandler`, `UseStatusCodePages`), OpenAPI setup, ProblemDetails wiring, `AddVictoriaProvider(...)` composition.
**Tessera.Build.Tools** — MSBuild SDK + targets (VerifyFormatOnBuild, VerifyAntiPatternsOnBuild, VerifyFolderLimits — будущее).

Содержит:
- `Program.cs` (composition root)
- `appsettings.json`, `appsettings.Development.json`
- Options binding setup
- Module registration через `services.AddTracesModule()`, `AddLogsModule()`, etc.
- `AddControllers().AddApplicationPart(...)` × per-module assemblies
- `AddOpenApi(...)` + `AddProblemDetails()` + `AddExceptionHandler<TesseraExceptionHandler>()`

**Не должно быть:** бизнес-логика, Refit clients, models.

### `shared/` — cross-cutting libraries

Один проект = одна ответственность, не "shared dump":

| Project | Responsibility |
|---------|----------------|
| `Tessera.Shared.Kernel` | Domain primitives (`Tenant`, `TraceId`, `SpanId`, `LogLevel`, `TimeRange`, `Page<T>`, `Result<T>`, `Error`) + **provider interfaces** (`ITraceProvider`, `ILogProvider`, `IMetricsProvider`, `IDiscoveryProvider`, `IHealthProvider`) |
| `Tessera.Shared.Http` | Refit base + `Microsoft.Extensions.Http.Resilience` (Polly) + OTel HTTP instrumentation |
| `Tessera.Shared.Telemetry` | OpenTelemetry setup, structured logging helpers, tracing conventions |
| `Tessera.Shared.OpenApi` | Scalar.AspNetCore + Swashbuckle annotations, OpenAPI document configuration |
| `Tessera.Shared.Validation` | FluentValidation helpers, request validation extension methods |

**Не должно быть:** feature-specific логика (endpoints, handlers), прямые ссылки на `Tessera.Providers.*`.

### `providers/` — concrete data source backends (Grafana datasource model)

**MVP-01:** `Tessera.Providers.Victoria` — реализация provider interfaces
для Victoria stack (VictoriaTraces + VictoriaLogs + VictoriaMetrics).
Refit → Jaeger/LogsQL/Prometheus endpoints, DTO mapping домен ↔
provider-specific shapes. Wired в DI через `AddVictoriaProvider(...)`
extension в composition root.

**Future:** `Tessera.Providers.Tempo/`, `Tessera.Providers.Loki/`,
`Tessera.Providers.Mimir/` и т.д. — каждый реализует те же provider
interfaces из `Tessera.Shared.Kernel`. Добавление нового backend = новый
project в `providers/` + регистрация в composition root.

**Не должно быть:** endpoints, handlers, references на `Tessera.Modules.*`.

### `modules/` — vertical-slice features

**Один module = один feature = один `.csproj`.** Без DDD-слоёв внутри — модуль
содержит всё для своего feature.

MVP модули:
- `Tessera.Modules.Traces` — Trace list + detail (with ILogProvider correlation) via `ITraceProvider`
- `Tessera.Modules.Logs` — log query (MVP-01: trace correlation only) via `ILogProvider`
- `Tessera.Modules.Discovery` — Service inventory aggregator via `IDiscoveryProvider`
- `Tessera.Modules.Health` — `/health` endpoint + per-Victoria probes via `IHealthProvider`

Stretch (когда вырастет scope):
- `Tessera.Modules.Metrics` — RED-метрики, flame graph
- `Tessera.Modules.ServiceMap` — граф зависимостей
- `Tessera.Modules.Alerts` — правила алертинга
- `Tessera.Modules.Slos` — SLO бюджеты

Когда модулей > 5, **nest**:
```
src/modules/
├── core/                       (MVP)
└── extended/                   (stretch)
```

Содержимое модуля (типичный шаблон):
```
Tessera.Modules.Traces/
├── Tessera.Modules.Traces.csproj
├── Controllers/
│   └── TracesController.cs             [ApiController] + ControllerBase
├── Contracts/
│   ├── ListTracesRequest.cs            [FromQuery] input
│   └── GetTraceResponse.cs             response DTO
├── Mapping/
│   ├── ITracesMapper.cs                domain → DTO contract
│   └── TracesMapper.cs                 [Mapper] partial impl (Riok.Mapperly)
├── Endpoints/
│   └── TracesEndpointHelpers.cs        internal static — query shaping (TimeRange, LogQuery)
├── Errors/
│   └── TracesErrors.cs                 dot.case error code constants
└── DependencyInjection/
    └── TracesModuleExtensions.cs        static AddTracesModule(this IServiceCollection)
```

**Cross-module communication:** через интерфейсы в `shared/` или через composition root в `host/`. Модули НЕ ссылаются друг на друга напрямую (см. `project-deps-and-tests.md`).

### `tests/`

```
tests/
├── unit/
│   ├── modules/                # per-module unit tests
│   └── core/                   # host, shared, architecture tests
└── integration/                # WebApplicationFactory + Testcontainers Victoria
```

Подробнее — `project-deps-and-tests.md` §3.

### `deploy/`

```
deploy/
├── docker/Dockerfile            multi-stage, linux/amd64 + arm64
├── systemd/tessera.service      bare-metal вариант
└── k8s/                         helm chart (stretch)
```

### `web/`

bun + Turbo monorepo (см. `architecture/web-routing.md` когда будет):
```
web/
├── apps/console/                Vite + React 19 + shadcn/ui + Tailwind
├── package.json                 bun workspace root
└── bunfig.toml
```

## 4. Anti-patterns

```
❌ src/shared/Utils/, src/shared/Helpers/, src/shared/Common/
❌ Один проект Tessera.Api вместо host + modules
❌ Модули со ссылками друг на друга (cross-feature imports)
❌ Бизнес-логика в Tessera.Host (composition root only)
❌ Models от модуля в Tessera.Shared.* (feature-specific в shared)
```

## Связанные правила

- `project-naming-and-setup.md` — naming, decision tree, новый проект
- `project-deps-and-tests.md` — layer dependencies, testing structure
- `module-structure-5-cap.md` — 5-project cap rule (enforced via NetArchTest)
- `naming-tessera-theme.md` — two-name system, theme words для internal naming
