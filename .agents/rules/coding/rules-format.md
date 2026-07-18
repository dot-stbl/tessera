---
description: формат и иерархия правил в проекте — где что лежит, как грузится, как добавлять новое
always: true
---

# Формат правил

В проекте **две** системы правил. Этот файл — карта между ними: что куда класть, как грузится, как не дублировать.

Каноническое дерево правил — `.agents/rules/`. Универсальные правила агентского harness (soly / claude-code) — в `~/.pi/agent/...` или `~/.claude/rules/`, и **тонкие указатели** на канон, без дублирования содержания.

## 1. Где что лежит

### `.agents/rules/<category>/*.md` — канонические правила

Файлы с soly frontmatter. Грузятся:
- по `globs` (применяется к указанным файлам),
- по `always: true` (всегда в контексте).

Используются для:
- code-style (C# правила, conventions),
- process (workflow, build, commits),
- тестирования (когда писать, какой стэк),
- архитектуры (слои, зависимости),
- security, performance — по необходимости.

### Глобальные правила harness'а — тонкие указатели

Глобальные правила (`~/.pi/agent/...`, `~/.claude/rules/`) **не дублируют** содержание канонических. Если правило развёрнуто в `.agents/rules/coding/X.md` — в глобальном только ссылка, не копия.

> **Hard rule: дублирование содержания между `.agents/` и глобальными запрещено.**

## 2. Frontmatter — REQUIRED формат

```yaml
---
description: one-line lowercase — что это правило ограничивает
globs: ["**/*.py", "**/requirements.txt"]   # опционально
priority: high | medium | low                # опционально
interactive: false                           # true = только для интерактивного LLM
always: false                                # true = обходить glob-проверку
---
```

**`description` — всегда lowercase, разделитель `-` или `.`. Без исключений.**

Это касается всей meta-информации в проекте: frontmatter-поля, имена тегов
OTel (`command.name`), имён метрик (`query.duration`), datasource
name в Grafana. Capitalization оправдана только convention'ом языка/платформы
(C# class names, JSON `schemaVersion`).

## 3. Иерархия (от высшего приоритета к низшему)

1. `.agents/rules.local/` (per-project, gitignored, личные overrides)
2. `.agents/rules/` (per-project, коммитится в репу)
3. Глобальные правила harness'а (`~/.pi/agent/...`, `~/.claude/rules/`)
4. Built-in soly rules (highest priority, всегда грузятся)

При коллизии — выигрывает правило с **меньшим** номером.

## 4. Текущая структура `.agents/rules/`

```
.agents/rules/
├── coding/
│   ├── rules-format.md                ← этот файл
│   ├── naming-and-types.md            # C# naming, sealed, record vs class
│   ├── constructors-and-fields.md     # primary ctor, fields
│   ├── code-shape.md                  # pattern matching, var, ns, braces
│   ├── class-layout-and-tooling.md    # XML docs, model placement
│   ├── async-and-tasks.md             # async/await, ConfigureAwait ban
│   ├── anti-patterns.md               # enum, tuple ban, validation
│   ├── api-design.md                  # minimal API endpoints, OpenAPI
│   ├── logging.md                     # structured logging
│   ├── di-options.md                  # IOptions pattern
│   ├── di-lifetimes.md                # service lifetimes
│   ├── project-layers.md              # host/shared/modules overview
│   ├── project-naming-and-setup.md    # project naming, decision tree
│   ├── project-deps-and-tests.md      # layer deps, testing structure
│   ├── analyzers.md                   # analyzer packages wiring
│   ├── naming-tessera-theme.md        # tessera theme words (two-name system)
│   └── module-structure-5-cap.md      # max 5 projects per folder
├── process/
│   ├── build-verification.md          # build gate (dotnet build tessera.slnx)
│   ├── commit-format.md               # [.stbl](feat/...): subject
│   ├── engineering-zone-access.md     # tier-A vs tier-B access
│   ├── worker-audit.md                # self-audit gate
│   ├── agent-runtime-safety.md        # never run dev/watch/serve
│   └── project-slnx-registration.md   # new .csproj → slnx + ProjectReference
├── architecture/                      # (пусто в MVP)
└── observability/                     # (пусто в MVP)
```

## 5. Как добавить новое правило

1. Создать `<topic>.md` рядом с существующими (lowercase, kebab-case).
2. **Обязательно** frontmatter с `description:`.
3. Body: ToC + `##` / `###` + Good / Bad примеры + Enforcement/Test line.
4. Если правило применяется всегда — добавить `always: true`.
5. Если правило узкое (только для конкретных файлов) — добавить `globs: [...]`.

## 6. Reload

После правки `.agents/rules/` — перезапуск сессии подхватит изменения
автоматически. Для soly-aware агентов: `/rules reload`.

## Связанные правила

- `process/build-verification.md` — build gate (что ловится автоматически)
- `process/worker-audit.md` — self-audit gate (что проверяет LLM вручную)
- `process/engineering-zone-access.md` — какие файлы правил LLM может редактировать
