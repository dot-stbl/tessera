import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

export default defineConfig({
  // NOTE: we use code-based TanStack Router (defined in src/router.tsx),
  // not file-based. Do not add @tanstack/router-plugin/vite — it would
  // require src/routes/ to exist.
  plugins: [
    react(),
    tailwindcss(),
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