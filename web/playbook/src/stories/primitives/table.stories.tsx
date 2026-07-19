import type { Story, StoryDefault } from '@ladle/react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/shared/ui/primitives/table';
import { Duration } from '@/shared/ui/apm/duration';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

export default {
  title: 'Primitives / Table',
} satisfies StoryDefault;

/**
 * Base table primitive. Wrapped by `DataTable` in
 * `shared/ui/data-table/` for actual app usage with sorting, pagination
 * and column resize.
 */
const sampleRows = [
  { id: 'abc123', service: 'checkout-api', duration: 1247, status: 'ok' as const },
  { id: 'def456', service: 'postgres',     duration: 50,   status: 'ok' as const },
  { id: 'ghi789', service: 'stripe',       duration: 4180, status: 'err' as const },
];

export const Basic: Story = () => (
  <div className="p-6">
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Trace</TableHead>
          <TableHead>Service</TableHead>
          <TableHead className="text-right">Duration</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {sampleRows.map((row) => (
          <TableRow key={row.id}>
            <TableCell className="font-mono text-xs">{row.id}…</TableCell>
            <TableCell>{row.service}</TableCell>
            <TableCell className="text-right">
              <Duration ms={row.duration} />
            </TableCell>
            <TableCell>
              <StatusPill variant={row.status}>
                {row.status === 'ok' ? 'OK' : 'ERROR'}
              </StatusPill>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  </div>
);