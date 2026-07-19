/**
 * Ladle config for @tessera/playbook.
 *
 * @ladle/react v5 reads `config.mjs` from the `.ladle/` directory inside
 * the project root (`web/playbook/.ladle/config.mjs`). The legacy
 * `ladle.config.ts` filename is NOT auto-detected — a `-c` flag or this
 * `.ladle/` convention is required.
 *
 * See README for the full setup rationale and the v5.1.1 bug-tracking
 * notes (vite-tsconfig-paths plugin name detection, react/jsx-runtime
 * externalization).
 */
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export default {
  stories: 'src/stories/**/*.stories.{tsx,ts}',
  viteConfig: path.resolve(__dirname, '..', 'ladle-vite.config.ts'),
  port: 2006,
  host: '127.0.0.1',
};