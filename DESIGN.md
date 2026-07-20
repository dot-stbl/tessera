# Tessera — Frontend & Design Brief

> **Purpose.** A single, self-contained brief to design Tessera's screens **inside the
> design system that already exists** (Base UI + shadcn wrappers + OKLCH tokens in
> `web/apps/console/src/index.css`). Hand this to Claude design / a designer. It says what
> the product is, what screens it needs, what data each screen has, and what NOT to build.
> Ground truth for tokens is `index.css`; ground truth for API shapes is
> `web/apps/console/src/shared/api/types.ts`. This brief supersedes the drifted
> `.agents/docs/ui/*` docs.

---

## 1. Context — why this brief exists

Tessera's **backend (MVP-01) is code-complete**; the **frontend is the MVP-02 track**. A
working FE scaffold already exists (React 19 + Vite + bun, ~110 Base UI/shadcn primitives,
APM components, Ladle catalog, mock-first API, TanStack Router shells for
`/traces` · `/logs` · `/services` · `/dashboards`). We are **not greenfield** — we are
designing screens and refining a design system that is already on disk. Do not reinvent the
token system or primitive kit; design *within* it and extend where a screen needs it.

## 2. Product in one paragraph

Tessera is a **self-hosted APM UI for the Victoria observability stack**
(VictoriaTraces / VictoriaLogs / VictoriaMetrics), positioned as a **Grafana-analogue,
multi-provider** front-end (Victoria is just the first provider; Tempo/Jaeger/Loki plug in
later). The backend is a **thin read-only proxy** — no ingest, no persistence of telemetry.
The experience target is **Kibana-APM / Jaeger-style**: find a trace → see its waterfall →
read correlated logs. It's an **operator tool**: information-dense, keyboard-friendly,
desktop-first.

## 3. What already exists (reuse — do not rebuild)

| Area | Reality |
|------|---------|
| Stack | React 19, Vite 6, **bun** workspaces, TanStack Query + Router (code-based), i18next (**en + ru**) |
| Primitives | ~110 shadcn-style components on **Base UI** in `shared/ui/primitives/` (Button, Input, Select, Dialog, Tabs, Table, Tooltip, Popover, Sheet, Command, …) + Tessera flavors (`status-pill`, `stat`, `mono-num`, `copyable-text`) |
| APM components | `shared/ui/apm/`: `Waterfall`, `SpanRow`, `SpanDetailPanel`, `TraceTable`, `LogEntry`, `LogLevel`, `Duration`, `TimeFormat`, `TraceId`, `ServiceCombobox`, `TimeRangePicker`, `DashboardGrid` |
| App shell | Topnav (4-tile brand mark, red accent) + left icon sidebar + main — `shared/ui/app-shell/` |
| Tokens | `apps/console/src/index.css` — Tailwind 4 `@theme` + OKLCH (see §9) |
| Catalog | **Ladle** (`bun run playbook:dev`, port 2006), 26 stories |
| Screens wired | `/traces`, `/logs`, `/services` (mock data); `/traces/$traceId` detail just assembled; `/dashboards` + `/settings` are placeholders |

## 4. Brand & visual principles

- **Monochrome surfaces + one deep red.** Surfaces are pure OKLCH grayscale (white → near-black).
  The only brand color is the **deep red `--primary`** (`oklch(0.505 0.213 27.518)` light /
  `oklch(0.70 0.20 25)` dark), used for primary actions, links, the active/focus accent, and the
  accent tile in the mark. NOTE: `--accent`/`--ring` are **neutral gray**, not the red.
- **Status colors are the only other color, and they are semantic** — never decorative:
  `--ok` (green), `--err` (red), `--warn` (amber), `--idle` (gray), `--slow` (amber-orange, latency
  breach), plus log-level colors `trace/debug/info/warn/error/fatal`.
- **Numerics are monospace + tabular** (`font-mono` + `tabular-nums`): durations, timestamps,
  counts, trace/span IDs. Durations auto-scale (ns→µs→ms→s→m→h) with `is-slow` (>1s) / `is-very-slow`
  (>5s) tints.
- **Density over whitespace.** This is an ops surface; prefer compact tables, tight rows, quick
  scanning. Desktop-first (mobile works but is not optimized).
- **Typography:** sans/headings = **Geist**, body = **Onest**, mono = **JetBrains Mono** stack.
- **Brand mark:** 4-tile scatter (3 black + 1 red `#A60000`) — `assets/mark-tessera*.svg`.
- **Light + dark are both first-class.** Every color must invert.

## 5. Screens to design (MVP scope)

For each screen the designer needs: purpose, the **data available** (bounded by the module
contracts — see §6/§7), and the states to cover (loading / empty / error).

1. **App shell** — topnav (brand → home, global **time-range picker** + **service** selector,
   **health indicator**, theme + language toggles) + sidebar (Traces, Logs, Services, Dashboards,
   Settings) + main content. Health indicator reflects the composite `/health` status.
