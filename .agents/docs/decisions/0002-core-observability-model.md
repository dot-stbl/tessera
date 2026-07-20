# ADR-0002 — Core observability model

> Status: Accepted 2026-07-21
> Owner: bradw
> Source: core-design session 2026-07-21

## Context

ADR-0001 locked the MVP-01/MVP-02 *platform* decisions (auth, storage, FS layout, external
data sources). It did not define the *core observability semantics* — the domain model and how
logs/traces/metrics correlate, how errors and service dependencies are derived, how aggregation
is cached, and how work is scoped. This ADR locks those. Full narrative + worked examples:
`.agents/docs/core-design.md`.

---

## Decision 1 — OpenTelemetry only, OTel-shaped domain model

**Decision:** Tessera supports **only OpenTelemetry** for logs and traces — no other format.
The canonical internal domain model is OTel-semconv-shaped (Span with `span.kind`/`status.code`/
`exception` events/`links`; Log with `severityNumber`; Resource with `service.name` etc.).

**Rationale:** One format ⇒ a stable semantic vocabulary ⇒ correlation/grouping/deps become
deterministic reads of known fields instead of per-backend heuristics. OTel SDKs exist for every
language; incomplete instrumentation is the producing project's responsibility.

**Alternatives considered:** Multi-format ingest normalization (Zipkin, bare Loki labels, ad-hoc
JSON) — multiplies mapping/heuristic surface, kills the "written once" property.

**Consequences:** Log level is `severityNumber` (not free-text). Producers must be OTel; a config
layer is the escape hatch for gaps (~1%), not the default.

**Refs:** `core-design.md` §0–1.

---

## Decision 2 — Provider maps `wire → OTel`; smart logic lives above providers

**Decision:** A provider is **transport + `wire → OTel` mapping** only. All correlation,
grouping, error detection, dependency extraction, and RED logic operate on the OTel model **above**
the provider and are written **once** for all backends.

**Rationale:** This is what makes multi-provider real — backends differ only in query API
(Jaeger / PromQL / LogsQL); meaning is always OTel. A 2nd backend = a new mapper, zero changes to
the smart logic.

**Consequences:** Reinforces ADR-0001's provider abstraction. Tempo/Jaeger/Loki/Prometheus plug in
as mappers. The domain/module layer never contains backend-specific branching.

**Refs:** `core-design.md` §0, §8; ADR-0001 Decision 8.

---

## Decision 3 — Trace detail = request-view keyed by `trace_id` (union, degradable)

**Decision:** The trace-detail screen is a **request view keyed by `trace_id`** — the union of
spans (VT) + logs (VL) + (later) metric exemplars. Correlation is deterministic via
`trace_id`/`span_id` (never by time). It **degrades** rather than 404s: logs-only when the trace
was sampled out; spans-only when no logs. Both directions (span→logs, log→trace). span `events`
and log `records` are distinct sources, shown with source labels. Logs render as **timeline
markers** on the span bar.

**Rationale:** One mental contract instead of three special cases; the log-only view is gold in
incidents (trace sampled out, logs still tell the story) and costs nothing extra.

**Consequences:** Fetch trace logs once (≤500) and filter by `span_id` client-side (instant span
click). VL must ingest `trace_id`/`span_id` as fields. Hard 404 only when neither spans nor logs
exist for the id.

**Refs:** `core-design.md` §2.

---

## Decision 4 — Errors keyed on `status.code`; exception events; grouping message→fingerprint

**Decision:** A span is an error when `status.code == ERROR` (fallback `http.response.status_code
>= 500` / grpc `!= 0`; all config-overridable). Exception detail from `exception` span events.
Trace status = root status **+ "N errored spans inside" badge**. Error grouping: MVP by
`exception.type` + normalized message; **fingerprint** (normalized-stacktrace hash) later, via
`error.grouping: message | fingerprint`.

**Rationale:** `status.code` is the OTel-canonical truth (404 ≠ error; cancelled ctx may be an
error with no HTTP code). Fingerprint clusters one bug across differing messages and doubles as a
"all traces of this bug" filter.

**Refs:** `core-design.md` §3.

---

## Decision 5 — RED from OTel metrics (PromQL); light span fallback; no heavy engine

**Decision:** RED (Rate/Errors/Duration) is computed **from OTel metrics via PromQL** by default
(`http.server.request.duration` histogram). When metrics are absent, degrade to `count +
error-rate ≈` from already-fetched sampled spans (no percentiles) + an "enable metrics" nudge.
**No heavy span-aggregation engine** is built for RED.

