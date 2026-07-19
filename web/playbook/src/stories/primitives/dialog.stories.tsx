import type { Story, StoryDefault } from '@ladle/react';
import { Dialog, DialogTrigger, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/shared/ui/primitives/dialog';
import { Button } from '@/shared/ui/primitives/button';

export default {
  title: 'Primitives / Dialog',
} satisfies StoryDefault;

/**
 * Modal dialog. Used for confirmations, settings sheets, and any
 * operation that needs explicit user confirmation.
 */
export const Confirm: Story = () => (
  <div className="p-6">
    <Dialog>
      <DialogTrigger render={<Button variant="destructive">Delete dashboard</Button>} />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete this dashboard?</DialogTitle>
          <DialogDescription>
            This permanently removes the dashboard and its panels. The
            underlying data sources are not affected.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline">Cancel</Button>
          <Button variant="destructive">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
);

export const WithForm: Story = () => (
  <div className="p-6">
    <Dialog>
      <DialogTrigger render={<Button>Save trace as dashboard panel</Button>} />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Save panel</DialogTitle>
          <DialogDescription>Pick a dashboard to attach this query to.</DialogDescription>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <input className="input w-full" placeholder="Dashboard name" />
          <select className="input w-full">
            <option>Service overview</option>
            <option>Error budget</option>
          </select>
        </div>
        <DialogFooter>
          <Button variant="outline">Cancel</Button>
          <Button>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
);