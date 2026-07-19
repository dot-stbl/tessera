import type { Story, StoryDefault } from '@ladle/react';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/shared/ui/primitives/select';

export default {
  title: 'Primitives / Select',
} satisfies StoryDefault;

/**
 * Native-feel dropdown built on Radix. Used for service/operation filters,
 * time-range presets, and any fixed-set choice.
 */
export const Basic: Story = () => (
  <div className="flex flex-col gap-4 p-6">
    <Select>
      <SelectTrigger className="w-64">
        <SelectValue placeholder="Select a service…" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="checkout-api">checkout-api</SelectItem>
        <SelectItem value="postgres">postgres</SelectItem>
        <SelectItem value="redis">redis</SelectItem>
        <SelectItem value="stripe">stripe</SelectItem>
      </SelectContent>
    </Select>
  </div>
);