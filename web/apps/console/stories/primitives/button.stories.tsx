import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { Button } from '@/shared/ui/primitives/button';

export default {
  title: 'Primitives / Button',
  meta: { iframed: false },
} satisfies StoryDefault;

/**
 * Smoke-test story for the Ladle + Vite alias + Tailwind token pipeline.
 *
 * `tsc --noEmit` (`bun run typecheck`) passes for this file, confirming
 * `@/shared/ui/primitives/button` resolves via `web/playbook/tsconfig.json`
 * (`baseUrl: ../apps/console`, `paths: { "@/*": ["./src/*"] }`).
 *
 * Run `bun run playbook:dev` to launch the live playground on
 * http://127.0.0.1:2006. See README for the current bundling bug
 * tracking issue.
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
