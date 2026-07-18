---
description: Mandatory self-audit gate for worker subagents — run after writing code, before git commit. Catches analyzer violations, cross-checks against loaded rules, fills rule gaps via analyzer-coach skill.
priority: high
---

# Worker self-audit gate

После того как ты написал код, и **до** `git commit` — ОБЯЗАТЕЛЬНО прогон
этого gate. Цель: поймать нарушения правил которые ты внёс, и
обнаружить пробелы где существующих правил не хватает.

Skipping = полагаться на CI чтобы поймать то что ты пропустил. Это
противоположно self-verification.

## Шаги (по порядку)

### 0. Mechanical: format drift (ОБЯЗАТЕЛЬНО перед build)

```bash
dotnet format tessera.slnx --severity hidden
```

Format drift — самый частый повод «я не могу закоммитить, CI красный».
Если ты не починил drift в этом шаге, build упадёт (VerifyFormatOnBuild
target валит на любом drift), и ты будешь re-итерировать. **Чини до
build, не после**.

Если drift слишком большой (50+ violations), вынеси в отдельный
«format cleanup» commit — не смешивай с feature-работой.

### 1. Mechanical: build + analyzer warnings

```bash
dotnet build tessera.slnx -c Debug
```

**Любой warning = failure.** В этом проекте `TreatWarningsAsErrors=true`
глобально (см. `Directory.Build.props`), так что build падает
автоматически. Та же команда прогоняет format-гейт (`VerifyFormatOnBuild`):
**format drift тоже валит build** (см. `process/build-verification.md`).

