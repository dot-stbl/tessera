# Tessera state

## Milestone
MVP-01 — backend-only (Traces + Logs + Discovery + Health + Victoria provider + Tessera.Host)

## Status
**Phase 1 (Backend foundation) — DONE.** Phase 2-5 not started.

## Progress
- **Phase 0** (rules + provider scaffold) — DONE (6 commits)
- **Phase 1** (shared primitives + provider impls) — DONE (19 commits, see commits below)
- **Phase 2** (4 backend modules: Health, Traces, Logs, Discovery) — NOT STARTED
- **Phase 3** (Host composition root + TOML config + admin bearer + shared plumbing) — NOT STARTED
- **Phase 4** (architecture tests + Testcontainers integration) — NOT STARTED
- **Phase 5** (E2E verify + handoff) — NOT STARTED

Phase 1 deliverable: 22-project solution builds clean, 34/34 unit tests passing.

## Working agreement
- Owner confirms strategic decisions (architectural forks) before code work begins
- PLAN.md scaffolded via `soly_workflow new <slug>`; fleshed out via `discuss` then `plan`
- Each plan task has acceptance criteria — implemented exactly
- Decisions logged here for audit trail
- HANDOFF.md updated when strategic context shifts
- **Owner instruction 2026-07-19:** "делай до конца план, меня не спрашивай" — execute full MVP-01 without re-asking

## Decisions

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-07-19 | Tessera positioning: Grafana-analogue APM (multi-provider), NOT Victoria-specific UI | MVP-01 ships with Victoria as first provider via new `src/providers/` layer; future phases add Tempo/Jaeger/Loki/Mimir without refactoring modules. Architecture: provider interfaces in `Tessera.Shared.Kernel`, concrete implementations in new `src/providers/` top-level layer. No own collector/storage layer — reuse external collectors. |
| 2026-07-19 | New top-level `src/providers/` folder for concrete provider implementations | Mirrors Grafana datasource model: providers pluggable, modules provider-agnostic. Composition root wires active provider(s) in DI. |
| 2026-07-19 | Provider interfaces (`ITraceProvider`, `ILogProvider`, `IMetricsProvider`, `IDiscoveryProvider`, `IHealthProvider`) in `Tessera.Shared.Kernel` | Kernel already holds domain primitives; provider contracts are natural extension. Keeps `shared/` at 5-project cap. |
| 2026-07-19 | MVP-01 module scope: 4 modules — Traces + Logs + Discovery + Health | Matches HANDOFF baseline. Health as DI verification canary, Discovery aggregates services + streams. |
| 2026-07-19 | Victoria is MVP-01 first-and-only provider; second provider deferred to MVP-02+ | User explicitly chose "build abstraction now" over "defer to MVP-02". Cheap upfront, no refactor later. |
| 2026-07-19 | MVP-01 is backend-only; frontend integration deferred to MVP-02 | Owner direction 2026-07-19 ("без моков, сразу бек нормальный делаем, а потом ui уже"): MVP-01 ships backend (4 modules + provider abstraction + host composition root), testable end-to-end via Testcontainers + curl. FE integration deferred to MVP-02. |
| 2026-07-19 | Folder organization rule: max 3 .cs files per folder, 1 type per file, namespace mirrors folder path, file name = type name | Prevents God Object files, makes IDE tree views collapse cleanly, enforces grep-discoverability. New rule at `.agents/rules/coding/folder-organization.md`. |
| 2026-07-19 | Ban private methods globally — extract helpers to file-static classes | No `private` business logic in production classes; helpers go in `internal static class XxxMapper.cs` files. Single Responsibility Principle enforcement. Allowlist: framework overrides (Equals, Dispose), minimal API endpoint handlers. |
| 2026-07-19 | No Shouldly/NSubstitute/Bogus in MVP-01 tests; plain xUnit `Assert.*` + hand-written doubles | Transitive deps (Castle.Core, DiffEngine, Newtonsoft.Json) have net10 incompat. Reintroduce when fixed. |
| 2026-07-19 | Extract project-neutral C#/process rules to global `~/.agents/rules/` | Plexor + tessera share same core C# rules. Project-specific bits (theme names, ports, slnx layout) stay in each project's `.agents/rules/`. Verified location: pi-soly loads from `~/.agents/rules/` (NOT `~/.pi/agent/rules/` which I tried first by mistake). |

## Tech stack status