2. **Trace Explorer** (`/traces`) — filterable **table**: Service·Operation, status pill, duration
   (with slow tints), span count, started (relative + absolute hover). Filters: service, status,
   min/max duration, time range. **Cursor** pagination (not page numbers). Row → Trace Detail.
3. **Trace Detail** (`/traces/$traceId`) — **THE core screen.** Three regions:
   (a) **Waterfall** of the reconstructed span tree (indented parent/child, offset+duration bars,
   time axis, optional critical-path highlight); (b) **Span detail panel** (flyout on span select:
   status, service·operation, span id, duration + self-time, tags/attributes, events with
   exceptions highlighted); (c) **Correlated logs** for the trace (filterable to the selected span).
   Design the split-view + how logs relate to the selected span.
4. **Logs** (`/logs`) — log list: level chip + time + service + message + expandable **structured
   fields**; filter by trace id; time range. (An ad-hoc **LogsQL editor** is *stretch* — a field is
   already stubbed; design it lightly but don't make it central.)
5. **Service Inventory** (`/services`) — **card grid**: service name, error-rate badge
   (ok / warn >1% / err >5%), span + error counts, top operations. Counts may be approximate or
   absent (design must degrade gracefully — see §8).
6. **Health** — composite per-backend status: `vt` / `vl` / `vm` each ok|error + latency (+ error
   text). Surfaces as the shell indicator and a section in Settings.
7. **Settings** (`/settings`) — Victoria endpoint URLs, admin **bearer token** (stored client-side),
   theme, language, a health check. **No login screen** (MVP is anonymous read; the token only
   gates future admin/write ops).
8. **System states** — global empty / error (ProblemDetails: code + message) / loading (skeletons)
   / not-found.

## 6. Core user journeys

- **Primary:** find a slow/errored trace → open waterfall → inspect a span's tags/events → read the
  correlated logs for that trace/span.
- **Secondary:** browse services → see error rates + operations → jump to that service's traces.
- **Operational:** check backend health; configure Victoria endpoints + token.

## 7. Data contracts per screen (what the UI actually has)

All times are **UTC unix milliseconds**. Shapes below are the current FE types
(`shared/api/types.ts`).

- **Trace list** — `ListTracesResponse { items: TraceSummary[], cursor?, hasMore }`;
  `TraceSummary { traceId(32 hex), rootService, rootOperation, startTime, durationMs,
  status: 'ok'|'error'|'unset', spanCount, services[] }`.
- **Trace detail** — `TraceDetail { traceId, rootService, rootOperation, startTime, durationMs,
  status, spans: Span[] }` where `Span { spanId, parentSpanId, service, operation, startTime,
  durationMs, status, tags: Record<string,string>, events: {time,name,attributes}[] }`. The span
  list is **flat** — the tree is built on the client (or server). **Logs are a separate call.**
- **Logs** — `ListLogsResponse { entries: LogEntry[], total }`;
  `LogEntry { timestamp, level, service?, traceId?, spanId?, message, fields? }`.
  MVP requires a `traceId` for the per-trace log panel; **hard cap ~500 logs, no streaming**.
- **Services** — `ListServicesResponse { items: ServiceSummary[] }`;
  `ServiceSummary { name, spanCount, errorCount, operations: {name,count}[] }`.
- **Health** — `HealthResponse { status: 'healthy'|'degraded'|'unhealthy',
  backends: { vt, vl, vm: { status:'ok'|'error', latencyMs?, error? } } }`.

**What Victoria can/can't give** (bounds the UI): rich per-span **tags + events** and arbitrary
structured **log fields** ARE available → design expandable key/value detail. **Service-to-service
topology (service map)** and **metrics / RED panels** are **NOT** available in MVP (stretch).

## 8. Out of scope — do NOT design these for MVP

- Service map / dependency graph; **RED metric panels**; flame graphs; ad-hoc LogsQL / MetricsQL
  query editors (all *stretch* — don't center the design on them).
- **Dashboards editor** is a **separate, later track** (Grafana-style 24-col grid, panel types
  `trace_list`/`log_view`/`metric_chart`/`markdown`). It is NOT part of the core trace→log MVP
  journey — design it only if explicitly asked, separately.
- Alerting, SLOs, deployment markers, anomaly detection/ML, multi-tenancy UI, OIDC/LDAP/SAML login,
  saved views / pinning / sharing, user accounts/profiles, audit log, cost/capacity, RUM,
  mobile-optimized layouts. **Log streaming is out** (500-cap).

## 9. Design tokens (authoritative — from `index.css`)

Design against these exact tokens (all OKLCH; light value shown, dark counterpart exists). Use the
Tailwind utility that maps to each (`bg-card`, `text-muted-foreground`, `bg-ok-soft text-ok-ink`, …).

| Group | Tokens |
|-------|--------|
| Surfaces | `--background` (page), `--card`, `--popover`, `--surface-2`, `--surface-3` (3 elevation layers) |
| Ink | `--foreground`, `--muted-foreground`, `--fg-2`, `--muted-2` (4 levels) |
| Borders | `--border`, `--border-2` |
| Primary (brand red) | `--primary` `oklch(0.505 0.213 27.518)` / dark `oklch(0.70 0.20 25)`, `--primary-foreground` |
| Accent / ring | `--accent`, `--ring` — **neutral gray** (do not use for the brand red) |
| Status | `--ok` `--err` `--warn` `--idle` `--slow`, each with `-soft` (bg) + `-ink` (label) |
| Log levels | `--level-{trace,debug,info,warn,error,fatal}` (+ `-bg`) |
| Service palette | `--svc-1…--svc-6` (categorical, hues 190–320, clear of status hues) |
| Waterfall | `--grid` (axis gridlines) |
| Radius | `--radius: 0.45rem` + `sm/md/lg/xl/2xl/3xl/4xl` multipliers |
| Fonts | `--font-sans` Geist · body Onest · `--font-mono` JetBrains Mono stack |

Hand-written APM CSS classes already exist in `index.css` and should be reused/extended, not
re-invented: `.waterfall`/`.span-row`/`.span-bar`, `.log-entry`/`.log-level`, `.duration`, `.time`,
`.trace-id`, `.pill`, `.dashboard-panel`, `.app-shell`/`.app-topnav`/`.app-sidebar`/`.app-main`.

## 10. Contract inconsistencies to lock (flag, then pick one)

These conflict across older docs; resolving them belongs to the FE↔BE contract (design-first
OpenAPI is the intended fix). For **design**, assume the right-hand choice:

1. **Route prefix** — use **`/api/v1/…`** (authoritative; `modules.md`'s `/api/traces` is stale).
2. **Param naming** — FE uses `startUnixMs`/`endUnixMs`/`traceId` (current convention).
3. **Trace detail + logs** — treat as **two calls** (trace, then logs): design the waterfall to
   render first and the log panel to fill in slightly after.
4. **Health codes** — design "degraded" as a distinct visual state (not just up/down).
5. **Service counts** — may be approximate/absent → the service card must **degrade gracefully**.
6. **Dashboards** — a separate track, not the core journey (see §8).

## 11. How to work with Claude design

- Design **within** §4 principles and §9 tokens; outputs should map cleanly to Tailwind utilities +
  existing primitives. New components get a Ladle story.
- Deliver: mockups for the §5 screens (all three states each), the split-view for Trace Detail, and
  light/dark. Desktop-first.
- Keep it an **operator tool** — density, mono numerics, keyboard affordances — not a marketing site.

---

## 12. First prompt for Claude design (copy-paste)

> I'm designing **Tessera** — a self-hosted **APM UI for the Victoria observability stack**
> (VictoriaTraces/Logs/Metrics), positioned as a **Grafana-analogue, multi-provider** front-end. Think
> **Kibana-APM / Jaeger**: an operator finds a trace → sees its span **waterfall** → reads the
> **correlated logs**. It's a dense, keyboard-friendly, **desktop-first** ops tool — not a marketing
> site.
>
> **Design system (already fixed — design *within* it, don't reinvent):**
> - Monochrome OKLCH surfaces (white → near-black), **one deep-red brand accent** (`oklch(0.505 0.213
>   27.518)`) for primary actions/links/focus. All other neutrals are gray.
> - **Status colors are the only other color, semantic only:** ok=green, err=red, warn=amber,
>   idle=gray, slow=amber-orange (latency breach); plus log-level colors trace/debug/info/warn/error/fatal.
> - Type: **Geist** (sans/headings), **Onest** (body), **JetBrains Mono** for all numerics
>   (durations, timestamps, counts, trace/span IDs) with tabular figures.
> - Brand mark: a **4-tile scatter** (3 black tiles + 1 red). Light + dark both first-class.
>
> **Design these screens** (state the data each has; cover loading / empty / error):
> 1. **App shell** — topnav (brand, global time-range picker, service selector, health indicator,
>    theme+language) + left sidebar (Traces, Logs, Services, Dashboards, Settings) + main.
> 2. **Trace Explorer** — dense table (Service·Operation, status pill, duration with slow tint, span
>    count, started); filters (service, status, min/max duration, time range); cursor pagination.
> 3. **Trace Detail (the hero screen)** — split view: a Jaeger-style **waterfall** of the span tree
>    (indented, offset+duration bars, time axis) + a **span detail panel** (status, service·operation,
>    span id, duration/self-time, tags, events with exceptions highlighted) + a **correlated logs**
>    panel (log rows with level chip + expandable structured fields, filterable to the selected span).
> 4. **Service Inventory** — card grid: service, error-rate badge, span/error counts, top operations.
> 5. **Logs** — list with level chips + expandable fields + trace-id filter.
> 6. **Settings** — Victoria endpoint URLs, admin token field, theme, language, health check (no login).
>
> **Out of scope (don't design):** service map, RED metric charts, flame graphs, dashboards editor,
> alerting, login/SSO, mobile-optimized layouts, log streaming.
>
> Deliver desktop-first mockups in **light and dark**, dense and information-first. Start with the
> **Trace Detail** hero screen, then the Trace Explorer and App shell.
