import type { Story, StoryDefault } from '@ladle/react';
import { Separator } from '@/shared/ui/primitives/separator';

export default {
  title: 'Primitives / Separator',
} satisfies StoryDefault;

/**
 * Visual divider. Horizontal (default) or vertical.
 */
export const Horizontal: Story = () => (
  <div className="p-6 max-w-sm">
    <div className="space-y-1">
      <h4 className="text-sm font-medium">Trace detail</h4>
      <p className="text-xs text-muted-foreground">checkout-api · 7 spans</p>
    </div>
    <Separator className="my-4" />
    <div className="flex h-5 items-center gap-2 text-sm">
      <span>Duration</span>
      <span className="text-muted-foreground">·</span>
      <span className="font-mono">1247ms</span>
    </div>
  </div>
);

export const Vertical: Story = () => (
  <div className="p-6 flex items-center gap-3 h-8">
    <span className="text-sm">Trace</span>
    <Separator orientation="vertical" />
    <span className="text-sm">Logs</span>
    <Separator orientation="vertical" />
    <span className="text-sm">Metrics</span>
  </div>
);