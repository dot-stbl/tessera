# ADR-0003 — Core module cut + smart-logic placement

> Status: Accepted 2026-08-07  
> Owner: bradw  
> Source: architecture session (TUI) after #10 land on `develop`

## Context

ADR-0002 locked observability semantics (OTel domain, provider = wire→OTel,
request-view, errors, RED, deps, cache, projects). It did **not** fix:

1. Where pure derivation vs orchestration live in the solution.
2. How many modules we grow before the 5-project folder cap forces nest.
3. Which module owns errors / RED HTTP surfaces.
4. When projects + rollup cache enter the codebase.
5. How FE tracks API drift during P5–P9.

This ADR locks those so P5–P9 agents do not freestyle structure.

## Decision 1 — Smart logic: Kernel/Analysis + module orchestration (A3)

**Decision:**

| Kind | Where |
|------|--------|
| Pure derivation (span tree, error badge, dep edges from one trace, RED assembly from samples) | `Tessera.Shared.Kernel/Analysis/` — pure static, no I/O, passes `DomainPurityTests` |
| Orchestration (call N providers, combine, page) | Owning **module** service (e.g. `RequestViewService` in Traces) |
| `wire → OTel` mapping | Provider only |

**Rationale:** Matches ADR-0002 D2 (“smart logic above providers, written once”).
Keeps Kernel pure and unit-testable; modules stay the HTTP + multi-provider
fetch boundary. Avoids a new shared project that would burn the shared cap.

**Consequences:**

- P5 reserves the shape; population starts P6+.
- Controllers stay thin: inject orchestration service, not N providers with
  inline combine logic.
- No `Tessera.Shared.Analysis` project unless Kernel/Analysis outgrows
  folder rules and we explicitly nest/split later.

## Decision 2 — Fat module slices until nest is forced (A1)

**Decision:** Stay at the current five modules. Map product surfaces as follows:

| Surface | Module |
|---------|--------|
| Trace list, request view (`/traces`, `/traces/{id}`) | `Modules.Traces` |
| Log explorer (`/logs`) | `Modules.Logs` |
| Service inventory, RED, per-trace / service mini-map inputs | `Modules.Discovery` |
| Composite health | `Modules.Health` |
| User preferences (SQLite) | `Modules.Preferences` |

Do **not** add `Modules.Errors`, `Modules.Metrics`, or `Modules.Projects` in
the P5–P9 window unless a first-class product surface (own nav + long-lived
API) forces a sixth module — then nest `modules/core|extended` per
`module-structure-5-cap.md`.

**Rationale:** Cap is already 5. Nesting early is churn without a real product
boundary. Fat slices keep ownership obvious for MVP screens.

## Decision 3 — HTTP ownership: Traces → errors; Discovery → RED

**Decision:**

- **Errors inbox / error groups** — `Modules.Traces`  
  (routes under `/api/v1/…` chosen at implementation; data from span
  `status.code` + exception events, later grouping).
- **RED (rate/errors/duration) for services/operations** — `Modules.Discovery`  
  (extend service inventory or sibling routes; source = `IMetricsProvider`
  with span-approx fallback per ADR-0002 D5).

**Rationale:** Errors are “bad requests / bad spans” — same mental model as
Traces. RED is a property of services/ops — same as Discovery. Avoids a
god Traces hub and avoids stuffing error UX into inventory-only Discovery.

**Consequences:**

- `IMetricsProvider` (P5) is consumed first by Discovery, not a Metrics module.
- Error grouping endpoints do not require a new project.

## Decision 4 — Projects + rollup cache: post-core only

**Decision:** No projects predicate schema, no rollup cache engine, no global
service-map aggregation in P5–P9. Per-trace dependency mini-map (cheap, on the
fly) is in core; global weighted map waits for post-core cache work.

**Rationale:** ADR-0002 already describes the end state. Implementing shape
early freezes API without product pull. Post-core can still introduce projects
as declared predicates and rebuildable rollups without rewriting OTel domain.

## Decision 5 — FE contract: evolve free until core stabilizes

**Decision:** Backend OpenAPI may change freely through P5–P9. Frontend stays on
hand-written mocks / existing pages. OpenAPI snapshot + kubb/MSW parallel track
starts **after** core request-view / errors / RED contracts settle — not now.

**Rationale:** Owner deferred mock pipeline. Freezing early either blocks
backend or produces a dead snapshot. Stabilize then codegen.

## Out of scope

- Concrete route strings for errors/RED (implementation issue).
- P5 task breakdown (already #4–#8).
- Visual / FE screen design (separate UX stub).

## Related

- ADR-0001 — platform (auth, storage, external data only)
- ADR-0002 — observability model
- `.agents/docs/core-design.md` — narrative
- `.agents/plans/mvp-2-core/P5-otel-model/PLAN.md` — next code track
