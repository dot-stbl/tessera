---
description: layer dependencies (только вниз), testing structure (unit/ + integration/), anti-patterns организации проекта
globs: ["**/*.csproj", "**/tessera.slnx"]
always: true
---

# Layer dependencies & testing structure

Этот файл — правила ссылок между слоями, структура тестов, anti-patterns
организации. Layers overview — в `project-layers.md`. Naming/setup — в
`project-naming-and-setup.md`.

## 1. Layer dependencies — правило: ссылки только вниз

```
shared/        ←  leaf (только nuget, никаких project refs)

modules/       ─→  shared/        (consume provider interfaces)
providers/     ─→  shared/        (implement provider interfaces)

host/          ─→  modules/       ─┐
                ─→  providers/     ─┼─→  shared/
                ─→  shared/         ─┘

shared/        ─×  modules/      (запрещено)
shared/        ─×  host/         (запрещено)
shared/        ─×  providers/    (запрещено)
modules/       ─×  providers/    (запрещено — модуль знает только интерфейсы из shared)
modules/       ─×  host/         (запрещено)
modules/       ─×  друг на друга (запрещено)
providers/     ─×  modules/      (запрещено)
providers/     ─×  host/         (запрещено)
providers/     ─×  друг на друга (запрещено)
```

### Конкретные разрешённые ссылки

| Слой | Может ссылаться на |
|------|-------------------|
| `host/Tessera.Host` | `modules/*`, `providers/*`, `shared/*` (composition root wires everything) |
| `host/Tessera.Build.Tools` | ничего (MSBuild-only) |
| `shared/Tessera.Shared.*` | ничего (только nuget) |
| `modules/Tessera.Modules.Traces` | `shared/*` (**НЕ** `providers/*`, **НЕ** другие modules) |
| `modules/Tessera.Modules.Logs` | `shared/*` (**НЕ** `providers/*`, **НЕ** другие modules) |
| `modules/Tessera.Modules.Discovery` | `shared/*` (**НЕ** `providers/*`, **НЕ** другие modules) |
| `modules/Tessera.Modules.Health` | `shared/*` (**НЕ** `providers/*`, **НЕ** другие modules) |
| `providers/Tessera.Providers.Victoria` | `shared/*` only (**НЕ** `modules/*`, **НЕ** `host/`) |
| `tests/*` | любое из `src/` |

### Provider isolation (Grafana datasource model)

**Hard rule:** `Tessera.Modules.*` **НЕ МОЖЕТ** иметь `<ProjectReference>` на
`Tessera.Providers.*`. Модули потребляют **только интерфейсы** из
`Tessera.Shared.Kernel` (`ITraceProvider`, `ILogProvider`,
`IDiscoveryProvider`, `IHealthProvider`). Конкретная реализация
(например `VictoriaTraceProvider`) подключается в DI **только** в
`Tessera.Host/Program.cs` через `AddVictoriaProvider(...)`.

Зачем:
- **Testability** — модули тестируются с mock-провайдерами без поднятия
  VT/VL контейнеров.
- **Provider abstraction** — добавление Tempo/Jaeger/Loki в MVP-02+ = новый
  `Tessera.Providers.<Name>` без изменения модулей.
- **No accidental coupling** — модуль не может случайно вызвать
  Victoria-specific метод, не покрытый интерфейсом.

Проверяется автоматически в `tests/unit/core/Tessera.ArchitectureTests/`
через NetArchTest (правило `NoModuleReferencesProviders`).

### Cross-module communication

**Главное правило:** модули **не ссылаются** друг на друга напрямую.

Если `Tessera.Modules.Traces` нужны данные из `Tessera.Modules.Logs`
(например, для cross-source запросов), один из:

1. **Интерфейс в `shared/`** — `Tessera.Modules.Logs` экспортирует
   `IVictoriaLogsClient` через `Tessera.Shared.Http`, регистрирует в DI.
2. **Composition root в host** — `Tessera.Host` инжектит оба модуля и
   связывает их через endpoint (например, `/api/traces/{id}/logs` =
   `TracesEndpoint` оркеструет `IVictoriaLogsClient` + `IVictoriaTracesClient`).
3. **Event-based** — `Tessera.Modules.X` публикует events, `Tessera.Modules.Y` подписывается.
   (Не используется в MVP.)

