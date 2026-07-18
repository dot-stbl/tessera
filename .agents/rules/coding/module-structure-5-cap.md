---
description: max 5 .csproj per folder in tessera repo; nest folders if more needed; enforced via NetArchTest
globs: ["**/*.csproj", "**/tessera.slnx"]
always: true
---

# Module structure — 5-project cap per folder

## Hard rule

В **любой** папке в `src/` или `tests/` максимум **5 `.csproj`-файлов**.

Если scope требует больше — **nest** папку, не нарушай cap.

## Why 5

- **Visual scan:** count проектов в папке не задумываясь.
- **IDE tree view:** папки < 6 проектов обычно collapse в один уровень,
  > 6 — распирают tree view.
- **Cognitive load:** папка с 10 проектами требует решения "что общего"
  вместо "что это"; папка с 5 — namespace-like.
- **Diff readability:** PR с изменениями в одной папке из 5 проектов —
  один coherent slice; в папке из 10 — random grab-bag.

## MVP structure (cap respected)

```
src/
├── host/                       (2 projects — under cap)
│   ├── Tessera.Host/
│   └── Tessera.Build.Tools/
├── shared/                     (5 projects — at cap)
│   ├── Tessera.Shared.Kernel/
│   ├── Tessera.Shared.Http/
│   ├── Tessera.Shared.Telemetry/
│   ├── Tessera.Shared.OpenApi/
│   └── Tessera.Shared.Validation/
└── modules/                    (4 projects — under cap, MVP)
    ├── Tessera.Modules.Traces/
    ├── Tessera.Modules.Logs/
    ├── Tessera.Modules.Discovery/
    └── Tessera.Modules.Health/

tests/
├── unit/
│   ├── modules/                (4 projects)
│   └── core/                   (3 projects)
└── integration/                (1 project)
```

## Nesting когда scope растёт

Когда scope вырастет (stretch: Metrics, ServiceMap, Alerts, SLO), модулей
станет > 5 — **nest** в `modules/core/` и `modules/extended/`:

```
src/modules/
├── core/                       (existing MVP modules)
│   ├── Tessera.Modules.Traces/
│   ├── Tessera.Modules.Logs/
│   ├── Tessera.Modules.Discovery/
│   └── Tessera.Modules.Health/
└── extended/                   (new stretch modules)
    ├── Tessera.Modules.Metrics/
    ├── Tessera.Modules.ServiceMap/
    ├── Tessera.Modules.Alerts/
    └── Tessera.Modules.Slos/
```

Cap не нарушен, structure объясняет что это **две фазы** scope.

## Enforcement — NetArchTest rule

`Tessera.ArchitectureTests` содержит custom rule:

```csharp
public sealed class FolderLimitsTests
{
    [Theory]
    [InlineData("src/host")]
    [InlineData("src/shared")]
    [InlineData("src/modules")]
    [InlineData("tests/unit/modules")]
    [InlineData("tests/unit/core")]
    public void NoFolder_HasMoreThanFiveProjects(string rootFolder)
    {
        var rootPath = Path.Combine(SolutionPath, rootFolder);

        // Recursively walk, check each folder's own csproj count
        var folders = Directory
            .GetDirectories(rootPath, "*", SearchOption.AllDirectories)
            .Append(rootPath);

        foreach (var folder in folders)
        {
            var csprojs = Directory.GetFiles(folder, "*.csproj");
            Assert.True(
                csprojs.Length <= 5,
                $"Folder '{ToRelative(folder)}' has {csprojs.Length} csproj files (max 5). Nest if more needed.");
        }
    }
}
```

Прогоняется как обычный test (`dotnet test tessera.slnx`). Failure → CI красный.

Правило добавлено в `Tessera.ArchitectureTests` через [Fact] methods,
не custom Roslyn analyzer (filesystem access не нужен на compile time).

## Good / Bad

```
# ✅ Good — flat when under cap
src/shared/
├── Tessera.Shared.Kernel/
├── Tessera.Shared.Http/
├── Tessera.Shared.Telemetry/
├── Tessera.Shared.OpenApi/
└── Tessera.Shared.Validation/    (5 — at cap, ok)

# ✅ Good — nested when over cap
src/modules/
├── core/                         (MVP modules)
│   ├── Tessera.Modules.Traces/
│   └── ...
└── extended/                     (stretch modules)
    ├── Tessera.Modules.Metrics/
    └── ...

# ❌ Bad — over cap
src/shared/
├── Tessera.Shared.Kernel/
├── Tessera.Shared.Http/
├── Tessera.Shared.Telemetry/
├── Tessera.Shared.OpenApi/
├── Tessera.Shared.Validation/
└── Tessera.Shared.Caching/      (6 — over cap, must nest)
```

## Когда трогать это правило

❌ **Никогда не** ослаблять cap до 6, 7, 10 "ради удобства". Если scope требует
большего — **nest**. Это и есть design pressure для здоровой структуры.

✅ **Добавить новое правило** в `Tessera.ArchitectureTests` если нужны другие
проверки (например, max depth nesting = 2, или max files per project = 50).

## Related

- `.agents/rules/coding/project-layers.md` — overview of layers
- `.agents/rules/coding/project-naming-and-setup.md` — project naming
- `.agents/rules/coding/project-deps-and-tests.md` — testing structure
- `.agents/rules/process/build-verification.md` — overall build gate
