# Tessera playbook — Ladle component catalog

Live React playground for `@tessera/console` primitives and APM-specific
components. Ladle runs against the console's own Vite config so Tailwind
tokens, design system, and the `@/...` alias all work out of the box.

## Layout

```
web/apps/console/
├── .ladle/
│   ├── config.mjs          Ladle config: stories glob, vite config path, port
│   └── README.md           this file
├── stories/                Ladle stories (separate from src/)
│   ├── primitives/         shadcn primitives + custom
│   └── apm/                APM-specific components
├── src/
│   ├── index.css           Tessera design tokens + Tailwind @theme block
│   └── shared/ui/...       components the stories import from
└── vite.config.ts          shared Vite config (console + Ladle both use it)
```

## Usage

```bash
cd web/apps/console
bun install                  # one-time, picks up @ladle/react
bun run playbook:dev         # http://127.0.0.1:2006 (live playground)
bun run playbook:build       # static build → dist-playbook/
bun run playbook:preview     # serve the build
```

## Why stories live at `stories/` (not `src/stories/`)

Ladle's `--config <folder>` flag takes a directory, default `.ladle`. It
then runs `globby` on the `stories` glob relative to the **project root**
(`process.cwd()`), NOT relative to where the config lives. Stories must
be at `<cwd>/stories/` for the default glob `stories/**` to match.

The default globby config also skips dotfile directories (`**/.ladle/**`),
so `.ladle/stories/` doesn't work without a custom glob — and our config
tries to keep the config as close to the default as possible.

## Why stories import `@/index.css`

Ladle's Vite root is its own bundled app dir, not `apps/console/`. Relative
imports like `../src/index.css` resolve incorrectly relative to Ladle's
internal Vite root. The `@/` alias (defined in console's `tsconfig.json`)
always resolves correctly to console's source tree.

The `@tailwindcss/vite` plugin in console's `vite.config.ts` then picks
up the CSS during the Ladle build, scanning console's `src/` for utility
classes used by both the app and the stories.

## Port

**2006** in the project port pool (1990–2120 per
`.agents/rules/coding/project-ports.md`). Console dev uses 1991;
Tessera backend HTTP uses 1990.

## Story catalog (22 stories)

### Primitives (13)

`badge`, `button`, `card`, `dialog`, `popover`, `select`, `separator`,
`skeleton`, `sonner` (toast), `switch`, `table`, `tabs`, `tooltip`

### APM-specific (9)

`dashboard-grid`, `duration`, `log-entry`, `log-level`, `span-row`,
`status-pill`, `time-format`, `trace-id`, `waterfall`

## Working state (verified)

- `bun run typecheck` ✅ — both console + stories typecheck clean
- `bun run build` ✅ — console → `dist/`, 366 KB main bundle
- `bun run playbook:build` ✅ — 22 story chunks, 187 KB CSS, 6.37 MiB total
- `bun run playbook:dev` — runs locally (port 2006)