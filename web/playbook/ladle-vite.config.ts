import { defineConfig } from 'vite';
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
 */
export default defineConfig({
  plugins: [
    tsconfigPaths({
      root: import.meta.dirname,
      projects: [path.resolve(import.meta.dirname, '../apps/console/tsconfig.json')],
    }),
    tailwindcss(),
    {
      name: 'tessera-at-alias-fallback',
      enforce: 'pre',
      async resolveId(id, importer, options) {
        if (id.startsWith('@/') && importer) {
          const subpath = id.slice(2);
          const target = path.resolve(
            import.meta.dirname,
            '../apps/console/src',
            subpath,
          );
          // Defer to Vite's resolver so it picks up the right extension
          // (.ts/.tsx/.js/.jsx) and file resolution rules.
          const resolved = await this.resolve(target, importer, {
            ...options,
            skipSelf: true,
          });
          return resolved ?? target;
        }
        return null;
      },
    },
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