# Tessera Web — UI monorepo

Frontend for Tessera APM UI. bun workspaces + Vite + React + TanStack.

## Layout

```
web/
├── apps/
│   └── console/          # Tessera Console (Vite SPA) — @tessera/console
├── playbook/             # Static HTML design system catalog
├── shared/               # (planned) @tessera/ui, @tessera/lib, @tessera/api
└── tooling/
    └── eslint-config/    # @tessera/eslint-config
```

## Setup

```bash
cd web
bun install
bun run dev
```

## Stack

- **React 19** + **Vite 6**
- **TanStack Router** (file-based) + **TanStack Query** + **TanStack Table**
- **shadcn/ui** on Base UI + Radix primitives
- **Tailwind CSS 4** + tw-animate-css
- **recharts** for dashboard charts
- **react-hook-form** + **zod** for forms
- **cmdk** for command palette
- **i18next** for i18n
- **@hugeicons/react** + Phosphor (iconLibrary)
- **@fontsource-variable/onest** + Inter

## What was carried over from plexor

This web monorepo is **adapted from plexor's web/**. See git history for
copy operations. Key changes:

- Workspace name `@plexor/*` → `@tessera/*`
- Brand Plexor → Tessera (in `index.html` meta tags)
- Removed `@playwright/test` (forbidden by `process/agent-runtime-safety.md`)
- Removed `@kubb/plugin-client` and kubb tooling (no OpenAPI codegen yet — use
  `openapi-typescript` when needed)
- Removed MSW (no backend to mock in MVP — re-add when backend lands)
- Removed `@faker-js/faker` (was used by MSW)

## What we DON'T have yet (planned)

- `shared/ui/` — Tessera design system (will be filled during implementation)
- `shared/lib/` — hooks, utils (will be filled)
- `shared/api/` — generated API client (will use `openapi-typescript`)
- `apps/console/src/features/` — feature code (built phase by phase per PLAN.md)
- `apps/console/src/routes/` — TanStack file-based routes (built phase by phase)
- `apps/console/public/favicon.svg` — Tessera-branded (placeholder for now)

## Architecture decisions

See `.agents/docs/ui/` for design system docs (catalog of components,
design tokens, playbook structure).

## Commands

```bash
bun run dev           # vite dev server (port 5173)
bun run build         # production build to apps/console/dist
bun run preview       # preview production build
bun run test          # vitest (unit tests)
bun run test:watch    # vitest watch mode
bun run lint          # eslint with @tessera/eslint-config
bun run typecheck     # tsc --noEmit
```