# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What Tessera is

Self-hosted **APM UI for the Victoria stack** (VictoriaTraces / VictoriaLogs / VictoriaMetrics) — a Kibana-APM-like web app: trace explorer with waterfall + log correlation, service inventory, health. The backend is a **thin proxy**: no persistence, no business logic, no ingest. All state lives in the browser (TanStack Query + zustand). Data is fetched from Victoria over HTTP via Refit clients.

Positioned as a **Grafana-analogue, multi-provider** APM — MVP-01 ships Victoria as the first provider, but modules are provider-agnostic so Tempo/Jaeger/Loki plug in later without module changes.

### Current state (read before assuming code exists)

- **Milestone MVP-01, backend-only, early scaffolding.** Most modules/providers are project shells; `Tessera.Host/Program.cs` is a stub returning a JSON banner. Handlers, endpoints, and Refit clients described in the docs are the *target design*, not all present yet.
- **Frontend integration is deferred to MVP-02.** The `web/` monorepo exists (adapted from Plexor) but feature code, routes, and the generated API client are not built yet. Don't wire FE↔BE during MVP-01.
- Authoritative state + decision log: `.agents/STATE.md`. Strategic context: `.agents/HANDOFF.md`. Current plan: `.agents/plans/tessera-mvp/PLAN.md`.

## Commands

**Stack:** .NET 10 (SDK pinned in `global.json`), React 19 + Vite + bun. Solution file is `tessera.slnx` (XML slnx format, not `.sln`).

### Backend (.NET)

```bash
# Canonical build gate — compile + analyzers (warnings = errors). Same command CI runs.
# ALWAYS build the SOLUTION, not a single csproj (per-project builds skip the format/arch gates).
dotnet build tessera.slnx -c Debug

# Format — run before every commit, even when it looks clean. --severity hidden is REQUIRED
# (default severity won't fix silent/suggestion rules like IDE0320/RCS1161).
dotnet format tessera.slnx --severity hidden

# Tests — whole solution, or a single project, or a single test
dotnet test tessera.slnx
dotnet test tests/unit/modules/Tessera.Modules.Traces.Unit --no-build
dotnet test tests/unit/core/Tessera.ArchitectureTests --filter "FullyQualifiedName~FolderLimits"

# Run the host (listens on :1990 by default)
dotnet run --project src/host/Tessera.Host
```

### Frontend (`web/`)

Package manager is **bun** (never npm/pnpm — lockfile is `bun.lock`). Monorepo = bun workspaces; the app is `@tessera/console`.

```bash
cd web
bun install
bun run dev          # vite dev server on :1991 (strictPort — fails if taken)
bun run build        # production build → apps/console/dist
bun run test         # vitest
bun run lint         # eslint --max-warnings 0
bun run typecheck    # tsc --noEmit
bun run playbook:dev # Ladle component catalog (design-system playbook)
```

There is **no combined `gate` script** in `web/package.json` — the FE verification gate is `typecheck` + `lint` + `test` run individually, all three must exit 0. (Add a `gate` alias if you want a one-liner.)

## Architecture

### Layers (`src/`) — references point only *downward*

```
host/       Tessera.Host (minimal-API composition root) + Tessera.Build.Tools (MSBuild gates)
shared/     Tessera.Shared.{Kernel,Http,Telemetry,OpenApi,Validation}  — leaf, nuget-only
modules/    Tessera.Modules.{Traces,Logs,Discovery,Health}             — vertical-slice features
providers/  Tessera.Providers.Victoria                                 — concrete data-source backends
tests/      unit/{modules,core,providers} + integration/
```

- `host/` → may reference everything (composition root wires it all in `Program.cs`).
- `modules/` → reference **`shared/` only**. Never `providers/`, never `host/`, **never each other**.
- `providers/` → reference `shared/` only. Never `modules/`, never each other.
- `shared/` → references nothing but NuGet.

### Provider abstraction is the central design (Grafana datasource model)

