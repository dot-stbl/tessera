import type { CSSProperties, ReactNode } from 'react';
import { serviceColorIndex } from '@/shared/ui/apm';
import { cn } from '@/shared/lib/utils';

/**
 * The small identity marks: what emitted this row, and what happened to it.
 *
 * Service colour is assigned by hash in one place (`serviceColorIndex`), so the
 * same service is the same colour on every screen — that consistency is the
 * only reason a 6px square is worth showing at all.
 */

/** Service name plus operation, one line, service a level down in ink. */
export function Subject({
  service,
  operation,
  mark,
}: {
  service?: string;
  operation: ReactNode;
  /** Show a colour swatch instead of the service name — for service-per-row lists. */
  mark?: string;
}) {
  return (
    <span className="cell-subject">
      {mark !== undefined && <ServiceMark name={mark} />}
      {service !== undefined && <span className="svc">{service}</span>}
      <span className="op">{operation}</span>
    </span>
  );
}

export function ServiceMark({ name }: { name: string }) {
  return (
    <i
      className="svc-mark"
      style={{ '--svc': `var(--svc-${serviceColorIndex(name)})` } as CSSProperties}
    />
  );
}

/** The services a trace touched, as a fixed-width strip so columns stay aligned. */
export function ServiceDots({ names, max = 5 }: { names: readonly string[]; max?: number }) {
  return (
    <span className="svc-dots">
      {names.slice(0, max).map((name) => (
        <i
          key={name}
          title={name}
          style={{ '--svc': `var(--svc-${serviceColorIndex(name)})` } as CSSProperties}
        />
      ))}
    </span>
  );
}

/** Trailing supporting values — operation names under a service, and the like. */
export function InlineList({ items, max = 3 }: { items: readonly string[]; max?: number }) {
  return (
    <span className="cell-ops">
      {items.slice(0, max).map((item) => (
        <span key={item}>{item}</span>
      ))}
    </span>
  );
}

/**
 * Status as a word in its own colour.
 *
 * Not a filled pill: across forty rows the pastel lozenges were the loudest
 * thing on screen and said the least. `tone` is always data, never structure —
 * see the two-reds rule.
 */
export function Tag({ tone, children }: { tone: 'ok' | 'error' | 'unset'; children: ReactNode }) {
  return <span className={cn('tag', `tag-${tone}`)}>{children}</span>;
}
