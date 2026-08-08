import type { ReactNode } from 'react';
import {
  Empty,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from '@/shared/ui/primitives/empty';
import { cn } from '@/shared/lib/utils';

/**
 * Nothing to show — stated as a direction, never as a shrug.
 *
 * shadcn's `Empty` supplies the structure and the slots; the `.blank` block
 * re-aims it. Two deliberate changes from the stock component: it is left-
 * aligned rather than centred, and it has no dashed frame. A centred card with
 * a dotted border is a marketing-site empty state; an operator reading "no
 * spans in this window, widen the range" is reading a sentence, and sentences
 * start at the left margin under the toolbar that caused them.
 */

export function Blank({
  title,
  children,
  className,
}: {
  title: ReactNode;
  /** One or more `<BlankText>` paragraphs saying what to do next. */
  children?: ReactNode;
  className?: string;
}) {
  return (
    <Empty className={cn('blank', className)}>
      <EmptyHeader className="contents">
        <EmptyTitle className="blank-title">{title}</EmptyTitle>
        {children}
      </EmptyHeader>
    </Empty>
  );
}

export function BlankText({ children }: { children: ReactNode }) {
  return <EmptyDescription className="blank-body">{children}</EmptyDescription>;
}

/**
 * A machine value inside a sentence — a path, an endpoint, an env var. The type
 * roles hold even in prose: anything the machine reads keeps the machine face.
 */
export function Code({ children }: { children: ReactNode }) {
  return <code>{children}</code>;
}
