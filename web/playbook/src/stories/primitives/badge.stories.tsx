import type { Story, StoryDefault } from '@ladle/react';
import { Badge } from '@/shared/ui/primitives/badge';

export default {
  title: 'Primitives / Badge',
} satisfies StoryDefault;

/**
 * Inline status / count badge. Smaller than `<StatusPill>`; for
 * non-semantic counters and tags inside list rows.
 */
export const Variants: Story = () => (
  <div className="flex flex-wrap items-center gap-2 p-6">
    <Badge>Default</Badge>
    <Badge variant="secondary">Secondary</Badge>
    <Badge variant="outline">Outline</Badge>
    <Badge variant="destructive">Destructive</Badge>
    <Badge variant="secondary">Success</Badge>
    <Badge variant="outline">Warning</Badge>
  </div>
);

export const InContext: Story = () => (
  <div className="flex flex-col gap-2 p-6 font-mono text-xs">
    <div className="flex items-center gap-2">
      <Badge>42</Badge>
      <span>spans in trace</span>
    </div>
    <div className="flex items-center gap-2">
      <Badge variant="destructive">3</Badge>
      <span>errors in last minute</span>
    </div>
  </div>
);