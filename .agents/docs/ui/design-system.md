# Tessera design system

> ⚠️ **DRIFT NOTICE (2026-07-21).** This doc predates the real implementation and is kept as *design intent*, not ground truth.
> - **Authoritative tokens:** `web/apps/console/src/index.css` (Tailwind 4 + OKLCH, shadcn slot names). There is **no** `web/playbook/` static site.
> - **Live catalog:** Ladle — `bun run playbook:dev` (port 2006), stories under `apps/console/stories/**`.
> - **Corrections:** the deep-red brand color is **`--primary`** (light `oklch(0.505 0.213 27.518)`), **not `--accent`** (which is neutral gray, like `--ring`). `--font-sans` is **Geist** (body text uses Onest); mono is a JetBrains Mono stack. Spacing/type come from **Tailwind utilities**, not `--s-*`/`--fs-*`. Brand mark is a **4-tile scatter** (3 black + 1 red `#A60000`).
> - **Consolidated FE brief:** `DESIGN.md` (repo root).
> The APM token values (ok/err/warn/slow/level-*) below still match `index.css`.

Tessera inherits the **plexor design system** as foundation (typography, spacing,
layout chrome) and extends it with **APM-specific primitives**. **Accent color is
distinctly tessera** — deep red for primary actions, following the `.stbl` brand
spirit ("pure B&W + monospace") but with a strong brand color for focus.

## 0. Design direction (locked 2026-07-19)

- **Surfaces** — pure B&W (OKLCH grayscale, no hue). Light mode = white, dark mode = near-black.
- **Accent** — deep red for primary actions (buttons, links, focus rings). NOT a color hue
  on surfaces — surfaces stay monochrome.
- **Status semantics** — only colors that survive:
  - `--ok` (green) — success, healthy, running
  - `--err` (bright red) — error, failed, exception (distinct from --accent which is deep red)
  - `--warn` (amber) — warning, paused, pending
  - `--idle` (gray) — neutral, disabled
  - `--slow` (amber) — latency threshold breach
  - `--level-{trace,debug,info,warn,error,fatal}` — log severity
- **Ink** — black/white scale (4 levels)
- **Borders** — subtle gray scale (2 levels)
- **Typography** — Onest (sans) + JetBrains Mono (numerics), per plexor
- **Spacing** — 4-base (2–32px), per plexor
- **Radii** — 6/8/12px scale, per plexor
- **Status semantics colors are the ONLY colored values** — never use them for
  decoration. They mean what they say.

## 1. Inheritance from plexor

Carried over (no adaptation needed):

| Token group | Source |
|-------------|--------|
| Typography (Onest + JetBrains Mono) | plexor `--font-sans`, `--font-mono` |
| Surface scale (bg, surface, surface-2, surface-3) | plexor `--bg`, `--surface`, ... |
| Ink scale (fg, fg-2, muted, muted-2) | plexor `--fg`, ... |
| Border scale (border, border-2) | plexor `--border`, `--border-2` |
| Spacing scale (4-base) | plexor `--s-1` ... `--s-8` |
| Radii (sm, base, lg, pill) | plexor `--radius-sm`, `--radius`, ... |
| Layout chrome (topnav-h, sidebar-w, bc-row-h, row-h) | plexor `--topnav-h`, ... |
| Dark mode (data-theme="dark") | plexor `:root[data-theme="dark"]` block |
| Components (buttons, inputs, pills, tabs, table, toolbar, drawer, ...) | plexor `.btn`, `.input`, `.pill`, ... |

## 2. Tessera-specific tokens

### 2.1 Accent (deep red, NOT blue)

```css
:root {
  /* Accent — primary actions: buttons, links, focus rings */
  --accent: oklch(35% 0.18 25);          /* dark red, light mode */
  --accent-foreground: oklch(100% 0 0); /* white text on red */
  --ring: var(--accent);
}
.dark {
  --accent: oklch(70% 0.20 25);          /* lighter red in dark mode */
  --accent-foreground: oklch(17% 0.012 250); /* dark text on lighter red */
}
```

**Why red, not blue?** `.stbl` brand is "pure B&W · monospace only". Plexor
deviates with subtle blue accent — tolerated because open source utility
software. Tessera goes a step further with a strong red accent that
distinguishes it from plexor while still respecting B&W surface rule.

**Accent vs --err**: visually distinct by lightness + chroma:
- `--accent`: `oklch(35% 0.18 25)` — **dark, deep** (primary action)
- `--err`: `oklch(58% 0.20 25)` — **bright, vivid** (status semantic)

In dark mode accent brightens; --err stays bright. Always distinguishable.

## 3. APM-specific extensions

### 3.1 APM status (extends plexor's ok/err/warn/idle)

```css
:root {
  /* Reused from plexor */
  --ok:        oklch(58% 0.16 145);   /* success, running, healthy */
  --err:       oklch(58% 0.20 25);    /* error, failed */
  --warn:      oklch(72% 0.15 75);    /* warning, paused */

  /* New for APM */
  --slow:      oklch(68% 0.18 50);    /* slow operation, latency threshold breach */
  --slow-soft: oklch(96% 0.04 50);
  --slow-ink:  oklch(45% 0.12 50);

  /* Trace status (Jaeger convention) */
  --trace-ok:   var(--ok);
  --trace-err:  var(--err);
  --trace-unset: var(--idle);
}
```

