import type { Story, StoryDefault } from '@ladle/react';
import { Switch } from '@/shared/ui/primitives/switch';

export default {
  title: 'Primitives / Switch',
} satisfies StoryDefault;

/**
 * Toggle control. Used in settings pages.
 */
export const States: Story = () => (
  <div className="flex flex-col gap-4 p-6">
    <div className="flex items-center gap-3">
      <Switch />
      <span className="text-sm">Default (off)</span>
    </div>
    <div className="flex items-center gap-3">
      <Switch defaultChecked />
      <span className="text-sm">Default on</span>
    </div>
    <div className="flex items-center gap-3">
      <Switch disabled />
      <span className="text-sm text-muted-foreground">Disabled off</span>
    </div>
    <div className="flex items-center gap-3">
      <Switch disabled defaultChecked />
      <span className="text-sm text-muted-foreground">Disabled on</span>
    </div>
  </div>
);