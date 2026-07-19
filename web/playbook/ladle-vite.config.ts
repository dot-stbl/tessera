import { defineConfig, type Plugin } from 'vite';
import tailwindcss from '@tailwindcss/vite';
import tsconfigPaths from 'vite-tsconfig-paths';
import path from 'node:path';

/**
 * Vite config for @tessera/playbook (Ladle).
 *
 * Ladle v5.1.1 has a bug: it checks for the plugin named `"vite:tsconfig-paths"`
 * (with colon), but the published plugin exposes `"vite-tsconfig-paths"` (hyphen).
 * Without our explicit copy of the plugin, Ladle always adds its own with
 * `root: process.cwd()` = `web/playbook/` and tries to read OUR tsconfig from
 * there, producing wrong aliases.
 *
 * The patch in `node_modules/.bun/@ladle+react@5.1.1/.../get-user-vite-config.js`
 * rewrites the colon to a hyphen. With our `vite-tsconfig-paths` plugin present,
 * Ladle's `!hasTSConfigPathPlugin` short-circuit fires and we own the alias
 * mapping — pointing at `apps/console/tsconfig.json` so `@/...` resolves to
 * `apps/console/src/...` correctly.
 *
 * The `tesseraStylesInjector` plugin ensures `src/styles.css` is loaded by
 * Vite's CSS pipeline. Ladle's config.mjs can't import CSS (Node has no CSS
 * loader), so we inject the import at build time via a `transformIndexHtml`
 * hook — Vite then bundles it through `@tailwindcss/vite` and the result
 * lands in the production bundle.
 */
const tesseraStylesInjector = (): Plugin => ({
  name: 'tessera-styles-injector',
  transformIndexHtml() {
    return [
      {
        tag: 'script',
        attrs: { type: 'module' },
        // The path Vite uses to resolve the import; this virtual module
        // is rewritten in the `resolveId` hook below to point at the
        // real file on disk.
        children: "import '/@fs/__tessera-styles__';",
        injectTo: 'head-prepend',
      },
    ];
  },
  resolveId(id) {
    if (id === '/@fs/__tessera-styles__') {
      return path.resolve(import.meta.dirname, 'src/styles.css');
    }
    return null;
  },
});

export default defineConfig({
  plugins: [
    tesseraStylesInjector(),
    tsconfigPaths({
      root: import.meta.dirname,
      projects: [path.resolve(import.meta.dirname, '../apps/console/tsconfig.json')],
    }),
    tailwindcss(),
  ],
  resolve: {
    dedupe: ['react', 'react-dom'],
  },
  optimizeDeps: {
    include: ['react', 'react/jsx-runtime', 'react-dom'],
  },
  build: {
    rollupOptions: {
      // React + jsx-runtime are runtime-resolved peer deps, never bundled.
      external: ['react', 'react-dom', 'react/jsx-runtime'],
    },
  },
  appType: 'spa',
});
