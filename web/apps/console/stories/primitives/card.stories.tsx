import "@/index.css";
import type { Story, StoryDefault } from '@ladle/react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/shared/ui/primitives/card';

export default {
  title: 'Primitives / Card',
} satisfies StoryDefault;

/**
 * Surface container with optional header, body, footer.
 */
export const Basic: Story = () => (
  <div className="p-6">
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle>Trace 7f3a</CardTitle>
        <CardDescription>checkout-api · POST /checkout · 1247ms</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm">6 spans · 1 error · duration p99 within budget.</p>
      </CardContent>
      <CardFooter>
        <button className="btn btn-sm">Open trace</button>
      </CardFooter>
    </Card>
  </div>
);

export const Layouts: Story = () => (
  <div className="grid grid-cols-2 gap-4 p-6">
    <Card>
      <CardContent className="pt-6">
        <p className="text-sm text-muted-foreground">Body-only card.</p>
      </CardContent>
    </Card>
    <Card>
      <CardHeader>
        <CardTitle>Compact</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-sm">Header + body.</p>
      </CardContent>
    </Card>
  </div>
);
