import type { Story, StoryDefault } from '@ladle/react';
import { SpanRow } from '@/shared/ui/apm/span-row';

export default {
  title: 'APM / Span row',
} satisfies StoryDefault;

/**
 * Single span in the waterfall. Used directly by `Waterfall` for each
 * row, but can also be rendered standalone when showing partial trace
 * detail (e.g. in a side panel).
 */

export const Standalone: Story = () => (
  <div className="p-6">
    <SpanRow
      service="checkout-api"
      name="POST /checkout"
      durationMs={1247}
      startOffsetMs={0}
    />
  </div>
);

export const Statuses: Story = () => (
  <div className="flex flex-col gap-3 p-6">
    <SpanRow service="postgres" name="SELECT orders"  durationMs={50}  startOffsetMs={0} status="ok" />
    <SpanRow service="stripe"   name="Charge"        durationMs={1180} startOffsetMs={100} status="error" />
    <SpanRow service="redis"    name="GET cache"     durationMs={5}   startOffsetMs={200} status="idle" />
  </div>
);

export const Depths: Story = () => (
  <div className="flex flex-col gap-2 p-6">
    <SpanRow service="api"     name="root"         durationMs={1247} startOffsetMs={0}   depth={0} />
    <SpanRow service="db"      name="SELECT"       durationMs={50}   startOffsetMs={12}  depth={1} />
    <SpanRow service="cache"   name="GET"          durationMs={5}    startOffsetMs={64}  depth={2} />
    <SpanRow service="worker"  name="publish"      durationMs={20}   startOffsetMs={80}  depth={3} />
  </div>
);