---
description: tessera-specific rules — project structure, theme names, ports. global C#/process rules loaded from ~/.agents/rules/.
globs: ["**/*"]
always: true
---

# Tessera rules — index

**Two rule layers:**

1. **Global** (`~/.agents/rules/`) — project-neutral C#/TS + process rules, loaded for every session:
   - `secrets.md` — secret handling
   - `csharp/` — naming-and-types, code-shape, class-layout-and-tooling, constructors-and-fields, async-and-tasks, anti-patterns, logging, di-lifetimes, di-options, di-installer, folder-organization, nullability, exceptions, mapper, mapping, error-mapping, http-resilience-refit, json-and-ndjson, time-and-wire-format, configuration-toml-env, problem-details, api-route-constants, api-design, architecture, technology-stack, ef-core, ef-migrations, ef-owned-types, repository-spec, cross-platform, filesystem-paths, multi-provider-auth, testing-stack-and-pyramid, testing-unit, testing-integration, analyzers, rules-format
   - `typescript/` — react-and-components, tanstack-query-and-router, styling-and-design-system, workspace-and-i18n (**mandatory for `web/` work**)
   - `observability/` — diagnostics (OTel traces/metrics conventions)
   - `process/` — commit-format, build-verification, agent-runtime-safety, engineering-zone-access, project-slnx-registration, worker-audit, feature-workflow

2. **Tessera-specific** (this folder) — only what's specific to the Tessera project:
   - `coding/api-design.md` — controllers (matches plexor), v1 prefix, ProblemDetails, no Result<T> at HTTP boundary
   - `coding/module-structure-5-cap.md` — 5-project (csproj) cap per folder in Tessera repo
   - `coding/naming-tessera-theme.md` — Tessera theme words (tile, mosaic, mortar, weave, capstone, pattern, fragment, veneer, join)
   - `coding/project-deps-and-tests.md` — Tessera's layer structure + provider isolation rule
   - `coding/project-layers.md` — host/shared/modules/providers + tests
   - `coding/project-naming-and-setup.md` — Tessera naming convention (`Tessera.<Layer>...`)
   - `coding/project-ports.md` — Tessera port pool (1990–2120)

**Anything not in this folder is governed by global rules.** If a global
rule needs a Tessera-specific override, the override lives here. The
provider/proxy-flavoured global rules (error-mapping, http-resilience-refit,
json-and-ndjson, time-and-wire-format, mapper) use Victoria/Jaeger/LogsQL as
their worked examples.

**Decisions** (`.agents/docs/decisions/`) — Architecture Decision Records
lock architectural choices once made. Numbered sequentially (`NNNN-<slug>.md`).
Each ADR lists status, context, decision, alternatives considered, rationale,
and consequences. Reference ADRs from rules / code / HANDOFF when applying
the locked choice. New ADR = add file + entry to `.agents/STATE.md` §Decisions.

**Common mistakes to avoid:**
- Don't duplicate global rules in this folder — they get loaded twice and waste context.
- Don't put Plexor-specific content here (e.g. entity models, EF Core, controllers, exchange subscribers).
- Don't put `Process/<X>` rules in `coding/` or vice versa — hierarchy is enforced by the rule format.
