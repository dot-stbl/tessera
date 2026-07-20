import { QueryClient } from '@tanstack/react-query';

/**
 * Shared TanStack Query client for the console. Sensible defaults for an APM
 * UI: results stay fresh for 30s (matches the list pages' refetchInterval) and
 * we don't refetch on window focus, which would spam Victoria on tab switches.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});
