# Tessera core design — observability model

> Status: drafted 2026-07-21 (design session). Locked decisions: `.agents/docs/decisions/0002-core-observability-model.md`.
> Scope: the *core semantics* of Tessera — the domain model, how logs/traces/metrics
> correlate, how errors/dependencies are derived, how aggregation is cached, and how
> "projects" scope everything. Provider-agnostic; Victoria is the first backend.

## 0. The one principle

Tessera supports **only OpenTelemetry** for logs and traces (hard constraint). That buys a
stable semantic vocabulary (OTel semconv), which lets us state the whole architecture in one
line:

> **Provider = transport + `wire → OTel` mapping. Domain = the OTel model. All smart logic
> (correlation, grouping, errors, dependencies, RED) lives ABOVE the provider on the OTel
> model and is written ONCE for every backend.**

This is what makes "multi-provider" real: backends differ only in *query API*
(Jaeger / PromQL / LogsQL); the *meaning* is always OTel. A second backend (Tempo, Jaeger,
Loki, Prometheus) = a new `wire → OTel` mapper, zero changes to the smart logic.

## 1. Canonical domain model (OTel-shaped)

Every provider maps its wire format into these.

**Span** — `traceId, spanId, parentSpanId, name, kind (SERVER|CLIENT|PRODUCER|CONSUMER|INTERNAL),
service.name (from Resource), start, durationMs, status {code: UNSET|OK|ERROR, message},
attributes[], events[] (incl. `exception`), links[], resource{}, scope{}`.

**Log record** (OTel log data model) — `timestamp, severityNumber (1–24), severityText, body,
attributes[], traceId, spanId, resource{}`. **Level is `severityNumber`**, bucketed to
TRACE/DEBUG/INFO/WARN/ERROR/FATAL — not free-text (kills the `WARN/warning/W/4` zoo).

**Resource** — `service.name, service.namespace, service.instance.id, deployment.environment,
k8s.*`, plus custom attrs (`team`, `domain`). This is what "projects" (§7) predicate on.

## 2. Correlation — the hero

### 2.1 Request-view keyed by `trace_id`

The "trace detail" screen is really a **request view keyed by `trace_id`** = the **union** of
whatever sources hold that id: spans (VT), logs (VL), and later metric exemplars (VM). Any
subset that exists is worth rendering:

- spans + logs → full waterfall + correlated logs
- **logs only** (trace sampled out / trace retention expired) → "log-only trace view" + banner
- **spans only** (no logs emitted) → waterfall alone

One mental contract, not three special cases.

### 2.2 The link is two fields (deterministic)

Logs carry `trace_id` + `span_id`; spans carry `trace_id`/`span_id`/`parent_span_id`. Join by
id, **never by time** (clock skew across hosts). Example (`POST /checkout`):

```
14:03:21.412 ERROR checkout-api trace=a1b2… span=span_001 "failed to charge card: StripeCardError"
14:03:21.512 WARN  checkout-api trace=a1b2… span=span_001 "payment retry attempt 1 of 3"
```
`span_001` = the CLIENT span `POST /v1/charges → stripe`. Clicking it shows exactly these two.

### 2.3 What Tessera queries (join is ours)

- spans: VT Jaeger API `GET /select/jaeger/api/traces/{traceId}`
- logs: VL LogsQL `trace_id:"{traceId}"` (≤500, no streaming)
- span filter: **fetch trace logs once, filter by `span_id` client-side** → span-click is instant
- Neither backend does the cross-source join — that is Tessera's value-add.
- Dependency: VL must ingest logs with `trace_id`/`span_id` as **fields** (OTel log→VL mapping).

### 2.4 Both directions

- span → its logs (trace detail)
- log line → trace (Logs page): `trace_id` → open `/traces/{id}`, highlight `span_id`

### 2.5 span **events** ≠ log **records**