Проверяется автоматически в `tests/unit/core/Tessera.ArchitectureTests/`
через NetArchTest.

## 2. Anti-patterns

### Технический долг в имени папки

```
❌ modules/.../Repositories_Old/
❌ shared/.../Deprecated/
```

Либо удалить сразу, либо issue с дедлайном. Не хранить "на всякий случай".

### Циклы зависимостей через DI

```csharp
// ❌ modules/Traces регистрирует реализацию из modules/Logs → cycle
services.AddSingleton<ITraceFetcher, TracesModule.FetcherUsingLogs>();
```

Решение: общая абстракция в `shared/` или в composition root.

### Бизнес-логика в `host/`

`Tessera.Host` — **только** composition + bootstrap. Никаких handlers,
Refit clients, query planning. Всё это — в модулях.

### Cross-module imports

```csharp
// ❌ Прямой импорт — нарушает rule
using Tessera.Modules.Logs.Handlers;

// ✅ Косвенный — через shared интерфейс
using Tessera.Shared.Http;  // IVictoriaLogsClient
```

### Папки с именами-помойками

`Helpers/`, `Utils/`, `Common/`, `Misc/`, `Tools/`, `Stuff/`.

### Один большой проект вместо нескольких

```
❌ Tessera.Api/
   ├── Features/Traces/
   ├── Features/Logs/
   ├── Features/Discovery/
   ├── Features/Health/
   └── ...
```

Критерий выноса в отдельный module: feature изолирован, не зависит от других
features, имеет свой Refit client / configuration / deployable concern.

## 3. Testing structure — `tests/unit/` + `tests/integration/`

Физическое разделение по категориям (не плоско). Категория дублируется и
в имени проекта, и в под-папке.

```
tests/
├── unit/
│   ├── modules/                              # per-module unit tests
│   │   ├── Tessera.Modules.Traces.Unit/
│   │   ├── Tessera.Modules.Logs.Unit/
│   │   ├── Tessera.Modules.Discovery.Unit/
│   │   └── Tessera.Modules.Health.Unit/
│   └── core/                                 # host, shared, architecture
│       ├── Tessera.Host.UnitTests/
│       ├── Tessera.Shared.Unit/
│       └── Tessera.ArchitectureTests/        # NetArchTest rules
└── integration/                              # WebApplicationFactory + Testcontainers
    ├── Tessera.Host.Integration/
    └── Tessera.Victoria.Integration/
```

`tests/` — множественное число. НЕ `test/`.
`Tessera.ArchitectureTests` живёт в `unit/core/` — это reflection/convention
тесты без I/O.

### Folder cap

В `tests/unit/modules/` — max 4 модульных тест-проекта (под 5-cap rule).
Когда модулей > 4, **nest** в `tests/unit/modules/core/` и `tests/unit/modules/extended/`.

В `tests/unit/core/` — max 5 проектов. Обычно: `Host.UnitTests`, `Shared.Unit`,
`ArchitectureTests`, + 2 запасных. Если больше — **nest**.

### Нейминг

```
<SourceProject>.<TestKind>[.<SubGroup>]
```

- `SourceProject` — обязательно.
- `TestKind` — `Unit` | `Integration`, обязательно.
- `SubGroup` — опционально.

Примеры:
- `Tessera.Modules.Traces.Unit`
- `Tessera.Host.UnitTests`
- `Tessera.ArchitectureTests` (один на solution)
- `Tessera.Host.Integration`

### TestKind

| TestKind | Когда |
|----------|-------|
| `Unit` | Моки, in-memory, < 100ms каждый |
| `Integration` | Реальные зависимости: Victoria через Testcontainers, HTTP через `WebApplicationFactory` |
| `Benchmarks` | BenchmarkDotNet |

❌ Не должно быть: тестов внутри src, папки `test/` (ед. число), тест-проектов
лежащих прямо в `tests/`, одного тест-проекта на несколько src
(исключение — `Tessera.ArchitectureTests`).

## Связанные правила

- `project-layers.md` — обзор слоёв
- `project-naming-and-setup.md` — naming, decision tree
- `testing-stack-and-pyramid.md` — test stack
- `testing-unit.md` — unit-тесты подробно
- `testing-integration.md` — integration-тесты подробно
- `module-structure-5-cap.md` — folder cap rule
