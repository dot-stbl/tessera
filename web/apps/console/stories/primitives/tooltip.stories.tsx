import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { Tooltip, TooltipTrigger, TooltipContent, TooltipProvider } from '@/shared/ui/primitives/tooltip';
import { Button } from '@/shared/ui/primitives/button';

export default {
  title: 'Primitives / Tooltip',
} satisfies StoryDefault;

/**
 * Floating tooltip. Default placement top; supports all 4 sides.
 */
export const Basic: Story = () => (
  <TooltipProvider>
    <div className="flex flex-wrap items-center gap-6 p-6">
      <Tooltip>
        <TooltipTrigger render={<Button variant="outline">Hover me (top)</Button>} />
        <TooltipContent>
          <p>Tooltip text — top placement</p>
        </TooltipContent>
      </Tooltip>
      <Tooltip>
        <TooltipTrigger render={<Button variant="outline">Hover me (right)</Button>} />
        <TooltipContent side="right">
          <p>Tooltip text — right placement</p>
        </TooltipContent>
      </Tooltip>
      <Tooltip>
        <TooltipTrigger render={<Button variant="outline">Hover me (bottom)</Button>} />
        <TooltipContent side="bottom">
          <p>Tooltip text — bottom placement</p>
        </TooltipContent>
      </Tooltip>
    </div>
  </TooltipProvider>
);