Modules **do not own Refit clients**. They consume **interfaces** from `Tessera.Shared.Kernel`: `ITraceProvider`, `ILogProvider`, `IDiscoveryProvider`, `IHealthProvider` (and `IMetricsProvider`, stretch). Concrete implementations (Refit → Victoria's Jaeger/LogsQL/Prometheus endpoints, plus DTO↔domain mapping) live entirely in `src/providers/Tessera.Providers.Victoria/` and are wired in DI **only** in `Program.cs` via `AddVictoriaProvider(...)`.

Consequences you must preserve:
- **Modules are backend-agnostic** — unit tests mock the provider interfaces (NSubstitute), no Victoria container needed.
- Adding a second backend = a new `Tessera.Providers.<Name>` implementing the same interfaces, zero module changes.
- **Enforced by `Tessera.ArchitectureTests`** (NetArchTest): `NoModuleReferencesProviders`, no cross-module references, and the folder cap. These are `dotnet test`, not analyzers.

### Two hard structural rules

1. **5-project cap per folder** in `src/` and `tests/`. Over the cap → **nest** (e.g. `modules/core/` + `modules/extended/`), never widen the cap. `shared/` is currently *at* the cap (5).
2. **Modules never reference each other.** Cross-module needs (e.g. Traces correlating Logs) go through a `shared/` interface or are orchestrated in the `host/` composition root.

### Wire & domain conventions

- **Time on the wire is UTC unix milliseconds everywhere** (matches VT/VL/VM native format — avoids tz bugs).
- **Minimal API, not controllers** (Plexor uses controllers; Tessera does not). Each module owns a `Map<Feature>Endpoints` group + an `Add<Feature>Module(IServiceCollection)` extension.
- **Span tree is reconstructed on the backend** from VT's flat spans (parent/child via `references[CHILD_OF]`).
- **Single tenant, hardcoded `0`** in MVP. HTTP resilience via `Microsoft.Extensions.Http.Resilience` (Polly). Victoria bearer token stays server-side (SPA never sees it).

## Conventions Claude must follow

### Commit messages — strict format, enforced by rule

```
[.stbl](feat/<area>): <subject>
```

- `[.stbl]` literal tag + a **required** `feat/…` feature path (lowercase kebab, `/`-nested) + imperative subject (lowercase, no period, ≤72 chars).
- Areas: `feat/<module>`, `feat/<provider>`, `feat/host`, `feat/build-tools`, `feat/fe` (web/), `feat/tests`, `feat/docs`, `feat/meta` (build/CI/deps/**rules**). Code changes never use `feat/meta`.
- Body explains *why*, wrapped ~72 chars. Full spec + examples: [`~/.agents/rules/process/commit-format.md`](file:///C:/Users/bradw/.agents/rules/process/commit-format.md).

### Naming — Plexor two-name system

- **Project names** use capability/feature words: `Tessera.Shared.Http`, `Tessera.Modules.Traces`. **C# concept classes** use standard vocabulary: `Trace`, `Span`, `LogEntry`, `TimeRange`.
- **Theme words** (`tile`, `mosaic`, `mortar`, `weave`, `capstone`, `fragment`, `join`, …) are for *schemas/internal namespaces only* — never a C# class named `Mosaic`. New theme words go in the table in `naming-tessera-theme.md` first. See [`.agents/rules/coding/naming-tessera-theme.md`](.agents/rules/coding/naming-tessera-theme.md).

### Ports

Tessera reserves **1990–2120**. Backend `1990`, Vite `1991`, `/metrics` `1992`. Don't use 3000/5173/8080/9090. Tests use port `0` (OS-assigned).

### Before saying "done" (non-trivial change)

1. `dotnet format tessera.slnx --severity hidden`
2. `dotnet build tessera.slnx -c Debug` → exit 0, 0 warnings
3. `dotnet test` on touched project(s)
4. If `web/` touched: `bun run typecheck && bun run lint && bun run test`

Any non-zero exit = not done. Fix format drift **in the same commit that introduced it** — never defer it as "pre-existing". Never suppress with `#pragma`, `// eslint-disable`, `dotnet_diagnostic.* = none`, or `-p:DisableFormatOnBuild=true` in a final build.

## Rules & docs

This repo is governed by a two-layer rule set. **Read the relevant rule before working in its area** — they are authoritative over this summary.

### Global C#/process rules — `~/.agents/rules/` (`C:\Users\bradw\.agents\rules\`)

Project-neutral rules loaded for every `.stbl` session.

- **Coding** — [`analyzers`](file:///C:/Users/bradw/.agents/rules/coding/analyzers.md), [`anti-patterns`](file:///C:/Users/bradw/.agents/rules/coding/anti-patterns.md), [`async-and-tasks`](file:///C:/Users/bradw/.agents/rules/coding/async-and-tasks.md), [`class-layout-and-tooling`](file:///C:/Users/bradw/.agents/rules/coding/class-layout-and-tooling.md), [`code-shape`](file:///C:/Users/bradw/.agents/rules/coding/code-shape.md), [`constructors-and-fields`](file:///C:/Users/bradw/.agents/rules/coding/constructors-and-fields.md), [`di-lifetimes`](file:///C:/Users/bradw/.agents/rules/coding/di-lifetimes.md), [`di-options`](file:///C:/Users/bradw/.agents/rules/coding/di-options.md), [`folder-organization`](file:///C:/Users/bradw/.agents/rules/coding/folder-organization.md), [`logging`](file:///C:/Users/bradw/.agents/rules/coding/logging.md), [`naming-and-types`](file:///C:/Users/bradw/.agents/rules/coding/naming-and-types.md), [`testing-stack-and-pyramid`](file:///C:/Users/bradw/.agents/rules/coding/testing-stack-and-pyramid.md), [`testing-unit`](file:///C:/Users/bradw/.agents/rules/coding/testing-unit.md)
- **Coding (DI / errors / IO — added this session)** — [`di-installer`](file:///C:/Users/bradw/.agents/rules/coding/di-installer.md), [`nullability`](file:///C:/Users/bradw/.agents/rules/coding/nullability.md), [`exceptions`](file:///C:/Users/bradw/.agents/rules/coding/exceptions.md), [`mapper`](file:///C:/Users/bradw/.agents/rules/coding/mapper.md), [`error-mapping`](file:///C:/Users/bradw/.agents/rules/coding/error-mapping.md), [`http-resilience-refit`](file:///C:/Users/bradw/.agents/rules/coding/http-resilience-refit.md), [`json-and-ndjson`](file:///C:/Users/bradw/.agents/rules/coding/json-and-ndjson.md), [`time-and-wire-format`](file:///C:/Users/bradw/.agents/rules/coding/time-and-wire-format.md), [`configuration-toml-env`](file:///C:/Users/bradw/.agents/rules/coding/configuration-toml-env.md), [`problem-details`](file:///C:/Users/bradw/.agents/rules/coding/problem-details.md), [`api-route-constants`](file:///C:/Users/bradw/.agents/rules/coding/api-route-constants.md)
- **Observability** — [`diagnostics`](file:///C:/Users/bradw/.agents/rules/observability/diagnostics.md) (OTel traces/metrics conventions)
- **Process** — [`build-verification`](file:///C:/Users/bradw/.agents/rules/process/build-verification.md), [`commit-format`](file:///C:/Users/bradw/.agents/rules/process/commit-format.md), [`agent-runtime-safety`](file:///C:/Users/bradw/.agents/rules/process/agent-runtime-safety.md), [`engineering-zone-access`](file:///C:/Users/bradw/.agents/rules/process/engineering-zone-access.md), [`project-slnx-registration`](file:///C:/Users/bradw/.agents/rules/process/project-slnx-registration.md), [`worker-audit`](file:///C:/Users/bradw/.agents/rules/process/worker-audit.md)
- **Secrets** — [`~/.agents/rules/secrets.md`](file:///C:/Users/bradw/.agents/rules/secrets.md)

### Tessera-specific rules — [`.agents/rules/`](.agents/rules/_index.md) (in-repo, override globals)

- [`coding/project-layers.md`](.agents/rules/coding/project-layers.md) — host/shared/modules/providers layers
- [`coding/project-deps-and-tests.md`](.agents/rules/coding/project-deps-and-tests.md) — allowed references + provider isolation + test structure
- [`coding/project-naming-and-setup.md`](.agents/rules/coding/project-naming-and-setup.md) — `Tessera.<Layer>…` naming + new-project setup
- [`coding/module-structure-5-cap.md`](.agents/rules/coding/module-structure-5-cap.md) — 5-project folder cap
- [`coding/naming-tessera-theme.md`](.agents/rules/coding/naming-tessera-theme.md) — theme words
- [`coding/api-design.md`](.agents/rules/coding/api-design.md) — minimal API (not controllers)
- [`coding/project-ports.md`](.agents/rules/coding/project-ports.md) — 1990–2120 port pool

### Design docs — [`.agents/docs/`](.agents/docs/architecture.md)

[`architecture.md`](.agents/docs/architecture.md) (flows, decision table), [`modules.md`](.agents/docs/modules.md) (per-module HTTP contracts), [`victoria-stack.md`](.agents/docs/victoria-stack.md) (VT/VL/VM API reference), [`scope.md`](.agents/docs/scope.md) (in/out of MVP), [`ui/`](.agents/docs/ui/design-system.md), [`operations/`](.agents/docs/operations/install.md), [`security/auth-model.md`](.agents/docs/security/auth-model.md).

## Build configuration

- `Directory.Build.props` — `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisMode=Recommended`, Roslynator + VS threading analyzers, MinVer versioning from git tags (`v*`). Globally suppressed warnings live in its `<NoWarn>`.
- `Directory.Packages.props` — **CPM is disabled** (analyzer wildcard versions conflict with NU1008); versions are still centralized here, referenced without versions in csprojs.
- Format/anti-pattern build-time gates are intended to live in `src/host/Tessera.Build.Tools/Tessera.Build.Tools.targets` (currently a placeholder — the enforced gate today is analyzers-as-errors + the architecture tests).
- Brand assets are a git submodule at `assets/stbl/` (`git submodule update --remote assets/stbl`).
