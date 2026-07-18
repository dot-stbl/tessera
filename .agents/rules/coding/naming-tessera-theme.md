---
description: tessera theme words для internal schema/module naming — single-token, invented, mythological-tile theme
globs: ["**/*.cs", "**/*.csproj"]
always: true
---

# Tessera theme words

Tessera следует Plexor's **two-name system**:
- **Architecture theme names** — short, single-token, themed (для schemas, modules, internal namespaces).
- **C# concept names** — стандартная vocabulary (для entities, DTOs, services).

Theme root — `tessera` itself. Internal schemas/modules используют слова в
том же invented-word, single-token стиле, **тематически связанные** с mosaic /
tile work (значение слова "tessera" в латыни и итальянском — "tile",
"mosaic piece").

## Approved theme words (MVP scope)

| Word | Meaning | Used for |
|------|---------|----------|
| `tessera` | root — Latin "tile" | project name, theme root |
| `tile` | individual data point | smallest observation unit (single span, single log) |
| `mosaic` | composed picture | full trace/log query result (composed view) |
| `mortar` | what's between tiles | correlation glue (e.g. trace_id linking in VL queries) |
| `weave` | to combine | query planning / multi-source join logic |
| `capstone` | the peak tile | top-level dashboard / service overview |
| `pattern` | recurring shape | service map / dependency graph |
| `fragment` | broken piece | partial trace / truncated data |
| `veneer` | surface layer | UI presentation layer (stretch) |
| `join` | connection point | correlation key (trace_id) |

## Usage rules

1. **Schema/module names** — theme words only. Не использовать DDD-style names
   (`User`, `Tenant`, `Organization`) для schemas/modules.
2. **Concept names** — standard vocabulary. `Trace`, `Span`, `LogEntry`,
   `Service`, `TimeRange` — НЕ `Mosaic` или `Tile` (это schemas).
3. **Don't cross the streams** — никогда не называть C# класс `Mosaic` или
   `Weave`. Это schema names. Используй concept name.
4. **Project names** — `Tessera.<Capability>` или `Tessera.Modules.<Feature>`.
   Project name использует **capability** (Http, Telemetry) или **feature**
   (Traces, Logs), не theme word.
5. **New theme words** — добавить в эту таблицу с rationale в
   `.agents/STATE.md` Decisions.

## Anti-patterns

```
❌ Tessera.Modules.User           # not a theme word
❌ Tessera.Modules.Auth           # not a theme word
❌ Tessera.Modules.Telemetry      # conflicts with Tessera.Shared.Telemetry
❌ Tessera.Modules.Tile           # theme word в module name (theme для schemas, не features)
✅ Tessera.Modules.Traces         # concept name (Trace)
✅ Tessera.Modules.Discovery      # concept name (Service discovery)
✅ Tessera.Modules.Health         # concept name (health checks)
✅ Tessera.Shared.Kernel          # capability name
✅ Tessera.Shared.Http            # capability name
```

## Когда scope растёт

Stretch phases (Service Map, Alerts, SLO) вводят новые theme words.
Добавить сюда сначала, потом использовать. Примеры для будущих фаз:

| Word | Meaning | Used for |
|------|---------|----------|
| `kiln` | furnace for hardening tiles | SLO burn rate calculations |
| `relief` | raised surface | alert notifications |
| `inlay` | decorative insertion | custom dashboards |
| `tessellate` | to form into mosaic | data aggregation pipeline |
| `grout` | filler between tiles | log/metric enrichment pipeline |

## Mapping примеры

```
Schema:        tessera.<theme_word>
Module:        Tessera.Modules.<Feature>       ← feature = concept, не theme
Concept class: Tessera.Modules.Traces.Trace  ← concept name в feature module
DTO:           Tessera.Modules.Traces.TraceSummary
Endpoint:      Tessera.Modules.Traces.Endpoints.TracesEndpoint
Handler:       Tessera.Modules.Traces.Handlers.GetTraceHandler
Refit client:  Tessera.Modules.Traces.IVictoriaTracesClient
```

## Связанные правила

- `.agents/rules/coding/project-naming-and-setup.md` — project naming rules
- `.agents/rules/coding/project-layers.md` — где theme words применяются
- Plexor `AGENTS.md` — origin of the two-name system (read для контекста)
