import type { Story, StoryDefault } from '@ladle/react';
import { Button } from '@/shared/ui/primitives/button';

export default {
  title: 'Primitives / Button',
  meta: { iframed: false },
} satisfies StoryDefault;

/**
 * Smoke test — confirms Ladle + Vite alias + Tailwind pipeline works.
 * Other primitives stories live next to this file once the smoke test
 * typechecks cleanly (see commit history).
 */
export const Smoke: Story = () => (
  <div className="flex flex-col gap-4 p-8">
    <Button>Click me</Button>
    <Button variant="destructive">Destructive</Button>
    <Button variant="outline">Outline</Button>
    <Button variant="ghost">Ghost</Button>
    <Button disabled>Disabled</Button>
  </div>
);
