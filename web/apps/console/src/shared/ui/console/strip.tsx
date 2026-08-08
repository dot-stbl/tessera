import type { ReactNode } from 'react';
import { Badge } from '@/shared/ui/primitives/badge';
import { Button } from '@/shared/ui/primitives/button';
import { ToggleGroup, ToggleGroupItem } from '@/shared/ui/primitives/toggle-group';
import { cn } from '@/shared/lib/utils';

/**
 * The filter strip: the recessed channel above a listing that states what the
 * rows below have been narrowed to.
 *
 * Filters read as the query they are — `service=stripe` — because that is what
 * they are, and because the operator has to reproduce this view in LogsQL five
 * minutes later. Everything here is a vendored primitive underneath: the
 * segmented control is Base UI's `ToggleGroup`, a chip is a `Badge` with a
 * `Button` for the clear action, so keyboard and ARIA come for free and this
 * file only carries Tessera's skin.
 */

export function Strip({ children }: { children: ReactNode }) {
  return <div className="strip">{children}</div>;
}

/** Pushes what follows to the right end of the strip. */
export function StripSpacer() {
  return <span className="strip-spacer" />;
}

export interface SegProps<T extends string> {
  label: string;
  value: T;
  options: readonly T[];
  onChange: (next: T) => void;
  /** Render a value as something other than itself. */
  format?: (value: T) => ReactNode;
}

/**
 * An exclusive choice among a handful of options. Denser than the shared
 * `SegmentedControl` primitive because it sits in a 30px strip; same semantics.
 */
export function Seg<T extends string>({ label, value, options, onChange, format }: SegProps<T>) {
  return (
    <ToggleGroup
      className="seg"
      aria-label={label}
      // Zero spacing is what makes it one control divided by hairlines rather
      // than three loose buttons — the choice is exclusive, so it should look
      // like a single thing being switched.
      spacing={0}
      value={[value]}
      // Base UI hands back the full selection. An empty array means the user
      // deselected the active option — for an exclusive filter there is no such
      // state, so it is ignored rather than turned into "no range at all".
      onValueChange={(next) => {
        const [chosen] = next as T[];
        if (chosen) onChange(chosen);
      }}
    >
      {options.map((option) => (
        <ToggleGroupItem key={option} value={option}>
          {format ? format(option) : option}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

export interface ChipProps {
  /** The filter's field — rendered as the `key` half of `key=value`. */
  name: string;
  value: ReactNode;
  onClear: () => void;
  clearLabel: string;
}

export function Chip({ name, value, onClear, clearLabel }: ChipProps) {
  return (
    <Badge variant="outline" className="chip">
      <span className="chip-key">{name}</span>
      {value}
      <Button
        variant="ghost"
        size="icon"
        className="chip-clear"
        aria-label={clearLabel}
        onClick={onClear}
      >
        ×
      </Button>
    </Badge>
  );
}

/** A label inside the strip or a panel header — the scaffolding type role. */
export function Meta({ children, className }: { children: ReactNode; className?: string }) {
  return <span className={cn('meta', className)}>{children}</span>;
}
