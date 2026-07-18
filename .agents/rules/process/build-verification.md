---
description: когда и как прогонять build+verify на backend (.net) и frontend (react/ts), какие ошибки недопустимы перед коммитом
globs: ["**/*.cs", "**/*.csproj", "**/*.sln", "**/*.slnx", "**/*.ts", "**/*.tsx", "**/package.json", "**/tsconfig*.json", "**/vite.config.*"]
priority: high
interactive: false
always: true
---

# Build & Verification

Это правило описывает, **когда** и **как** запускать сборку backend (.NET) и
frontend (React/TypeScript), какие состояния считаются failure, и что делать
с pre-existing ошибками в незатронутых файлах.

## Анти-drift checklist — самое важное

> **Каждый format drift, который ты вносишь, чинится в том же коммите,
> где он появился.** Не «починю потом», не «это pre-existing», не
> «у меня времени нет». Format drift — токсичная задолженность: 10
> warnings сегодня = 373 warnings через месяц = часовая чистка которую
> никто не хочет делать. Поэтому:

**Перед коммитом (любой нетривиальной правки):**

```
1. git diff --stat      ← посмотри что менял
2. dotnet build tessera.slnx -c Debug
   ├── если ошибки компиляции / analyzers (warning|error) → почини
   └── если format drift (RCS/IDE/CA/MA в obj/format-verify.log):
       3. dotnet format tessera.slnx --severity hidden
          ← ЭТО часть работы над коммитом. Не пропускай.
       4. повторить build → убедиться что 0 drift
5. dotnet test tests/unit/<touched-project> --no-build
6. если затронут FE → cd web && bun run gate
7. git add + git commit
```

**Не «git commit → CI поймает»**. К моменту CI-fix-а в истории уже лежит
коммит с drift-ом, и при rebase / cherry-pick другие агенты будут получать
его в качестве pre-existing.

## Единственная команда верификации backend

> **`dotnet build tessera.slnx -c Debug`** — это ВЕСЬ гейт. Одна команда,
> одинаковая на всех платформах, та же, что в CI.

`dotnet build` делает три вещи за один прогон:

1. **Компиляция.**
2. **Анализаторы** (CA / RCS / MA / VSTHRD / IDE) — через
   `EnforceCodeStyleInBuild=true` + `TreatWarningsAsErrors=true` в
   `Directory.Build.props`. Любой `warning` = ошибка сборки.
3. **Format-check** — target `VerifyFormatOnBuild` (в
   `src/host/Tessera.Build.Tools/Tessera.Build.Tools.targets`) запускает
   `dotnet format tessera.slnx --verify-no-changes --severity hidden` один
   раз на solution-сборку. **Format drift = build FAILS** (жёсткий гейт).

Зелёный `dotnet build` ⇒ код соответствует стандарту. Точка. Это убирает
ситуацию «локально прошло, CI красный»: CI гоняет ту же команду.

**Важно:** только **solution**-сборка (`tessera.slnx`). При сборке одного
проекта (`dotnet build src/foo/foo.csproj`) `Tessera.Build.Tools` не попадает
в граф → format-check не запускается.

Эскейпы (только для inner-loop, не для «готово»):

```bash
dotnet build tessera.slnx -c Debug -p:DisableFormatOnBuild=true        # пропустить format-гейт
dotnet build tessera.slnx -c Debug -p:FormatOnBuildTreatAsWarning=true # drift как warning, не error
```

**`--severity hidden`** — критический флаг при ручном fix:

```bash
dotnet format tessera.slnx --severity hidden
```

Без флага `dotnet format` идёт на дефолтном `--severity warn` и **не чинит**
правила ниже `warning` (silent/suggestion). Без флага `dotnet format` отработает
«вхолостую», а гейт продолжит падать на тех же violations.

**Только whitespace (быстрый фикс):** если `dotnet format --severity hidden`
застревает, используй сабкоманду:

```bash
dotnet format tessera.slnx whitespace
```

## Agent contract — Definition of Done

Перед тем как сказать «готово» — прогнать набор. Без сокращений:

```bash
# 1. Fix drift (если есть)
dotnet format tessera.slnx --severity hidden

# 2. Build = компиляция + анализаторы + format-гейт.
dotnet build tessera.slnx -c Debug

# 3. Тесты (затронутые проекты).
dotnet test tests/unit/<ProjectName>.Unit --no-build --nologo

# 4. Frontend — если затронут.
cd web && bun run gate
```

**Правило:** exit ≠ 0 от **любой** команды = задача **не готова**. Чинить и повторить.

## Когда применять

Применяется ко **всем** нетривиальным правкам в `src/`:

| Затронутая сторона | Паттерн файлов | Что проверять |
|--------------------|----------------|---------------|
| Только BE | `**/*.cs`, `**/*.csproj`, `**/*.slnx` | `dotnet build tessera.slnx -c Debug` |
| Только FE | `**/*.ts`, `**/*.tsx`, `**/package.json`, `**/tsconfig*.json`, `**/vite.config.*` | FE build |
| Обе стороны | mix of above | **Оба** билда (см. Dual-Build Rule) |
| OpenAPI / endpoints | `**/*.cs` с `MapGet` / `MapPost` / `**/openapi*.json` | BE + регенерация FE API client |

