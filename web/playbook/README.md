# Tessera playbook — Ladle component catalog

Component catalog (`@tessera/playbook`) — fast Vite-powered playground
for `@tessera/console` primitives and APM-specific components. Heavily
inspired by [Plexor's static-HTML playbook](../.gitignore) but built
on Ladle so we get live React controls + MDX docs without giving up the
"plain CSS" feel.

## Layout

```
web/playbook/
├── package.json            workspace entry (@tessera/playbook)
├── tsconfig.json           extends web/tsconfig.base.json
├── ladle.config.ts         Ladle config: stories glob, viteConfig path
├── ladle-vite.config.ts    Vite config shared with console
├── src/
│   ├── styles.css          global tokens (re-exports console index.css)
│   └── stories/
│       ├── primitives/     shadcn-based components
│       └── apm/            APM-specific components (status pill, etc.)
└── README.md               this file
```

Stories import production components directly via the `@/*` alias
pointing to `apps/console/src/*` — no duplicate copies of components.

## Usage

```bash
cd web
bun install              # install @tessera/playbook + dependencies
bun run playbook:dev     # start Ladle on http://127.0.0.1:2006
```

Authoring a new story: create `web/playbook/src/stories/<group>/<name>.stories.tsx`.

```tsx
import type { Story, StoryDefault } from '@ladle/react';
import { Component } from '@/shared/ui/path/to/component';

export default {
  title: 'Group / Component',
  meta: { iframed: false },
} satisfies StoryDefault;

export const Default: Story = () => <Component />;
```

`tsc --noEmit` (via `bun run typecheck`) validates all stories.

## Port

**2006** in the project port pool (1990–2120 per
`.agents/rules/coding/project-ports.md`). Console dev uses 1991; Tessera
backend HTTP uses 1990. `2006` was picked because 1992–1999 are reserved
for internal services and 2000–2020 is the external dev tooling bucket.

## Known issues

### Ladle v5.1.1 user-config merge bug — bundle build broken

**Symptom:** `bun run playbook:dev` or `bun run build` fails with
"Rollup failed to resolve import `@/shared/ui/...`". Typecheck
(`bun run typecheck`) passes.

**Root cause:** Ladle v5.1.1's `vite-base.js` deep-merges user vite
config with Ladle's defaults. The user `resolve.alias` for `@/` is
silently dropped during the merge — only Ladle's own `msw/browser`,
`msw`, `axe-core` aliases survive. Ladle's `vite-tsconfig-paths` plugin
looks for a plugin named `"vite:tsconfig-paths"` (colon) to skip
re-adding, but the actual published plugin instance uses
`"vite-tsconfig-paths"` (hyphen), so Ladle always adds its own.

**Workaround (current state):** `bun run typecheck` passes; Ladle
runtime is deferred until Ladle upstream fixes the plugin name
detection, OR we replace the merge with a custom CLI hook.

**Tracking:** open issue + reference in this README. Do not enable
`bun run playbook:dev` until resolved.

### Pre-existing console-side bugs that block Ladle even when fixed

These are documented separately in `.agents/STATE.md` and surfaced in
the commit message for this change. They were *not* in scope for the
Ladle task; flagged here so future work knows they exist:

1. `web/apps/console/src/shared/ui/app-shell/nav-config.ts` — JSX in a
   `.ts` file (renamed to `.tsx` in this PR as a minimum-impact fix).
2. `web/apps/console/src/lib/utils.ts` — Plexor shim missing
   (added in this PR as a re-export of `@/shared/lib/utils`).
3. `web/apps/console/src/types/deps.d.ts` — module shim for
   `@nine-thirty-five/material-symbols-react` subpath imports +
   `@iconify/react` + `@shadcn/react/*` + `@/shared/ui/tech-icon-data`
   that Plexor uses but are not in our `package.json` (added in this
   PR; runtime resolution still `undefined`).
4. Tailwind 4 + shadcn: `@apply border-border` in `index.css` breaks
   `vite build` of console. Pre-existing.
5. Console typecheck has 7 unused-import errors (TS6133/TS6196/TS2709)
   across `mock-data.ts`, `services-page.tsx`, `time-format.test.tsx`,
   `app-shell.tsx`, `preferences-dialog.tsx`, `empty-state.tsx`.
   Pre-existing. None block Ladle build, only `tsc --noEmit`.