The span panel shows TWO scoped sources: (1) **span events** from the trace itself (VT) — the
`exception` event with `exception.type/message/stacktrace`; (2) **log records** from VL. The
same incident often appears in both. **Show both, labelled by source**; optional collapse by
`exception.type`+time (toggle, never forced).

### 2.6 Timeline markers (the "wow")

Logs have timestamps → render them as **markers on the span bar** in the waterfall
(`●ERROR @+608ms`). Operator sees *where inside the span* it burned. MVP may start with a
filtered panel; markers are the same data rendered on the bar.

### 2.7 Honest edge cases

- log with `trace_id` but no `span_id` → attach at trace level
- log with no `trace_id` → orphan; findable only on the Logs page (logger not wired to OTel context)
- trace sampled out but logs kept → §2.1 log-only view (not a 404)
- correlation quality = instrumentation quality → semconv reliance + config escape hatch (§8)

## 3. Errors

- A span is an error when **`status.code == ERROR`** (OTel canonical). Fallback only:
  `http.response.status_code >= 500` / `rpc.grpc.status_code != 0`. (A 404 is not an error;
  a cancelled context can be an error with no HTTP code.) All overridable by config.
- Exception detail from **`exception` span events** (`exception.type/message/stacktrace`).
- **Trace status** = root span status **+ a "N errored spans inside" badge** (common: root 200
  OK, inner span failed + retried).
- **Error grouping**: MVP by `exception.type` + normalized message; **fingerprint** (hash of
  normalized stack frames, Sentry-style) later, via `error.grouping: message | fingerprint`.
  Fingerprint doubles as a filter: "all traces of this one bug".

## 4. RED (Rate / Errors / Duration)

Three numbers per service/operation: throughput, error fraction, latency percentiles.

**Default — from OTel metrics (PromQL).** Cheap, exact, and **not sampled** (so more accurate
than spans). Example (`checkout-api`, `POST /checkout`):

```promql
# R
sum(rate(http_server_request_duration_seconds_count{service_name="checkout-api", http_route="/checkout"}[5m]))
# E (5xx share)
sum(rate(http_server_request_duration_seconds_count{service_name="checkout-api", http_route="/checkout", http_response_status_code=~"5.."}[5m]))
# D (p95)
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{service_name="checkout-api"}[5m])))
```

**Fallback — no metrics:** show `count + error-rate ≈` from the spans already fetched (labelled
"≈ from sampled traces"), **no percentiles**, + a "enable OTel metrics" nudge. **We do NOT
build a heavy span-aggregation engine for RED.** (Requires `IMetricsProvider` — MVP-02 per
ADR-0001.)

## 5. Service dependencies — derived from spans

Portable (no backend `/dependencies` API). Rules, on the `POST /checkout` trace:

| In the spans | Rule | Graph output |
|---|---|---|
| CLIENT `checkout-api` parent of SERVER `currency` | different `service.name`, real child SERVER | edge **checkout-api → currency** |
| CLIENT `checkout-api`, `db.system=postgresql`, no child SERVER | synthesize node from `db.system`/`db.name` | node **postgres** + edge |
| CLIENT `checkout-api`, `peer.service=stripe`, no child SERVER, ERROR | synthesize node from `peer.service`/`server.address` | node **stripe** + errored edge |
| PRODUCER→CONSUMER across traces | via span `links[]` + `messaging.*` | async edge |

Key trick: **DBs and external APIs emit no spans of their own** but enter the graph from the
CLIENT span's attributes. An edge = a mini-RED between two services (count / error-rate / p95).

