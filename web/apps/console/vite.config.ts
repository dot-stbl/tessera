import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { TanStackRouterVite } from '@tanstack/router-plugin/vite';
import path from 'node:path';

export default defineConfig({
  plugins: [
    // TanStack Router file-based code-gen — generates routeTree.gen.ts
    // when src/routes/ has files. Optional — disabled if routesDir absent.
    TanStackRouterVite({
      routesDirectory: './src/routes',
      generatedRouteTree: './src/routeTree.gen.ts',
    }),
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