| Component | Status | Notes |
|-----------|--------|-------|
| **Tessera.Shared.Kernel** | ✅ DONE | Domain primitives (Tenant, TraceId, SpanId, TimeRange, LogLevel, Page<T>, Result<T>, Error), domain models (Trace, Span, LogEntry, ServiceSummary), 4 provider interfaces + support types. All in category subfolders (max 3 files/folder). |
| **Tessera.Shared.Http** | ✅ DONE | `RefitExtensions.AddTesseraRefitClient<T>`, `BearerTokenHandler`, `HttpClientAuthOptions`. Polly standard resilience wired through `Microsoft.Extensions.Http.Resilience` 10.8.0. |
| **Tessera.Providers.Victoria** | ✅ DONE | 4 provider implementations (Trace/Log/Discovery/Health), Jaeger + LogsQL DTOs, NDJSON parsing, span tree reconstruction (flat list), `VictoriaServiceCollectionExtensions.AddVictoriaProvider()` DI extension. 34/34 unit tests passing. |
| **Tessera.Host** | ❌ NOT STARTED | Composition root only (placeholder Program.cs). No TOML config, no admin bearer, no endpoint mapping. |
| **4 Tessera.Modules.*** | ❌ NOT STARTED | Traces, Logs, Discovery, Health endpoints not implemented. |
| **Architecture tests** | ❌ NOT STARTED | Tessera.ArchitectureTests project empty. |
| **Testcontainers integration** | ❌ NOT STARTED | Tessera.Host.Integration + Tessera.Victoria.Integration empty. |
| **Format gate** | ✅ GREEN | dotnet build exit 0; 46 hidden/info warnings remaining (pre-existing noise, none affect build). RCS1141/RCS1142/IDE0320 suppressed via `.editorconfig` last-resort [*.cs] block. |

## Build state

```
$ dotnet build tessera.slnx -c Debug
22 projects, 0 errors, 0 warnings

$ dotnet test tests/unit/providers/Tessera.Providers.Victoria.Unit
34/34 passing
```

## Naming audit (per worker-audit.md self-audit grep)

All passing for tessera `src/`:
- ✅ No forbidden abbreviations (`ct`, `req`, `resp`, `err`, `msg`, `svc`, `u`, `x`, `tmp`)
- ✅ No underscore-prefixed fields
- ✅ No banned suffixes (Dto, Model, Impl, Util, ViewModel)
- ✅ No private methods (zero, after refactor)
- ✅ No `throw ex` (vs `throw`)
- ✅ No `async void`
- ✅ No `_ = discard`
- ✅ No `ArgumentNullException.ThrowIfNull`
- ✅ No `#region`
- ✅ No block-scoped namespaces
- ✅ All public types have XML `<summary>`

## Recent commits (Phase 1)

```
cd52a37 [.stbl](feat/meta/format): suppress hidden RCS1141/1142/IDE0320 + fix trailing newlines
88ddded [.stbl](feat/meta/analyzers): remove ArgumentNullException.ThrowIfNull in DI extension
8ad0136 [.stbl](feat/meta/rules): extract global C#/process rules to ~/.pi/agent/rules/
62ef197 [.stbl](feat/meta/rules): fix plexor-imported inconsistencies in rules
bac9b8e [.stbl](feat/meta/analyzers): remove Meziantou.Analyzer
1fbf09d [.stbl](feat/meta/rules): ban private methods; disable CA1711; refactor Victoria to file-static mappers
83c18e9 [.stbl](feat/providers/victoria): provider impls + DI extension + unit tests
9f6e482 [.stbl](feat/providers/victoria): add Refit clients + Jaeger/LogsQL DTOs
a12141a [.stbl](feat/shared-http): add Refit extension + BearerTokenHandler + auth options
d381756 [.stbl](feat/shared-kernel): add primitive unit tests + fix test project deps
f82dccc [.stbl](feat/shared-kernel): add 4 provider interfaces + support types
18ed55b [.stbl](feat/shared-kernel): add domain models (Trace, Span, LogEntry, Service)
c301fed [.stbl](feat/meta/rules): add folder-organization rule
7102452 [.stbl](feat/shared-kernel): restructure primitives into category folders
1166f19 [.stbl](feat/meta): add Tessera.Providers.Victoria to tessera.slnx
1f6a585 [.stbl](feat/meta): scaffold Tessera.Providers.Victoria project
```

## Open questions for Phase 2-5

- TOML config location precedence (cwd vs /etc/tessera/ vs env TESSERA_CONFIG)?
- Admin bearer auth model: middleware or endpoint filter?
- CORS policy: permissive in dev only, strict in prod?
- OpenAPI emission: built-in `Microsoft.AspNetCore.OpenApi` or Swashbuckle?
- Endpoint /api/v1 prefix? Minimal API module registration pattern?
- Tessera.Host wiring: one `AddTracesModule()` per module?

## Next step

Continue executing Phase 2 (4 backend modules: Health, Traces, Logs, Discovery). Owner has confirmed "делай до конца план, меня не спрашивай" — proceed without re-asking.
