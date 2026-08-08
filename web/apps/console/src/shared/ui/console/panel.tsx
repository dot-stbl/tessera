import type { CSSProperties, ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

/**
 * A bounded settings surface: a legend and a stack of label/control rows.
 *
 * Unlike a listing this is capped in width. A table earns full bleed because
 * more width is more data; a form does not — at 1400px the label and its
 * control end up at opposite ends of the screen and stop reading as one row.
 */

export function Panel({ legend, children }: { legend: ReactNode; children: ReactNode }) {
  return (
    <section className="settings">
      <h2 className="meta settings-legend">{legend}</h2>
      {children}
    </section>
  );
}

export function PanelRow({
  label,
  hint,
  children,
}: {
  /** Omit on a row that is an action rather than a setting. */
  label?: ReactNode;
  hint: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="settings-row">
      <div className="settings-label">
        {label !== undefined && <span>{label}</span>}
        <p>{hint}</p>
      </div>
      {children}
    </div>
  );
}

/** A read-only machine value on the right of a panel row. */
export function PanelValue({ children }: { children: ReactNode }) {
  return <span className="settings-value">{children}</span>;
}

/**
 * The loading state for a listing.
 *
 * Deliberately not built on the shadcn `Skeleton`, which is a filled pulsing
 * lozenge — the exact thing the design system rejects here. What is coming is a
 * table with a time gutter, so that is what the placeholder draws: the gutter's
 * tint, the rule, and one bar of plausible subject length per row. Nothing
 * jumps when the real rows arrive.
 *
 * Widths cycle through a fixed sequence. A random one would reshuffle on every
 * render, and `Math.random()` in a render body is a bug waiting for a reason.
 */
const SKELETON_WIDTHS = [42, 66, 34, 58, 48, 72, 38, 54] as const;

export function LoadingRows({ count = 8, className }: { count?: number; className?: string }) {
  return (
    <div className={cn('loading-rows', className)} aria-hidden="true">
      {Array.from({ length: count }, (_, index) => (
        <i
          key={index}
          style={
            {
              '--w': `${SKELETON_WIDTHS[index % SKELETON_WIDTHS.length]}%`,
              '--i': index,
            } as CSSProperties
          }
        />
      ))}
    </div>
  );
}
