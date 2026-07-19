import type { Story, StoryDefault } from '@ladle/react';
import { Skeleton } from '@/shared/ui/primitives/skeleton';

export default {
  title: 'Primitives / Skeleton',
} satisfies StoryDefault;

/**
 * Loading placeholder. Pulses softly while data is fetching.
 */
export const Shapes: Story = () => (
  <div className="flex flex-col gap-3 p-6 max-w-md">
    <Skeleton className="h-4 w-3/4" />
    <Skeleton className="h-4 w-1/2" />
    <Skeleton className="h-8 w-full" />
    <Skeleton className="h-32 w-full rounded-md" />
  </div>
);

export const CardLoading: Story = () => (
  <div className="p-6 max-w-md">
    <div className="rounded-md border p-6 space-y-3">
      <Skeleton className="h-5 w-1/2" />
      <Skeleton className="h-4 w-3/4" />
      <Skeleton className="h-32 w-full" />
    </div>
  </div>
);