import type { ReactNode } from 'react';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { Card } from '@/shared/ui/primitives/card';
import { cn } from '@/shared/lib/utils';

/**
 * The summary bar above a listing: the four numbers that decide whether the
 * rows below are worth reading.
 *
 * A trace list answers "which request was slow". It does not answer "is this
 * hour worse than the last one", and an operator arriving from an alert needs
 * that first — otherwise they scan forty rows to work out something the header
 * could have said. One card divided by hairlines rather than four floating
 * tiles: these are facets of one window, not four independent widgets.
 */

export interface StatItem {
  icon: Icon;
  label: string;
  value: ReactNode;
  /** The second line — context for the number, not a repeat of the label. */
  sub?: ReactNode;
  /** Raises the value into the data red. For rates that are actually bad. */
  bad?: boolean;
}

export function StatBar({ items }: { items: StatItem[] }) {
  return (
    <Card className="stat-bar">
      {items.map((item) => {
        const Glyph = item.icon;
        return (
          <div key={item.label} className="stat">
            <Glyph className="stat-icon" aria-hidden="true" />
            <div className="stat-body">
              <div className="stat-line">
                <span className={cn('stat-value', item.bad && 'is-bad')}>{item.value}</span>
                <span className="stat-label">{item.label}</span>
              </div>
              {item.sub !== undefined && <span className="stat-sub">{item.sub}</span>}
            </div>
          </div>
        );
      })}
    </Card>
  );
}
