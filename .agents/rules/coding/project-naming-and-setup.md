---
description: project naming convention, repository root, slnx, decision tree for new projects, creating a project, internal structure
globs: ["**/*.csproj", "**/tessera.slnx", "**/Directory.Build.props", "**/Directory.Build.targets", "**/Directory.Packages.props"]
always: true
---

# Project naming, decision tree, setup

Этот файл — naming, decision tree для нового проекта, создание, internal
structure. Layer overview — в `project-layers.md`. Layer dependencies —
в `project-deps-and-tests.md`.

## 1. Repository root

Корень — **только** управляющие файлы и каталоги верхнего уровня.

| Файл | Назначение | Регистр |
|------|-----------|---------|
| `Directory.Build.props` / `.targets` / `.Packages.props` | Общие MSBuild свойства | **PascalCase** |
| `.editorconfig`, `.gitattributes`, `.gitignore` | Код-стайл, Git | как есть |
| `tessera.slnx` | Solution-файл | lowercase |
| `README.md`, `AGENTS.md` | Документация | UPPERCASE |

**Critical:** MSBuild на Linux (CI) — case-sensitive. `Directory.Build.props`
**обязан** быть в PascalCase, иначе `dotnet build` на Linux не подхватит.

**Чего не должно быть:** lock-файлы npm/bun/yarn в корне; `node_modules/`,
`bin/`, `obj/` (в `.gitignore`); дубли `*.sln` + `*.slnx`.

## 2. Solution file — `.slnx` only

Один формат — `.slnx` (XML из .NET SDK 9 / Rider 2024.3+).

**Запрет:** держать одновременно `tessera.sln` и `tessera.slnx`. Миграция:
`dotnet sln tessera.sln migrate` → `git rm tessera.sln`.

## 3. Project naming — REQUIRED convention

### Базовый шаблон для tessera

```
Tessera.<Layer>[.<Feature>]
```

| Слой | Префикс | Пример |
|------|---------|--------|
| `host/` | `Tessera.<EntryPoint>` | `Tessera.Host`, `Tessera.Build.Tools` |
| `shared/` | `Tessera.Shared.<Capability>` | `Tessera.Shared.Kernel`, `Tessera.Shared.Http` |
| `modules/` | `Tessera.Modules.<Feature>` | `Tessera.Modules.Traces`, `Tessera.Modules.Logs` |
| `providers/` | `Tessera.Providers.<Name>` | `Tessera.Providers.Victoria`, `Tessera.Providers.Tempo` (future) |
| `tests/unit/<group>/` | `<SourceProject>.Unit[.<SubGroup>]` | `Tessera.Modules.Traces.Unit` |
| `tests/integration/` | `<SourceProject>.Integration[.<SubGroup>]` | `Tessera.Host.Integration` |

### Глубина имён

```
Tessera.<Layer>.<Capability>      → ок (3 сегмента)
Tessera.Modules.<Feature>         → ок
Tessera.Shared.<Capability>.<Sub> → ок (для больших shared)
Tessera.Providers.<Name>          → ок (Grafana datasource model)
Глубже 4 — перебор. Поднимай на уровень вверх.
```

### Запрещённые имена

Имена-помойки: `Utils`, `Helpers`, `Common`, `Misc`, `Tools`, `Shared`,
`Core` (без квалификации), `Implement`, `Context`, `Manager`, `Service`
(как generic-категория — только как role-specific суффикс).

Имена-нарушения two-name system: `Tessera.Trace`, `Tessera.Log` (concepts
не должны быть project names — projects используют theme words или capability names).

## 4. Decision tree — куда класть новый проект

```
1. Запускаемое приложение (Main, Web host)?
   ├── Public HTTP API          → host/Tessera.Host/
   └── Build tools / SDK         → host/Tessera.Build.Tools/

2. MSBuild SDK / targets (gates)?
   → host/Tessera.Build.Tools/

3. Cross-cutting infrastructure (kernel, http, telemetry)?
   → shared/Tessera.Shared.<Capability>/

4. Vertical-slice feature (один feature = один .csproj)?
   → modules/Tessera.Modules.<Feature>/

5. Concrete data source backend (provider implementation)?
   → providers/Tessera.Providers.<Name>/
   (implements provider interfaces from Tessera.Shared.Kernel;
    e.g. Tessera.Providers.Victoria, Tessera.Providers.Tempo future)

6. Unit tests (per source project)?
   → tests/unit/<SourceProject>.Unit/

7. Integration tests (WebApplicationFactory + Testcontainers)?
   → tests/integration/<SourceProject>.Integration/

8. Architecture tests (NetArchTest rules)?
   → tests/unit/core/Tessera.ArchitectureTests/

9. Frontend?
   → web/apps/<app>/
```