- **Per-trace graph** (one trace's mini-map): computed on the fly, cheap → in core/MVP.
- **Global service map** (all traces, weighted): cross-trace aggregation → cached rollup (§6),
  post-core.

## 6. Aggregation cache — derived views only

Two data classes, two strategies:

- **Raw data (spans, logs) — stays in the backend.** Query on demand; cache only *hot queries*
  briefly. **Full-text log search is VL's job** — Tessera never builds a log index.
- **Derived views — background-computed rollup cache**: dep graph, error clusters, RED-fallback
  counters. Small numbers, keyed `(project × service × op × time-bucket)`.

Rules that keep this from becoming a second Victoria:
- **Time-bucketed** (e.g. 5-min buckets): hot window resident, old spilled to disk, oldest
  evicted. "last 24h" = sum 288 buckets, not "fetch 24h of spans".
- **Rebuildable, never source of truth.** Lose the disk spill → re-derive from the backend.
  Preserves ADR-0001 Decision 8 (Tessera owns no telemetry) and read-only trust.
- **Push down what the backend does well** (PromQL aggregation, VL stats, VT services/ops).
  Derive only what no backend gives portably (span-derived graph, cross-trace error clusters).
- RAM-capped (target ~50% configurable); if you find yourself building retention/downsampling/
  compaction — **stop, that is Victoria.**

This turns the "thin proxy" into a **read-model / materialized-view service** — a conscious ops
commitment (memory sizing, disk, warmup), but still a cache over Victoria, not a store.

## 7. Projects — declared attribute-predicates

A **project** is a named predicate over OTel attributes — a view/scope, not a partition:

```yaml
project: payments
match: service.namespace == "checkout" || service.name in ["stripe","currency"]
```

- The predicate vocabulary is OTel semconv → works uniformly for logs, traces, metrics.
- **Declared (finite) → bounds the aggregation surface.** Rollups (§6) are cached per
  `(project × …)`; a finite project set keeps the cache finite. Declarativeness here is what
  makes background aggregation computable, not bureaucracy.
- As-code (YAML, git/CI) — fits the platform's everything-as-code direction.
- Same concept later becomes the **RBAC / multi-tenancy boundary** and dashboard scope. One
  concept covers grouping + cache boundary + permissions + tenancy.
- Projects may overlap (a span in 3 projects is fine) — projects are *views*; counters across
  overlapping projects don't sum to a whole. Must be explicit in UX.

## 8. Provider responsibilities

Each provider maps its wire format → the §1 model. semconv is trusted ~99%; a config layer is
the escape hatch (URL templating, status mapping, attribute synonyms) — not the default.

| Domain need | Victoria source | Notes |
|---|---|---|
| spans / trace tree | VT Jaeger API | tree reconstructed from `references[CHILD_OF]` |
| logs (by trace/query) | VL LogsQL (NDJSON) | `trace_id`/`span_id` must be fields |
| services / operations | VT `/services`, `/operations` | don't recompute if backend gives it |
| metrics / RED | VM PromQL/MetricsQL | `IMetricsProvider`, MVP-02 |

## 9. Open questions (triage)

**Decide at implementation (design fixes the shape, numbers come later):**
- Project predicate granularity — resource-attrs only (cheap rollup) vs also span/log attrs
  (powerful, costlier). Lean resource-first.
- Cache internals — bucket size (1m/5m), RAM-cap policy, eviction, spill format.

**Scope decisions (recommended, confirm):**
- **Global service map deferred** — MVP ships the per-trace graph; global map lands with the
  rollup cache post-core.
- **RED lands with `IMetricsProvider`** (MVP-02); until then service cards show the degraded
  span-approx.

**Locked (see ADR-0002):** OTel-only; provider-maps-wire→OTel; request-view union; error model;
RED-from-metrics; deps-from-spans; rollup cache = derived+rebuildable; projects = declared
predicates.

## Related documents

- `.agents/docs/decisions/0002-core-observability-model.md` — locked decisions (this doc's ADR)
- `.agents/docs/decisions/0001-mvp01-locked-decisions.md` — Decision 8 (external data only), auth/storage
- `.agents/docs/architecture.md`, `modules.md`, `victoria-stack.md` — flows, contracts, VT/VL/VM APIs
- `DESIGN.md` (repo root) — FE / screen brief
- memory: `tessera-otel-only-core`, `tessera-product-vision`, `agent-native-platform-pattern`
