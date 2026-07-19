---
description: tessera-specific rules — project structure, theme names, ports. global C#/process rules loaded from ~/.pi/agent/rules/.
globs: ["**/*"]
always: true
---

# Tessera rules — index

**Two rule layers:**

1. **Global** (`~/.pi/agent/rules/`) — project-neutral C# rules, loaded for every session:
   - `coding/` — naming-and-types, code-shape, class-layout-and-tooling, constructors-and-fields, async-and-tasks, anti-patterns, logging, di-lifetimes, di-options, folder-organization, testing-stack-and-pyramid, testing-unit, analyzers, rules-format
   - `process/` — commit-format, build-verification, agent-runtime-safety, engineering-zone-access, project-slnx-registration, worker-audit

2. **Tessera-specific** (this folder) — only what's specific to the Tessera project:
   - `coding/api-design.md` — minimal API endpoints (NOT controllers — Plexor uses controllers)
   - `coding/module-structure-5-cap.md` — 5-project (csproj) cap per folder in Tessera repo
   - `coding/naming-tessera-theme.md` — Tessera theme words (tile, mosaic, mortar, weave, capstone, pattern, fragment, veneer, join)
   - `coding/project-deps-and-tests.md` — Tessera's layer structure + provider isolation rule
   - `coding/project-layers.md` — host/shared/modules/providers + tests
   - `coding/project-naming-and-setup.md` — Tessera naming convention (`Tessera.<Layer>...`)
   - `coding/project-ports.md` — Tessera port pool (1990–2120)

**Anything not in this folder is governed by global rules.** If a global
rule needs a Tessera-specific override, the override lives here.

**Common mistakes to avoid:**
- Don't duplicate global rules in this folder — they get loaded twice and waste context.
- Don't put Plexor-specific content here (e.g. entity models, EF Core, controllers, exchange subscribers).
- Don't put `Process/<X>` rules in `coding/` or vice versa — hierarchy is enforced by the rule format.
