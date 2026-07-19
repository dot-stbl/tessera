import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { Popover, PopoverTrigger, PopoverContent } from '@/shared/ui/primitives/popover';
import { Button } from '@/shared/ui/primitives/button';

export default {
  title: 'Primitives / Popover',
} satisfies StoryDefault;

/**
 * Floating content panel anchored to a trigger. Lighter than a dialog.
 */
export const Basic: Story = () => (
  <div className="p-12">
    <Popover>
      <PopoverTrigger render={<Button variant="outline">Open popover</Button>} />
      <PopoverContent className="w-72">
        <div className="space-y-2">
          <h4 className="font-medium">Filters</h4>
          <p className="text-xs text-muted-foreground">Narrow the result set by service or time range.</p>
        </div>
      </PopoverContent>
    </Popover>
  </div>
);
