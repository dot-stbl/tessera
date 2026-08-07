/**
 * Route templates, mirroring `ApiRoutes` on the backend.
 *
 * Every path carries the `v1` segment. The previous client omitted it, so each
 * of the five endpoints it called would have 404'd the moment the app was
 * pointed at a real backend — nothing caught it because the mock branch never
 * used the paths at all.
 */

const BASE = '/api/v1';

export const apiRoutes = {
  health: `${BASE}/health`,
  services: `${BASE}/services`,
  traces: `${BASE}/traces`,
  trace: (traceId: string) => `${BASE}/traces/${encodeURIComponent(traceId)}`,
  logs: `${BASE}/logs`,
  errors: `${BASE}/errors`,
} as const;