Не подошёл ни один — **остановись и обсуди**. Новая папка верхнего уровня
— архитектурное решение.

## 5. Creating a new project — 5 шагов

```
1. Определить место                     (decision tree, §4)
2. Создать физическую папку             (mkdir)
3. Создать csproj                       (dotnet new <template>)
4. Добавить в solution                  (dotnet sln add --solution-folder)
5. Добавить ProjectReference            (dotnet add reference)
```

**Критично:** шаги 2 и 4 в этом порядке. Solution folder в `.slnx` **не
создаёт** физическую папку — она должна быть на диске **до** `sln add`.

**Шаблоны:** `classlib` для большинства; `webapi` для `Tessera.Host`;
`xunit` для тестов. Test framework фиксируется **один** на solution
(xUnit + NSubstitute + Shouldly + Bogus, по образцу plexor).

**Folder cap:** перед созданием нового проекта — проверь что в целевой папке
< 5 `.csproj`. Если 5 — **nest** папку (см. `module-structure-5-cap.md`).

**Solution folder = physical path** — должна точно соответствовать
физическому пути.

### Минимальный csproj

Общие свойства (`TargetFramework`, `Nullable`, `ImplicitUsings`,
`LangVersion`, `TreatWarningsAsErrors`) — в `Directory.Build.props`.
csproj содержит только `ProjectReference` и `PackageReference`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup />
  <ItemGroup>
    <ProjectReference Include="..\..\..\shared\Tessera.Shared.Kernel\Tessera.Shared.Kernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Refit" Version="7.*" />
  </ItemGroup>
</Project>
```

❌ Не дублировать: `TargetFramework`, `Nullable`, `ImplicitUsings`,
`LangVersion` (общее), версии пакетов (централизованно в `Directory.Packages.props`).

**Только `ProjectReference`** внутри solution. `PackageReference` — только
для внешних NuGet.

### CI проверка целостности

```bash
diff <(find src tests -name '*.csproj' | sort) \
     <(dotnet sln tessera.slnx list | grep '\.csproj$' | sort)
```

Расхождение → fail в CI. Защита от забытого `dotnet sln add`.

Удаление проекта: `dotnet sln remove` + `rm -rf`. Переименование —
**удаление + создание заново**, не переименование csproj.

## 6. Internal project structure

### Модуль (`Tessera.Modules.<Feature>`)

Modules consume provider **interfaces** from `Tessera.Shared.Kernel` —
NOT direct Refit clients. Refit + DTO mapping live in providers.

```
Tessera.Modules.Traces/
├── Tessera.Modules.Traces.csproj
├── Models/                              DTOs для HTTP responses
│   ├── TraceSummary.cs
│   ├── TraceDetail.cs
│   ├── ListTracesRequest.cs
│   └── ListTracesResponse.cs
├── Handlers/                            consume ITraceProvider, ILogProvider
│   ├── ListTracesHandler.cs
│   └── GetTraceHandler.cs               spans + log correlation
├── Endpoints/
│   └── TracesEndpoint.cs                minimal API group
└── DependencyInjection.cs               static AddTracesModule(this IServiceCollection)
```

### Provider (`Tessera.Providers.<Name>`)

Concrete backend implementation. Implements provider interfaces from
`Tessera.Shared.Kernel`, owns Refit clients + DTO mapping.

```
Tessera.Providers.Victoria/
├── Tessera.Providers.Victoria.csproj
├── Clients/                             Refit interfaces (provider-specific)
│   ├── IVictoriaTracesClient.cs
│   └── IVictoriaLogsClient.cs
├── Dto/                                 Provider-native DTOs (Jaeger, LogsQL, ...)
│   ├── JaegerTrace.cs
│   ├── JaegerSpan.cs
│   └── VLLogEntry.cs
├── VictoriaTraceProvider.cs             implements ITraceProvider
├── VictoriaLogProvider.cs               implements ILogProvider
├── VictoriaDiscoveryProvider.cs         implements IDiscoveryProvider
├── VictoriaHealthProvider.cs            implements IHealthProvider
├── VictoriaOptions.cs                   base URL, timeout, tenant, auth token
└── VictoriaServiceCollectionExtensions.cs  AddVictoriaProvider(this IServiceCollection)
```

❌ Папки `Helpers/`, `Utils/`, `Common/`, `Misc/`, `Tools/` внутри проекта
**запрещены**.

## Связанные правила

- `project-layers.md` — обзор слоёв
- `project-deps-and-tests.md` — layer dependencies, testing structure
- `module-structure-5-cap.md` — 5-project cap rule
- `naming-tessera-theme.md` — internal naming theme words
