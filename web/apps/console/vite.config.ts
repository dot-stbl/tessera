import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

/**
 * Vite config for @tessera/console.
 *
 * Used by both the console SPA (`bun run dev` / `bun run build`) and
 * the Ladle component playground (`bun run playbook:*`) via the
 * `.ladle/config.mjs → viteConfig: 'vite.config.ts'` pointer.
 *
 * `playgroundStoriesCssInjector` runs only when the build `outDir` ends
 * in `dist-playbook` (the Ladle playbook). It uses Vite's
 * `transformIndexHtml` hook to inject `<link rel="stylesheet">` tags
 * for every CSS asset that Vite emits but Ladle's HTML doesn't link.
 *
 * Why this is needed: Ladle builds the playground as a SPA where the
 * main entry HTML is generated from Ladle's own `app/index.html`
 * template. Stories (and their CSS imports like `@/index.css`) are
 * dynamically loaded. The Tailwind-compiled CSS for those stories
 * lives in `utils-*.css` chunks; Vite only injects `<link>` tags for
 * the entry's statically-imported CSS, not for CSS chunks that are
 * pulled in via dynamic imports. Without this plugin, Tailwind
 * utilities and Tessera design tokens never reach the browser.
 *
 * The plugin is a no-op for the console SPA build path
 * (`outDir === 'dist'`); it only activates when the Ladle playbook
 * build runs (`outDir === 'dist-playbook'`).
 */

function playgroundStoriesCssInjector(): Plugin {
  let outDir = 'dist';
  return {
    name: 'tessera-playbook-stories-css-injector',
    apply: 'build',
    configResolved(config) {
      outDir = config.build.outDir;
    },
    transformIndexHtml(html, ctx) {
      if (!outDir.endsWith('dist-playbook')) return;

      // Collect emitted CSS file names from the bundle
      const cssFiles: string[] = [];
      if (ctx.bundle) {
        for (const [fileName, asset] of Object.entries(ctx.bundle)) {
          if (asset.type === 'asset' && fileName.endsWith('.css')) {
            cssFiles.push(fileName);
          }
        }
      }
      if (cssFiles.length === 0) return;

      // Skip files already referenced via <link rel="stylesheet">
      const existing = new Set<string>();
      const linkRe = /<link[^>]+href=["']([^"']+)["'][^>]*>/g;
      let m: RegExpExecArray | null;
      while ((m = linkRe.exec(html)) !== null) {
        if (m[1]) existing.add(m[1]);
      }

      const newLinks = cssFiles
        .filter((f) => !existing.has(`/${f}`) && !existing.has(f))
        .map((f) => `    <link rel="stylesheet" href="/${f}">`)
        .join('\n');

      if (!newLinks) return;
      return html.replace(
        '</head>',
        `\n    <!-- injected by tessera-playbook-stories-css-injector -->\n${newLinks}\n  </head>`,
      );
    },
  };
}

export default defineConfig({
  // NOTE: we use code-based TanStack Router (defined in src/router.tsx),
  // not file-based. Do not add @tanstack/router-plugin/vite — it would
  // require src/routes/ to exist.
  plugins: [
    react(),
    tailwindcss(),
    playgroundStoriesCssInjector(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    // Tessera port pool: 1990–2120 (see .agents/rules/coding/project-ports.md).
    // 1991 = vite dev server. strictPort fails if taken (no fallback to 5173).
    port: 1991,
    strictPort: true,
    cors: true,
  },
  // SPA fallback — TanStack Router uses History API navigation, so
  // direct hits on /traces/$id, /dashboards/$id etc. need to be served
  // index.html so the client-side router can take over.
  appType: 'spa',
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
