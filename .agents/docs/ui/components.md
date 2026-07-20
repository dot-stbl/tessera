# Tessera components catalog

> ⚠️ **PARTIALLY OBSOLETE (2026-07-21).** The HTML / CSS-class snippets below reflect an earlier static-catalog plan. Real components are **React (Base UI + shadcn wrappers)** under `web/apps/console/src/shared/ui/{primitives,apm}`, catalogued in **Ladle** (not `web/playbook/`). The component *inventory* and *behavior notes* here remain a useful reference; the markup is illustrative, not the implementation. See `DESIGN.md` (repo root).

Catalog of components specific to tessera. Plexor components inherited as-is
(see `design-system.md`).

## Reused from plexor (no changes)

| Component | Plexor class | Used by |
|-----------|--------------|---------|
| Button | `.btn`, `.btn.lg`, `.btn.sm`, `.btn.ghost` | All action buttons |
| Input | `.input`, `.input.is-search` | Time range, search fields |
| Pill | `.pill`, `.pill.ok/err/warn` | Status indicators |
| Table | `.tbl` | Trace list, log viewer, service list |
| Toolbar | `.toolbar` | Filter bars (above tables) |
| Topnav | `.topnav` | App chrome |
| Sidebar | `.sidebar` | Section navigation (adapted for APM sections) |
| Breadcrumb | `.bc-row`, `.bc`, `.bc-item` | Page hierarchy |
| Combobox | (TBD) | Service/operation selector |
| Drawer | (TBD) | Settings, dashboard editor side panel |
| Dialog | (TBD) | Confirm destructive actions |

## New for tessera (APM-specific)

### 1. Log level chip

Compact pill showing log severity (TRACE/DEBUG/INFO/WARN/ERROR/FATAL).

```html
<span class="log-level log-level-error">ERROR</span>
<span class="log-level log-level-warn">WARN</span>
<span class="log-level log-level-info">INFO</span>
<span class="log-level log-level-debug">DEBUG</span>
<span class="log-level log-level-trace">TRACE</span>
<span class="log-level log-level-fatal">FATAL</span>
```

**Used by:** log viewer (every log line), structured fields panel.

### 2. APM status pill

Like plexor's pill but with APM-specific states.

```html
<span class="pill pill-ok"><span class="dot"></span>OK</span>
<span class="pill pill-error"><span class="dot"></span>ERROR</span>
<span class="pill pill-warn"><span class="dot"></span>SLOW</span>
<span class="pill pill-slow"><span class="dot"></span>1.2s</span>
<span class="pill pill-idle"><span class="dot"></span>UNSET</span>
```

**Used by:** trace list (status column), trace detail (per-span status), health
endpoint.

### 3. Duration formatter

Auto-scaling compact duration display.

```html
<span class="duration">1247<span class="duration-unit">ms</span></span>
<span class="duration is-slow">2.4<span class="duration-unit">s</span></span>
<span class="duration is-very-slow">1.5<span class="duration-unit">m</span></span>
```

**Format rules:**
- `< 1µs`: nanoseconds (`ns`)
- `< 1ms`: microseconds (`µs`)
- `< 1s`: milliseconds (`ms`)
- `< 1m`: seconds (`s`)
- otherwise: minutes (`m`) or hours (`h`)

**Threshold highlighting:**
- `is-slow`: > 1s (yellow tint)
- `is-very-slow`: > 5s (red tint)

### 4. Time formatter

```html
<span class="time is-absolute">2026-07-19T14:08:21.412Z</span>
<span class="time is-relative">2 min ago</span>
```

**Used by:** trace list (start time), log viewer (timestamp), span detail.

### 5. Trace ID display

```html
<code class="trace-id">abc123def456…</code>
```

**Behavior:** truncated to 12 chars + ellipsis. Full ID in `title` attribute
(tooltip). Click copies full ID to clipboard.

### 6. Service/Operation dropdown

Searchable combobox for selecting service or operation from VT's `/services`
endpoint.

```html
<div class="combobox" data-combobox>
  <input class="input" placeholder="Service" />
  <div class="combobox-menu">
    <button class="combobox-item">checkout-api <span class="muted">4210 spans</span></button>
    <button class="combobox-item">postgres <span class="muted">2100 spans</span></button>
    <!-- ... -->
  </div>
</div>
```

**Used by:** trace list filter, dashboard variable, log viewer filter.

### 7. Time range picker

Dropdown with presets + custom range.

