import { useEffect } from 'react';
import { APP_NAME } from './app-name';

/**
 * Set `document.title` to `<page> · ${APP_NAME}` (all lowercase) for the
 * lifetime of the component. Restores the previous title on unmount.
 *
 * Usage:
 *   const { data: trace } = useGetTrace(id);
 *   useDocumentTitle(trace?.traceId ?? null);
 */
export function useDocumentTitle(page: string | null | undefined): void {
  useEffect(() => {
    const previous = document.title;
    document.title = page ? `${page.toLowerCase()} · ${APP_NAME}` : APP_NAME;
    return () => {
      document.title = previous;
    };
  }, [page]);
}