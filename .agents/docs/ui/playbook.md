# Tessera playbook

> **Path:** `web/playbook/` (under `web/`). **Live:** `web/playbook/index.html`.

The playbook is the **single source of truth** for tessera's visual design.
It's a static HTML site that any developer can open in a browser to see
every component, its variants, and how to use it.

## Directory structure

```
web/playbook/
├── README.md                       # this file (well, .md version)
├── index.html                      # main catalog page (TOC + sections)
├── styles.css                      # design tokens + base styles
├── components/
│   ├── buttons.html                # all button variants
│   ├── inputs.html                 # input fields
│   ├── pills.html                  # status pills
│   ├── tables.html                 # table variants
│   ├── toolbars.html               # toolbar patterns
│   ├── status.html                 # APM status pills (NEW)
│   ├── log-level.html              # log severity chips (NEW)
│   ├── duration.html               # duration formatter (NEW)
│   ├── time-range.html             # time range picker (NEW)
│   ├── service-dropdown.html       # searchable combobox (NEW)
│   ├── variable-input.html         # template variables (NEW)
│   ├── waterfall.html              # trace timeline (NEW)
│   ├── span-row.html               # single span in waterfall (NEW)
│   ├── log-entry.html              # log line with structured fields (NEW)
│   ├── dashboard-grid.html         # 24-col dashboard layout (NEW)
│   └── chart-panel.html            # time series wrapper (NEW)
└── patterns/
    ├── page-trace-list.html        # full trace list page example
    ├── page-trace-detail.html      # full trace detail (waterfall + log panel)
    ├── page-dashboard.html         # full dashboard with multiple panels
    └── page-settings.html          # settings page
```

## How to use

1. **Browse:** open `web/playbook/index.html` in browser
2. **Find component:** click TOC entry or scroll to section
3. **Copy HTML:** use the example markup directly in `web/apps/console/`
4. **Theme:** click theme toggle in topnav to switch light/dark

## How to add a new component

1. Create `web/playbook/components/<component-name>.html` with examples
2. Add TOC entry in `web/playbook/index.html`
3. Add CSS in `web/playbook/styles.css` (or component-scoped `<style>` block)
4. Cross-reference from `docs/ui/components.md`

## Relationship to actual app

The playbook serves **two purposes**:

1. **Visual reference** — designers + devs see what components look like
2. **Implementation source** — copy HTML/CSS into `web/apps/console/`

**Important:** the playbook is **not** built into the app. It's standalone
HTML for design review. The actual app code lives in `web/apps/console/`.

## Implementation plan

**Phase 1 (foundation):**
- [ ] Copy plexor `styles.css` → `web/playbook/styles.css` (adapt Plexor → Tessera)
- [ ] Build `index.html` skeleton (TOC, topnav, dark/light toggle)
- [ ] Port plexor components: buttons, inputs, pills, tables, toolbar

**Phase 2 (APM extensions):**
- [ ] Add APM status pills (ok/error/slow/idle)
- [ ] Add log level chips (TRACE/DEBUG/INFO/WARN/ERROR/FATAL)
- [ ] Add duration formatter
- [ ] Add time formatter
- [ ] Add time range picker
- [ ] Add service/operation dropdown

**Phase 3 (APM-specific):**
- [ ] Build trace waterfall (Jaeger-style)
- [ ] Build span row + detail panel
- [ ] Build log entry with structured fields
- [ ] Build dashboard grid + panel types

**Phase 4 (full pages):**
- [ ] Page examples: trace list, trace detail, dashboard, settings

## Code examples

### Minimal playbook page

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Component name · Tessera playbook</title>
  <link rel="stylesheet" href="../styles.css" />
  <style>
    /* Component-scoped styles */
    .demo-component {
      padding: var(--s-4);
      border: 1px solid var(--border);
      border-radius: var(--radius);
    }
  </style>
</head>
<body>
  <header class="topnav">
    <a href="../index.html" class="brand">
      <span class="brand-name">Tessera</span>
    </a>
    <span class="brand-tag">playbook</span>
    <span class="topnav-spacer"></span>
    <div class="topnav-right">
      <button class="btn ghost" data-theme-toggle>Toggle theme</button>
    </div>
  </header>

  <main class="page">
    <div class="page-header">
      <h1>Component name</h1>
      <div class="page-subtitle">Description of when to use this component</div>
    </div>

    <div class="page-body">
      <div class="panel">
        <h3>Variants</h3>

        <!-- Primary variant -->
        <div class="demo">
          <div class="demo-cap">Primary</div>
          <button class="btn primary">Click me</button>
        </div>

        <!-- Secondary variant -->
        <div class="demo">
          <div class="demo-cap">Secondary</div>
          <button class="btn">Click me</button>
        </div>

        <!-- Disabled state -->
        <div class="demo">
          <div class="demo-cap">Disabled</div>
          <button class="btn" disabled>Click me</button>
        </div>
      </div>
    </div>
  </main>

  <script src="../app.js"></script>
</body>
</html>
```

### Theme toggle

```javascript
// web/playbook/app.js
(function () {
  // Apply theme from localStorage or system preference
  try {
    var t = localStorage.getItem('tessera-theme');
    var dark = t ? t === 'dark' :
      (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
    if (dark) document.documentElement.setAttribute('data-theme', 'dark');
  } catch (e) {}

  // Theme toggle button
  document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var isDark = document.documentElement.getAttribute('data-theme') === 'dark';
      if (isDark) {
        document.documentElement.removeAttribute('data-theme');
        try { localStorage.setItem('tessera-theme', 'light'); } catch (e) {}
      } else {
        document.documentElement.setAttribute('data-theme', 'dark');
        try { localStorage.setItem('tessera-theme', 'dark'); } catch (e) {}
      }
    });
  });
})();
```

## What gets copied from plexor

Already in plexor `.agents/docs/design/`:
- `styles.css` (876 lines) → copy to `tessera/web/playbook/styles.css`
- `app.js` (theme toggle, combobox, etc.) → adapt for tessera
- `design-system.html` (1008 lines) → use as inspiration for `index.html`

Not copied:
- `screens/*.html` — plexor-specific pages (VM list, audit log, etc.)
- `index.html` (Plexor launcher) — replace with tessera-specific entry

## Related docs

- `design-system.md` — design tokens
- `components.md` — component catalog
- Plexor source: `C:\Users\bradw\source\stbl\plexor\.agents\docs\design\`