```html
<div class="time-range-picker">
  <button class="btn">Last 1 hour</button>
  <div class="time-range-menu">
    <button class="time-range-item">Last 15 minutes</button>
    <button class="time-range-item is-selected">Last 1 hour</button>
    <button class="time-range-item">Last 6 hours</button>
    <button class="time-range-item">Last 24 hours</button>
    <button class="time-range-item">Last 7 days</button>
    <hr class="time-range-divider" />
    <button class="time-range-item">Custom range…</button>
  </div>
</div>
```

**Used by:** every page (global time range).

### 8. Variable input

Template variable selector (Grafana-style `$variable`).

```html
<div class="variable-input">
  <label class="variable-label">Service</label>
  <button class="variable-control">
    <span class="variable-current">$service</span>
    <span class="variable-value">checkout-api</span>
    <span class="variable-caret">▾</span>
  </button>
</div>
```

**Used by:** dashboard panel titles, query inputs.

### 9. Trace waterfall

Timeline visualization of parent/child spans (Jaeger-style).

```
┌──────────────────────────────────────────────────────────────────────┐
│ checkout-api · POST /checkout                              1247ms ←  │
│ ├─ postgres · SELECT orders                                50ms       │
│ │  └─ postgres · SELECT order_items                        23ms       │
│ └─ stripe · Charge                                         1180ms ←   │
│    └─ stripe · Refund (idempotent)                          12ms      │
└──────────────────────────────────────────────────────────────────────┘
```

**Components:**
- `Waterfall` — root container, vertical scrollable
- `SpanRow` — single span with offset bar + duration bar
- `SpanDetailPanel` — right-side panel showing span tags + events

### 10. Span row

```html
<div class="span-row" data-depth="0">
  <div class="span-bar" style="--offset: 0%; --duration: 95%;">
    <span class="span-name">checkout-api · POST /checkout</span>
    <span class="span-duration">1247ms</span>
  </div>
</div>
<div class="span-row" data-depth="1">
  <div class="span-bar" style="--offset: 0.5%; --duration: 4%;" data-status="ok">
    <span class="span-name">postgres · SELECT orders</span>
    <span class="span-duration">50ms</span>
  </div>
</div>
```

**Behavior:** click row → open SpanDetailPanel. Status colors:
- `data-status="ok"` — `--ok-soft` background
- `data-status="error"` — `--err-soft` background + indicator
- `data-status="unset"` — `--idle-soft` background

### 11. Log entry

```html
<div class="log-entry">
  <span class="log-time">14:08:21.412</span>
  <span class="log-level log-level-error">ERROR</span>
  <span class="log-service">checkout-api</span>
  <span class="log-message">failed to charge card</span>
  <button class="log-expand">▼</button>
  <div class="log-fields hidden">
    <dl class="kv">
      <dt>trace.id</dt><dd>abc123def456</dd>
      <dt>span.id</dt><dd>span_001</dd>
      <dt>error.kind</dt><dd>StripeError</dd>
      <dt>user.id</dt><dd>u_42</dd>
    </dl>
  </div>
</div>
```

**Behavior:** click expand → show structured fields in KV table (plexor's
`.kv` component, reused).

### 12. Dashboard grid + panel

24-column grid layout, drag-resize panels (like Grafana).

```html
<div class="dashboard-grid">
  <div class="panel panel-trace-list" data-grid-pos="0,0,24,8">
    <div class="panel-header">
      <h3 class="panel-title">Recent traces for $service</h3>
      <button class="panel-menu">⋮</button>
    </div>
    <div class="panel-body">
      <!-- trace_list renderer -->
    </div>
  </div>

  <div class="panel panel-log-view" data-grid-pos="0,8,12,8">
    <div class="panel-header">
      <h3 class="panel-title">Error logs</h3>
    </div>
    <div class="panel-body">
      <!-- log_view renderer -->
    </div>
  </div>
</div>
```

**Panel types in MVP:**
- `trace_list` — table of traces
- `log_view` — log entries
- `metric_chart` — time series chart (visx)
- `markdown` — rendered text

## Implementation

| Component | Tessera module | Frontend location |
|-----------|----------------|-------------------|
| All chrome (topnav, sidebar, etc.) | — | `web/apps/console/components/chrome/` |
| Log level chip | `Tessera.Modules.Logs` | `web/apps/console/components/log/` |
| Trace waterfall | `Tessera.Modules.Traces` | `web/apps/console/components/waterfall/` |
| Span row / detail | `Tessera.Modules.Traces` | same |
| Status pill (APM) | shared | `web/apps/console/components/pill/` |
| Dashboard panel | `Tessera.Modules.Dashboards` | `web/apps/console/components/panel/` |
| Time range picker | shared | `web/apps/console/components/time-range/` |

## Related docs

- `design-system.md` — design tokens, APM extensions
- `playbook.md` — playbook directory structure
- `../../docs/dashboard-schema.md` — dashboard JSON schema (panels)