# Tessera playbook — Ladle component catalog

Component catalog (`@tessera/playbook`) — fast Vite-powered playground
for `@tessera/console` primitives and APM-specific components. Built on
Ladle v5 (per-project workspace).

## Layout

```
web/playbook/
├── package.json            workspace entry (@tessera/playbook)
├── tsconfig.json           extends web/tsconfig.base.json; @/* → apps/console/src
├── .ladle/
│   └── config.mjs          Ladle config (stories glob, port, viteConfig path)
├── ladle-vite.config.ts    Vite config: tsconfig-paths plugin + Tailwind
├── src/
│   ├── styles.css          re-exports console index.css (Tessera DS tokens)
│   └── stories/
│       ├── primitives/     12 shadcn-based primitives
│       └── apm/            9 APM-specific components
└── README.md               this file
```

Stories import production components directly via the `@/*` alias
pointing to `apps/console/src/*` — no duplicate copies of components.
Ladle resolves the alias via `vite-tsconfig-paths` reading
`apps/console/tsconfig.json`.

## Usage

```bash
cd web
bun install                         # one-time
bun run playbook:dev                # start Ladle on http://127.0.0.1:2006
bun run --filter @tessera/playbook build  # static build → dist/
```

Authoring a new story: create `web/playbook/src/stories/<group>/<name>.stories.tsx`.

```tsx
import type { Story, StoryDefault } from '@ladle/react';
import { MyComponent } from '@/shared/ui/path/to/component';

export default {
  title: 'Group / MyComponent',
} satisfies StoryDefault;

export const Basic: Story = () => <MyComponent />;
```

## Port

**2006** in the project port pool (1990–2120 per
`.agents/rules/coding/project-ports.md`). Console dev uses 1991; Tessera
backend HTTP uses 1990. `2006` was picked because 1992–1999 are reserved
for internal services and 2000–2020 is the external dev tooling bucket.

## Working state (2026-07-19)

- `bun run typecheck` ✅ — both `@tessera/console` and `@tessera/playbook` typecheck clean.
- `bun run --filter @tessera/playbook build` ✅ — 22 story chunks + Ladle UI bundle, 0.79 MiB total.
- `bun run --filter @tessera/console build` ✅ — 190 modules, 366 KB main bundle.
- 22 stories shipped (12 primitives + 8 APM + 2 default meta chunks).

## Two upstream patches required

These are untracked edits under `web/node_modules/.bun/@ladle+react@5.1.1/.../`
that need to be reapplied after every `bun install`. Both are upstream bugs
that should eventually be fixed in `@ladle/react` itself.

### 1. Plugin name detection (Ladle v5.1.1)

In `@ladle/react/lib/cli/get-user-vite-config.js`, the `hasTSConfigPathPlugin`
detection checks for `"vite:tsconfig-paths"` (with colon), but the published
plugin instance exports `"vite-tsconfig-paths"` (with hyphen). Without this
patch, Ladle always adds its own copy of the plugin, which then resolves
paths from the wrong project root and breaks our `@/*` alias.

Patch: replace `"vite:tsconfig-paths"` with `"vite-tsconfig-paths"`.

### 2. (None currently — removed during cleanup)

A previous debug patch in `vite-base.js` was used to inspect the merged
user config; it's no longer needed and has been restored to clean state.

## Story catalog

### Primitives (12)

- `badge`, `button`, `card`, `dialog`, `popover`, `select`, `separator`,
  `skeleton`, `sonner` (toast), `switch`, `table`, `tabs`, `tooltip`

### APM-specific (9)

- `dashboard-grid`, `duration`, `log-entry`, `log-level`, `span-row`,
  `status-pill`, `time-format`, `trace-id`, `waterfall`

## Pre-existing console-side fixes (touched in this PR)

These were needed to make the build green. Listed here so future work
knows they exist; pre-existing drift per `process/build-verification.md`
("Pre-existing drift — отдельная задача"):

1. `web/apps/console/src/shared/ui/app-shell/nav-config.ts` → renamed
   to `.tsx` (JSX in `.ts` files doesn't parse).
2. `web/apps/console/src/lib/utils.ts` — added Plexor shim
   (re-exports `cn` from `@/shared/lib/utils`); satisfies
   `import { cn } from "@/lib/utils"` used by 84 primitives.
3. `web/apps/console/src/shared/ui/tech-icon-data.ts` — copied Plexor's
   generated file (regenerated from `scripts/gen-tech-icons.cjs`).
4. `web/apps/console/src/index.css` — added `@theme inline` block
   mapping design tokens to Tailwind utilities (`bg-primary`, `border-border`,
   etc.). Without this, `vite build` fails with "Cannot apply unknown
   utility class `border-border`".
5. Console `package.json` — added `@nine-thirty-five/material-symbols-react`,
   `@shadcn/react`, `@iconify/react`, `@iconify-json/logos` (Plexor's
   icon packages; were lost during the initial copy).
6. Minor unused-import cleanup in `services-page.tsx`, `mock-data.ts`,
   `time-format.test.tsx`, `app-shell.tsx`, `preferences-dialog.tsx`.