**Status mapping:**

| Concept | Plexor token | Tessera usage |
|---------|--------------|---------------|
| `ok` (success, healthy) | `--ok` | trace status, span status, health check |
| `err` (error, failed) | `--err` | error trace, exception span, 5xx response |
| `warn` (paused, pending) | `--warn` | degraded health, slow query |
| `slow` (latency > threshold) | `--slow` (NEW) | p95 latency breach, trace > 1s |
| `idle` (neutral) | `--idle` | unset status, disabled, draft |

### 3.2 Log levels (Victoria Logs / OpenTelemetry)

```css
:root {
  /* Log severity colors — soft for backgrounds, ink for labels */
  --level-trace:    oklch(60% 0.005 240);     /* gray */
  --level-debug:    oklch(60% 0.05 220);      /* muted blue */
  --level-info:     oklch(58% 0.10 230);      /* blue */
  --level-warn:     oklch(72% 0.15 75);       /* amber (= --warn) */
  --level-error:    oklch(58% 0.20 25);       /* red (= --err) */
  --level-fatal:    oklch(45% 0.22 0);        /* deep red */

  --level-trace-bg:  oklch(96% 0.005 240);
  --level-debug-bg:  oklch(96% 0.03 220);
  --level-info-bg:   oklch(96% 0.04 230);
  --level-warn-bg:   oklch(96% 0.05 75);
  --level-error-bg:  oklch(96% 0.05 25);
  --level-fatal-bg:  oklch(95% 0.05 0);
}
```

**Usage in log viewer:**

```html
<span class="log-level log-level-error">ERROR</span>
<span class="log-level log-level-warn">WARN</span>
```

### 3.3 Duration formatting

APM data has many duration values (span duration, request latency, etc.).
Compact format: `<value> <unit>` with auto-scaling.

```css
.duration {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
}

.duration.is-slow { color: var(--slow-ink); background: var(--slow-soft); }
.duration.is-very-slow { color: var(--err-ink); background: var(--err-soft); }
```

**Format:** `100ns`, `2.4µs`, `1.2ms`, `500ms`, `1.5s`, `2m`, `1h` (auto-scale).

### 3.4 Time format

```css
.time {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  color: var(--fg-2);
}

.time.is-relative { color: var(--muted); }
```

**Display modes:**
- Absolute: `2026-07-19T14:08:21.412Z` (RFC3339, monospace)
- Relative: `2 min ago` (muted color)
- Hover: tooltip with absolute

### 3.5 Trace ID / Span ID — abbreviated monospace

```css
.trace-id, .span-id {
  font-family: var(--font-mono);
  font-size: var(--fs-12);
  color: var(--muted);
}

.trace-id:hover { color: var(--fg); text-decoration: underline dotted; }
```

**Format:** `abc123def456...` (16-char hex prefix + ellipsis).

## 4. APM status pills (log viewer + trace viewer)

```html
<span class="pill pill-error">
  <span class="dot"></span> ERROR
</span>
<span class="pill pill-warn">
  <span class="dot"></span> SLOW (1.2s)
</span>
<span class="pill pill-ok">
  <span class="dot"></span> OK
</span>
```

CSS (extension of plexor's `.pill`):

```css
.pill.pill-error { background: var(--err-soft); color: var(--err-ink); }
.pill.pill-warn { background: var(--warn-soft); color: var(--warn-ink); }
.pill.pill-ok { background: var(--ok-soft); color: var(--ok-ink); }
.pill.pill-slow { background: var(--slow-soft); color: var(--slow-ink); }
.pill.pill-idle { background: var(--idle-soft); color: var(--idle-ink); }
```

## 5. Theme: light + dark

Inherited from plexor — same `data-theme="dark"` attribute, same token
inversion pattern. Tessera APM extensions (slow, level-*) also need dark-mode
counterparts:

```css
:root[data-theme="dark"] {
  --level-trace-bg:  oklch(22% 0.005 240);
  --level-debug-bg:  oklch(22% 0.02 220);
  --level-info-bg:   oklch(22% 0.025 230);
  --level-warn-bg:   oklch(25% 0.04 75);
  --level-error-bg:  oklch(25% 0.04 25);
  --level-fatal-bg:  oklch(25% 0.05 0);
}
```

## 6. Implementation status

**MVP scope:**
- [ ] Copy plexor `styles.css` → `web/playbook/styles.css`
- [ ] Add APM extensions (status, log levels, duration, time)
- [ ] Create `web/playbook/index.html` — main catalog page
- [ ] Build component examples for: button, input, pill, table, toolbar, status pill, log level, duration

**Stretch:**
- [ ] Live playground (toggle knobs for variants)
- [ ] Dark/light toggle in catalog
- [ ] Code snippet export

## Related docs

- `playbook.md` — playbook directory structure, how to use
- `components.md` — catalog of tessera-specific components
- Plexor source: `C:\Users\bradw\source\stbl\plexor\.agents\docs\design\styles.css`