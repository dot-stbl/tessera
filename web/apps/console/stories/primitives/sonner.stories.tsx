import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { Toaster } from '@/shared/ui/primitives/sonner';
import { toast } from 'sonner';
import { Button } from '@/shared/ui/primitives/button';

export default {
  title: 'Primitives / Toast (Sonner)',
} satisfies StoryDefault;

/**
 * Sonner-based toast surface. Toaster must be mounted once in the app
 * root; individual toasts are triggered via the imperative `toast()`
 * API.
 */
export const Variants: Story = () => (
  <>
    <Toaster />
    <div className="flex flex-wrap items-center gap-3 p-6">
      <Button onClick={() => toast('Saved trace as panel')}>Default toast</Button>
      <Button onClick={() => toast.success('Dashboard published')}>Success toast</Button>
      <Button onClick={() => toast.error('Failed to fetch trace')}>Error toast</Button>
      <Button onClick={() => toast.warning('Slow query detected (>1s)')}>Warning toast</Button>
      <Button onClick={() => toast.info('Heads up: time range updated')}>Info toast</Button>
    </div>
  </>
);
