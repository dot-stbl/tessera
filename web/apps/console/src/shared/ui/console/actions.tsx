import { useState, type ReactNode } from 'react';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { Check, KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/400';
import { Button } from '@/shared/ui/primitives/button';
import { Checkbox } from '@/shared/ui/primitives/checkbox';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover';
import { ToggleGroup, ToggleGroupItem } from '@/shared/ui/primitives/toggle-group';
import { cn } from '@/shared/lib/utils';

/**
 * The controls that turn a table into an instrument.
 *
 * A list you can only read is a report. What makes an APM usable at 3am is that
 * every row is a starting point: narrow to this service, pull this trace's
 * logs, copy this id into the incident channel. These are the components for
 * that, and they share one rule — actions live **on the row**, revealed on
 * hover and on keyboard focus, never in a column of permanently visible icon
 * buttons that would add forty pieces of chrome to a forty-row screen.
 */

export interface RowAction {
  icon: Icon;
  /** Imperative and specific: "Copy trace id", not "Copy". */
  label: string;
  onAct: () => void;
}

export function RowActions({ actions }: { actions: RowAction[] }) {
  return (
    <span className="row-actions">
      {actions.map((action) => {
        const Glyph = action.icon;
        return (
          <button
            key={action.label}
            type="button"
            title={action.label}
            aria-label={action.label}
            onClick={(event) => {
              // The row itself is a link. Without this, every action would also
              // navigate, and "copy id" would take you away from the list.
              event.stopPropagation();
              action.onAct();
            }}
          >
            <Glyph aria-hidden="true" />
          </button>
        );
      })}
    </span>
  );
}

/**
 * A copy button that says what it did. The label flips to "Copied" for two
 * seconds — the same word the action promised, in the past tense.
 */
export function CopyAction({ value, label }: { value: string; label: string }) {
  const [done, setDone] = useState(false);

  return (
    <Button
      variant="outline"
      className="h-[26px] gap-1.5 px-2.5 text-[11.5px]"
      onClick={() => {
        void navigator.clipboard.writeText(value);
        setDone(true);
        setTimeout(() => setDone(false), 2000);
      }}
    >
      {done ? <Check className="size-3.5" /> : null}
      {done ? 'Copied' : label}
    </Button>
  );
}

/**
 * Which columns are shown. Kibana's field picker, minus the sidebar: an
 * operator adds a column to answer one question and drops it again, so the
 * control belongs next to the table rather than beside it permanently.
 */
export function ColumnPicker<T extends string>({
  columns,
  hidden,
  onToggle,
  labels,
}: {
  columns: readonly T[];
  hidden: ReadonlySet<T>;
  onToggle: (column: T) => void;
  labels: Record<T, string>;
}) {
  const shown = columns.length - hidden.size;

  return (
    <Popover>
      <PopoverTrigger
        render={
          <Button variant="outline" className="h-[26px] gap-1.5 px-2.5 text-[11.5px]">
            Columns
            <span className="text-muted-foreground">
              {shown}/{columns.length}
            </span>
            <KeyboardArrowDown className="size-3.5" />
          </Button>
        }
      />
      <PopoverContent className="w-48 p-1.5" align="end">
        {columns.map((column) => (
          <label key={column} className="column-option">
            <Checkbox
              checked={!hidden.has(column)}
              onCheckedChange={() => onToggle(column)}
            />
            {labels[column]}
          </label>
        ))}
      </PopoverContent>
    </Popover>
  );
}

/**
 * A non-exclusive filter — log levels, where "warn and error" is a normal thing
 * to want and a segmented control cannot express it. Empty selection means no
 * filter rather than no results: an operator who unticks everything wants the
 * unfiltered stream back, not an empty screen.
 */
export function MultiFilter<T extends string>({
  label,
  options,
  selected,
  onChange,
  tone,
}: {
  label: string;
  options: readonly T[];
  selected: readonly T[];
  onChange: (next: T[]) => void;
  /** Maps an option to a class carrying its semantic colour. */
  tone?: (option: T) => string | undefined;
}) {
  return (
    <ToggleGroup
      className="seg"
      aria-label={label}
      spacing={0}
      multiple
      value={[...selected]}
      onValueChange={(next) => onChange(next as T[])}
    >
      {options.map((option) => (
        <ToggleGroupItem key={option} value={option} className={cn(tone?.(option))}>
          {option}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

/** The page-level action cluster, right-aligned in the header. */
export function Actions({ children }: { children: ReactNode }) {
  return <div className="page-actions">{children}</div>;
}
