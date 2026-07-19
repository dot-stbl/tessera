import type { Story, StoryDefault } from '@ladle/react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/shared/ui/primitives/tabs';

export default {
  title: 'Primitives / Tabs',
} satisfies StoryDefault;

/**
 * Tabbed view. Used on detail panels to switch between related datasets
 * (trace / logs / metrics for a service).
 */
export const Horizontal: Story = () => (
  <div className="p-6">
    <Tabs defaultValue="traces">
      <TabsList>
        <TabsTrigger value="traces">Traces</TabsTrigger>
        <TabsTrigger value="logs">Logs</TabsTrigger>
        <TabsTrigger value="metrics">Metrics</TabsTrigger>
      </TabsList>
      <TabsContent value="traces">
        <p className="text-sm text-muted-foreground">Recent traces for $service.</p>
      </TabsContent>
      <TabsContent value="logs">
        <p className="text-sm text-muted-foreground">Recent log lines, INFO and above.</p>
      </TabsContent>
      <TabsContent value="metrics">
        <p className="text-sm text-muted-foreground">RED metrics (rate, errors, duration).</p>
      </TabsContent>
    </Tabs>
  </div>
);