**Rationale:** Metrics are cheap, exact, and **unsampled** (more accurate than spans); the backend
(VM) does the math. Span-derived percentiles are expensive and approximate — not worth the code.

**Consequences:** Requires `IMetricsProvider` (MVP-02 per ADR-0001); until then service cards show
the degraded approximation. Auto-select by presence of metrics.

**Refs:** `core-design.md` §4; ADR-0001 (`IMetricsProvider` deferred).

---

## Decision 6 — Service dependencies derived from spans

**Decision:** The service graph is **derived from spans**, not a backend `/dependencies` API:
CLIENT→SERVER edges across differing `service.name`; external/DB nodes synthesized from
`peer.service` / `server.address` / `db.system`; async edges via span `links[]` + `messaging.*`.
Per-trace graph computed on the fly (core); global weighted map = cross-trace aggregation
(cached rollup, post-core).

**Rationale:** Portable across backends (many lack a deps endpoint); DB/external nodes appear even
though they emit no spans of their own. An edge = a mini-RED between two services.

**Alternatives considered:** Use VT's Jaeger `/dependencies` — backend-specific, not portable.

**Refs:** `core-design.md` §5.

---

## Decision 7 — Background rollup cache: derived views only, rebuildable, never a store

**Decision:** A background-computed cache holds **derived views only** (dep graph, error clusters,
RED-fallback counters), keyed `(project × service × op × time-bucket)`. Raw spans/logs stay in the
backend (hot-query cache only); **full-text log search is VL's job**. The cache is time-bucketed
(hot in RAM ~50% cap, spilled to disk, evicted), and **rebuildable — never the source of truth**.

**Rationale:** Turns O(fetch-all) queries into O(sum-buckets) without owning telemetry. Preserves
ADR-0001 Decision 8 and read-only trust. Prevents rebuilding Victoria (no retention/downsampling/
compaction inside Tessera).

**Consequences:** Tessera becomes a **read-model / materialized-view service** (memory/disk/warmup
ops) — but a cache over the backend, not a store. Cold start rebuilds from spill + backfill.

**Refs:** `core-design.md` §6; ADR-0001 Decision 8.

---

## Decision 8 — Projects = declared attribute-predicates (view / scope)

**Decision:** A **project** is a named, **declared** predicate over OTel attributes (e.g.
`service.namespace == "checkout" || service.name in [...]`), authored as-code (YAML). It is a
view/scope, not a partition; projects may overlap.

**Rationale:** OTel attributes are a uniform predicate vocabulary across logs/traces/metrics. A
*finite declared* set **bounds the aggregation surface** (rollups cached per project). Same concept
later serves as the RBAC / multi-tenancy boundary and dashboard scope — one concept, four jobs.

**Consequences:** Overlap must be explicit in UX (counters across overlapping projects don't sum).
Predicate granularity (resource-only vs also span/log attrs) decided at implementation
(resource-first leaning).

**Refs:** `core-design.md` §7.

---

## Consequences summary

### What this establishes
- OTel-only core with an OTel-shaped domain model
- "smart logic once, above providers" architecture
- request-view-by-`trace_id` correlation contract (degradable union)
- error / RED / dependency derivation rules
- a rebuildable derived-rollup cache (read-model service)
- projects as declared attribute-predicates (grouping + cache bound + future RBAC/tenancy)

### Deferred
- Global service map (per-trace graph ships in core; global map with the rollup cache)
- RED (lands with `IMetricsProvider`, MVP-02)
- Error fingerprinting (param, post-MVP)
- Timeline markers may follow the filtered log panel

### Risks
| Risk | Mitigation |
|------|------------|
| Cache becomes a second TSDB | Derived views only; rebuildable; no retention/compaction — push down to backend |
| RAM blow-up from caching raw data | Cache small rollups, not the span/log firehose; time-bucketed + capped |
| Correlation gaps | = instrumentation quality; semconv 99% + config escape hatch; log-only/spans-only degrade |
| Project × aggregation combinatorics | Projects are declared (finite) → bounded surface |

## Related documents
- `.agents/docs/core-design.md` — full narrative + worked examples
- `.agents/docs/decisions/0001-mvp01-locked-decisions.md` — platform decisions (auth/storage/data)
- `.agents/docs/{architecture,modules,victoria-stack}.md`
- `DESIGN.md` (repo root) — FE / screen brief
