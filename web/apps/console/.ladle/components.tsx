import type { GlobalProvider } from '@ladle/react';
import '@/index.css';

/**
 * Ladle global provider.
 *
 * Loads the Tessera design system (`@/index.css` — tokens + Tailwind layers +
 * APM component styles) once, so every story renders styled in BOTH
 * `playbook:dev` and `playbook:build`. Previously only the build path injected
 * the CSS (see `playgroundStoriesCssInjector` in vite.config.ts), so
 * `playbook:dev` came up unstyled.
 *
 * The wrapper maps Ladle's light/dark control onto our `@custom-variant dark`,
 * which keys off `[data-theme="dark"]` (and `.dark`). Setting `data-theme` here
 * makes the token overrides in index.css cascade into the story canvas.
 */
export const Provider: GlobalProvider = ({ children, globalState }) => (
  <div
    data-theme={globalState.theme === 'dark' ? 'dark' : 'light'}
    className="min-h-screen bg-background text-foreground"
  >
    {children}
  </div>
);