**Тривиальные правки** (опечатки в markdown, переименование файла) — полный
прогон не требуется, но если Format drift остаётся — прогоните хотя бы
`dotnet format tessera.slnx --severity hidden`.

## Что ловит build, чего не ловит «голый компилятор»

`VerifyFormatOnBuild` (`dotnet format --severity hidden`, самый permissive
уровень) внутри build ловит то, что анализаторы при компиляции в принципе
не покрывают:

- **Whitespace** — BOM, trailing-whitespace, EOL, пустые строки между членами.
- Style-rules с severity ниже `warning` (`silent`/`suggestion`), у которых
  есть auto-fix.

Поэтому отдельный `dotnet format --verify` запускать не нужно — он уже внутри
`dotnet build`.

**Глобально suppressed** (см. `.editorconfig` + `Directory.Build.props`):
- `RMG012` (Mapperly), `CS1573` (param comment в partial-методах),
- `NU1510` / `NU1605` — package-not-pruned warnings (harmless в modular repos).

Все остальные diagnostics — **починить до коммита**. Не отключать анализаторы,
не править `.editorconfig` ради одного warning (см. `coding/analyzers.md`).

## Frontend

FE-монорепо живёт в `web/` (bun + turbo + React 19 + Vite). PM — **bun**,
не npm/pnpm.

```bash
cd web && bun run gate          # каноничный FE-гейт: typecheck + lint + test
cd web && bun run build         # если менялся build-вывод (vite)
```

**Требования к выходу:**
- Exit code = `0`, **0 TypeScript errors**, eslint чисто.
- Vite warnings (chunk size > 500kb, dynamic import hints) — допустимы.

## Dual-Build Rule

Если задача затрагивает **обе стороны** (новый endpoint + страница, смена
контракта API):

```bash
# 1. Backend (компиляция + анализаторы + format-гейт).
dotnet build tessera.slnx -c Debug

# 2. Frontend.
cd web && bun run gate

# 3. Опционально: регенерация API client, если менялись endpoints.
cd web && bun run codegen
cd web && bun run build
```

**Обе** должны дать exit 0. Недопустимо «FE компилируется, BE потом починю».

## Pre-existing drift — отдельная задача

Если при первом запуске `dotnet build tessera.slnx -c Debug` в начале сессии
format-gate падает на drift, который ты **не вносил** в этой сессии:

| Ситуация | Что делать |
|----------|------------|
| Drift в **затронутом** файле | ✅ Починить в рамках текущей задачи (если fix тривиальный) ИЛИ отдельный commit «fix format drift» |
| Drift в **незатронутом** файле, < 50 violations | ✅ Отдельный cleanup-коммит в начале сессии — НЕ смешивать с feature-работой |
| Drift в **незатронутом** файле, > 50 violations | ⚠️ Можно разбить на несколько коммитов, **отметить в commit message** |

❌ **Запрещено:**
- `// @ts-ignore` / `// eslint-disable-next-line` без обоснования.
- `dotnet_diagnostic.* = none` в `.editorconfig` для подавления warning.
- `#pragma warning disable` без `restore` и без комментария «почему».
- `-p:DisableFormatOnBuild=true` в финальном build (только inner-loop escape).

## Перед коммитом — чеклист (TL;DR)

```
1. git diff --stat  → определил затронутую сторону (BE / FE / обе)
2. dotnet format tessera.slnx --severity hidden
   ← ОБЯЗАТЕЛЬНО. Даже если вроде всё чисто. 5 сек.
3. dotnet build tessera.slnx -c Debug
   ← exit 0? Если нет → fix → повторить.
4. dotnet test tests/unit/<ProjectName>.Unit --no-build
   ← pass? Если нет → fix → повторить.
5. (если FE) cd web && bun run gate
6. git add + git commit
```

**Запрет:** коммитить до того как все 6 шагов дали exit 0. Без исключений
кроме горячего hotfix (см. выше).

## Good / Bad

```bash
# ✅ Correct — затронут только BE, format gate зелёный
$ dotnet format tessera.slnx --severity hidden
$ dotnet build tessera.slnx -c Debug
 ... Build succeeded. 0 Warning(s) 0 Error(s)
$ git commit -m "[.stbl](feat/traces): add list traces endpoint"
```

```bash
# ❌ Wrong — затронут только BE, format drift не починил
$ dotnet build tessera.slnx -c Debug
 ... error : [VerifyFormatOnBuild] Format drift detected: 47 violation(s)
$ git commit --no-verify -m "..."   # BUG: drift ленднет в историю
```

```bash
# ❌ Wrong — свой набор команд вместо стандарта
$ dotnet format ... --severity warn  # не тот severity, не та проверка
$ dotnet build src/foo/foo.csproj    # per-project → format-гейт не сработал
# Стандарт ОДИН: dotnet build tessera.slnx -c Debug
```

## Связанные правила и файлы

- `.editorconfig` — severity правил (прод / тесты)
- `Directory.Build.props` — глобальные suppressed warnings, `TreatWarningsAsErrors`
- `src/host/Tessera.Build.Tools/Tessera.Build.Tools.targets` — `VerifyFormatOnBuild` гейт
- `process/worker-audit.md` — self-audit gate (что проверяет LLM вручную)
