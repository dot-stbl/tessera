# AGENTS.md

> **Tessera** — APM UI for the Victoria stack. Self-hosted multi-provider
> tracing/logs/metrics viewer. MVP-01 backend-only; frontend deferred to
> MVP-02. Authoritative context: `.agents/STATE.md` (progress),
> `.agents/HANDOFF.md` (strategy), `.agents/docs/decisions/0001-*.md`
> (locked architectural choices).

## Rules — load order (FORCE)

This repository inherits **all** rules from the user-global store
`~/.agents/rules/`. They are **force-loaded at session start** via the
rules engine and override silent defaults. The Tessera-specific
overrides + file index live in `.agents/rules/_index.md`.

### Mandatory pre-coding reading list

**Global rules** (from `~/.agents/rules/`):

1. `csharp/naming-and-types.md`
2. `csharp/code-shape.md`
3. `csharp/class-layout-and-tooling.md`
4. `csharp/constructors-and-fields.md`
5. `csharp/async-and-tasks.md`
6. `csharp/anti-patterns.md`
7. `csharp/folder-organization.md`
8. `csharp/nullability.md`
9. `csharp/exceptions.md`
10. `csharp/error-mapping.md`
11. `csharp/problem-details.md`
12. `csharp/ef-core.md` (relevant once Phase 5 lands)
13. `csharp/repository-spec.md` (relevant once Phase 5 lands)
14. `secrets.md`
15. `process/commit-format.md`
16. `process/build-verification.md`
17. `process/worker-audit.md`
18. `process/agent-runtime-safety.md`
19. `process/engineering-zone-access.md`

**Frontend rules** (from `~/.agents/rules/typescript/`) — **mandatory for any `web/` work** (MVP-02+):

- `react-and-components.md` — React 19 shape, named exports, hooks, `cn()`
- `tanstack-query-and-router.md` — QueryClientProvider required, queryKeys, code-based routes
- `styling-and-design-system.md` — DS primitives first, OKLCH tokens in `index.css`, theming
- `workspace-and-i18n.md` — bun only, typecheck/lint/test gate, i18next copy

**Tessera-specific rules** (from `.agents/rules/coding/`):

20. `api-design.md` — controllers pattern, `[ApiController] + ControllerBase`
21. `project-layers.md` — host / shared / modules / providers / tests
22. `project-deps-and-tests.md` — modules **MUST NOT** reference providers
23. `project-naming-and-setup.md` — `Tessera.<Layer>...` naming
24. `module-structure-5-cap.md` — 5-project cap per folder
25. `project-ports.md` — 1990–2120 reserved pool

Plus the in-repo file `csharp/worker-audit-grep-catalog.md` and
`process/audit-debug.md` (5-step debug trace protocol).

### Conflict resolution

If a global rule conflicts with a Tessera-specific rule, the
**global rule wins** unless the Tessera rule explicitly overrides with
`<reason>` documentation. If a global rule conflicts with `.editorconfig`,
the editorconfig entry wins **only** if it carries owner approval
(see `~/.agents/rules/csharp/analyzers.md`).

## Build gate (mandatory before every commit)

```bash
dotnet format tessera.slnx --severity hidden
dotnet build tessera.slnx -c Debug    # exit 0, 0 warnings
dotnet test tests/unit/<touched-project> --no-build
```

The canonical command is `dotnet build tessera.slnx -c Debug` (NOT
per-project) — it runs compile + analyzers + format-check in one gate,
identical to CI. Per-project builds skip the format gate.

Plus self-audit grep catalog (`~/.agents/rules/csharp/worker-audit-grep-catalog.md`)
on changed files before commit.

## Commit format (mandatory)

```
[.stbl](feat/<area>): <subject>
```

- `[.stbl]` literal tag — `.stbl.solutions` project identifier.
- `feat/<area>` required feature path (lowercase kebab, `/`-nested).
  Areas: `feat/<module>`, `feat/<provider>`, `feat/host`, `feat/storage`,
  `feat/kernel`, `feat/fe`, `feat/tests`, `feat/docs`, `feat/meta`.
- Subject: imperative, ≤72 chars, no period.

Full spec: `~/.agents/rules/process/commit-format.md`.

## Hard DON'Ts

- **NEVER** run `bun run dev`, `dotnet run`, `dotnet watch`, `vite`,
  `tsc --watch`, `vitest --watch`, `playwright`, `puppeteer`,
  `chromium` — long-lived / browser-automation processes kill the
  agent runtime. See `~/.agents/rules/process/agent-runtime-safety.md`.
- **NEVER** run `taskkill //F //IM node.exe`, `pkill node`, blanket kills.
- **NEVER** commit without owner approval. `git commit --no-verify`
  is banned.
- **NEVER** suppress analyzer violations via `#pragma warning disable`,
  `.editorconfig` severity edits, or `-p:DisableFormatOnBuild=true`
  in a final build.
- **NEVER** hand-write or hand-edit EF Core migrations
  (`ef-migrations.md` — tool-generated only).
- **NEVER** expose secrets in TOML files, commit messages, or URL
  query strings (`.agents/rules/secrets.md`).

## Engineering zone

The host composition root (`src/host/Tessera.Host/Program.cs`),
`Directory.Build.props`, `.editorconfig`, build-tools targets, and
`.agents/rules/**` are the engineering zone. Changes require owner
approval per session (interactive) or written permission (batch /
unattended). See `~/.agents/rules/process/engineering-zone-access.md`.

## Project structure (one-line summary)

```
src/
├── host/          Tessera.Host (composition root only)
├── shared/        Tessera.Shared.{Kernel,Http,Authentication,Web,Validation}
├── modules/       Tessera.Modules.{Traces,Logs,Discovery,Health}
└── providers/     Tessera.Providers.<Name>  (Grafana datasource model)
tests/
├── unit/
│   ├── core/      Architecture + Host + Shared tests
│   ├── modules/   Per-module controller + mapping tests
│   └── providers/ Per-provider mapper + DTO tests
└── (integration/  — deferred per Phase 5 owner decision)
compose/           Dockerfile + docker-compose.yml + otel-collector
web/               Frontend monorepo (bun workspaces, MVP-02 deferred)
```

Layer rule: `modules` reference `shared` only; `providers` reference
`shared` only; `host` references everything; `modules` **MUST NOT**
reference `providers` (enforced by NetArchTest).

## Tech stack (pinned)

| Concern | Choice |
|---------|--------|
| Backend | .NET 10 (SDK pinned in `global.json`) |
| ORM | EF Core + SQLite (`Microsoft.EntityFrameworkCore.Sqlite 10.0.0`) — Phase 5 |
| Outbound HTTP | Refit + `Microsoft.Extensions.Http.Resilience` (Polly) |
| Config | Tomlyn 2.x (TOML only, no `appsettings.json`) |
| JSON | System.Text.Json + source-gen contexts |
| Frontend | React 19 + Vite + bun (Phase 6+ bundle into wwwroot) |
| Time | `TimeProvider` (never `DateTime.UtcNow`) |
| Errors | RFC 9457 ProblemDetails + `IExceptionHandler` |
| Auth | `IAuthProvider` abstraction (Phase 4a) + Guest + AdminBearer; LDAP / Keycloak via Phase 4b/c |
| Storage | SQLite + EF Core + Repository + Specification pattern |

Locked decisions: `.agents/docs/decisions/0001-mvp01-locked-decisions.md`.