import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * `cn` — className merger (clsx + tailwind-merge). Standard shadcn helper.
 *
 * Shim file: 84 Plexor primitives import from `@/lib/utils` (Plexor
 * convention preserved from `web/apps/console/src/lib/utils.ts`), while the
 * tessera convention per `components.json` is `@/shared/lib/utils`. This
 * shim satisfies the Plexor imports without rewriting the primitives; the
 * `shared/lib/utils.ts` source of truth remains the canonical location.
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
