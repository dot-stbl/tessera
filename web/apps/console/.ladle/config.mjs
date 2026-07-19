/**
 * Ladle config for @tessera/console.
 *
 * Stories live under `stories/` (not `src/stories/` — see README.md for
 * why) and import production components directly from `@/shared/ui/...`
 * via console's existing tsconfig.json paths block.
 *
 * `viteConfig: 'vite.config.ts'` makes Ladle use the console's own Vite
 * config — Tailwind 4 plugin, React SWC plugin, alias @, dedupe. All
 * Tailwind scanning and design tokens come from console's `src/index.css`
 * via Tailwind's Vite plugin.
 *
 * Stories also `import "@/index.css"` so Tailwind processes the design
 * system file even though Ladle's Vite root is its own bundled app dir.
 * Without this, the bundle has no Tailwind utilities.
 *
 * Port **2006** in the project pool (1990–2120 per
 * `.agents/rules/coding/project-ports.md`). Console dev uses 1991.
 */
export default {
  stories: 'stories/**/*.stories.{tsx,ts,mdx}',
  viteConfig: 'vite.config.ts',
  port: 2006,
  host: '127.0.0.1',
};