**Чини код, не подавляй warning.** Документированный escape hatch —
только с baseline-issue (см. `coding/analyzers.md` §"Что делать при новых
warnings"):

- `#pragma warning disable` — только с комментарием-обоснованием + restore
- `dotnet_diagnostic.X.severity = none` — **запрещено** (это отключение
  правила, а не enforcement)
- `<NoWarn>` в csproj — только с записью в baseline-issue

### 2. Convention: cross-check diff против загруженных rules

Ты уже имеешь в контексте эти rules (load order ниже на случай если
не помнишь что у тебя в system prompt):

- `coding/naming-and-types.md` — naming, primary ctor, sealed, record vs class, type references
- `coding/code-shape.md` — pattern matching, var, ns, braces
- `coding/constructors-and-fields.md` — primary ctor, fields
- `coding/async-and-tasks.md` — async/await, ConfigureAwait ban
- `coding/anti-patterns.md` — enum anti-patterns, tuple ban, validation
- `coding/api-design.md` — minimal API endpoints, OpenAPI attributes
- `coding/logging.md` — structured logging
- `coding/di-lifetimes.md` — service lifetimes
- `coding/di-options.md` — IOptions pattern
- `coding/analyzers.md` — analyzer packages, severity в editorconfig
- `coding/project-layers.md` — host/shared/modules structure
- `coding/project-naming-and-setup.md` — naming, decision tree
- `coding/project-deps-and-tests.md` — layer deps, testing structure
- `coding/naming-tessera-theme.md` — internal theme words
- `coding/module-structure-5-cap.md` — 5-project cap per folder
- `process/build-verification.md` — 0 warnings перед commit
- `process/commit-format.md` — `[.stbl](feat/...): subject`
- `process/agent-runtime-safety.md` — never run dev/watch/serve
- `process/project-slnx-registration.md` — slnx + ProjectReference

**Пройдись по diff'у и для каждого релевантного rule проверь
compliance.** «Я вроде нормально написал» — это не проверка. Реально
прочитай правило, найди в diff'е место, убедись что соответствует.

Частые точки внимания:
- Public методы без `<summary>` (см. `code-shape.md`)
- Braces без скобок (`code-shape.md` §9)
- Async метод без `Async` суффикса (`naming-and-types.md`)
- `_privateField` (`naming-and-types.md` — convention)
- Explicit ctor + `private readonly _field` когда хватает primary ctor (`constructors-and-fields.md`)
- Несколько public-классов в одном файле (`code-shape.md` — one-class-per-file)
- `var` vs explicit type (`code-shape.md`)
- `IReadOnlyCollection` vs `List` в public API (`code-shape.md`)
- **`ArgumentNullException.ThrowIfNull`** в nullable-enabled контексте — дубль
  статического контракта компилятора. Non-nullable параметр уже обеспечен
  на call-site; `ThrowIfNull` внутри = мусор.
- **`#region` директивы** запрещены — прячут структуру.
- **Запрещённые суффиксы** `*Dto`/`*Model`/`*Impl` в DTO именах (`naming-and-types.md`)
- **Generic параметры `ct`/`req`/`resp`** вместо `cancellationToken`/`request`/`response`
- **Cross-module imports** — модули не должны ссылаться друг на друга (`project-deps-and-tests.md`)
- **5-project cap нарушен** в `src/` или `tests/` (`module-structure-5-cap.md`)
- **Theme word в feature name** (`naming-tessera-theme.md`) — не использовать
  `Tessera.Modules.Tile` или `Tessera.Modules.Mosaic` (theme для schemas)
- **Commit message format** (`commit-format.md`)

### 3. Rule gaps: вызови `analyzer-coach` skill

Если ты нашёл в diff'е style issue, который:
- Не enforced ни одним rule (analyzer + .editorconfig + convention docs)
- Скорее всего повторится (не разовый случай)

→ **Запусти `analyzer-coach` skill.** Он предложит один из вариантов:

| Куда | Когда |
|---|---|
| `.editorconfig` (через `dotnet_diagnostic.RCS####.severity`) | Issue покрывается standard analyzer (Roslynator/CA/MA/VSTHRD) |
| `.agents/rules/coding/*.md` | Convention/pattern, нет standard rule |
| **Custom analyzer** в `src/host/Tessera.Build.Tools/Tessera.Analyzers/` | Project-specific, code-level check |
| "Not analyzable" verdict | Это правда convention, обсуди в code review |

Custom analyzer path конкретно:
- Проект: `src/host/Tessera.Build.Tools/Tessera.Analyzers/`
- Convention: один файл на правило, sealed class, public const `DiagnosticId` = `CMK####`
- Severity задаётся в `.editorconfig` (`dotnet_diagnostic.CMK0001.severity = error`)
- Перед добавлением проверь что issue не покрыт standard analyzer'ом

Примени proposal, прогони шаг 1 ещё раз чтобы убедиться что ничего не сломал.

### 4. Loop до чистого состояния

Если шаг 1 или 2 находит violations — fix и перепрогон обоих. **Max 3 итерации**
чтобы не уйти в infinite loop на genuine conflicts. Если после 3 итераций что-то
всё ещё не проходит — опиши проблему в completion report и попроси parent решить.

### 5. Только после этого commit

Commit только когда:
- Шаг 1 passes (0 warnings)
- Шаг 2 не нашёл violations
- Rule gaps из шага 3 либо resolved, либо явно описаны в report

**Pre-commit hook'а нет** — вся валидация в `dotnet build tessera.slnx`
(компиляция + анализаторы + format-гейт). Не обходи гейт через
`-p:DisableFormatOnBuild=true` ради «готово».

## Почему это mandatory

Цель soly'евской rule infrastructure — LLM пишет код соответствующий
project standards **автоматически**, не «стараясь». Этот gate это
enforce'ит. Skipping = мы в том же месте что и без rules: «агент
написал, CI поймал, переделываем».

## Связанные rules

- `coding/analyzers.md` §"Что делать при новых warnings" — escape hatches
- `process/build-verification.md` — full verify перед "готово"
- `process/commit-format.md` — `[.stbl](feat/...): subject`

## См. также

- `~/.pi/agent/skills/analyzer-coach/SKILL.md` — skill для шага 3 (rule gaps)
- `~/.pi/agent/skills/analyzer-coach/references/cookbook.md` — топ-30 жалоб → правила
