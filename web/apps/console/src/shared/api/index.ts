import { createHttpApi, type TesseraApi } from './client';
import { createMockApi } from './mock-api';

/**
 * The single API instance the app talks to.
 *
 * Which implementation is chosen is decided once, here, from
 * `VITE_API_BASE_URL`: set it and the app talks HTTP, leave it unset and the
 * app runs on fixtures. No call site knows which one it got, and the mock
 * fixtures are tree-shaken out of a production build because nothing imports
 * them when the variable is set at build time.
 */
const baseUrl = import.meta.env['VITE_API_BASE_URL'] ?? '';

export const usingMockData = baseUrl === '';

export const api: TesseraApi = usingMockData ? createMockApi() : createHttpApi(baseUrl);

export type { TesseraApi } from './client';
export { createHttpApi } from './client';
export { createMockApi, mockIds } from './mock-api';
export { apiRoutes } from './routes';
export { TesseraApiError, type ProblemDetails } from './problem-details';
export type * from './types';
