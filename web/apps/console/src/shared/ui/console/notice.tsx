import type { ReactNode } from 'react';
import { Alert, AlertDescription } from '@/shared/ui/primitives/alert';
import { cn } from '@/shared/lib/utils';

/**
 * A one-line caveat about the data below it — "this view is partial", "the
 * provider degraded". Not an error: the screen still works, but something about
 * it is not what the operator assumed, and assuming wrong here costs an hour.
 *
 * shadcn's `Alert` ships `default` and `destructive` only. The tone that
 * matters most in an APM is the middle one — attention, not failure — so it is
 * added here rather than by reaching for `destructive` and diluting what red
 * means (see the two-reds rule).
 */
export function Notice({
  tone = 'attention',
  children,
}: {
  tone?: 'attention' | 'danger';
  children: ReactNode;
}) {
  return (
    <Alert className={cn('notice', `notice-${tone}`)}>
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  );
}
