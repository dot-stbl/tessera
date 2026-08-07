import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/utils';

export interface PageTemplateProps {
  title: string;
  subtitle?: string;
  /** Header actions (right side). */
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}

/**
 * Standard page layout: title, subtitle and actions on one baseline, then the
 * content, which is free to fill the window.
 *
 * Title and subtitle share a line rather than stacking. Every subtitle in this
 * app is a count or summary of the thing in the title ("1 284 traces · last 1h"),
 * so the two read as one statement; stacking spent a whole line on a comma.
 *
 * Usage:
 *   <PageTemplate title="Traces" subtitle="1 284 · last 1h" actions={<Refresh />}>
 *     <TraceTable />
 *   </PageTemplate>
 */
export function PageTemplate({ title, subtitle, actions, children, className }: PageTemplateProps) {
  return (
    <div className={cn('page', className)}>
      <header className="page-header">
        <h1 className="page-title">{title}</h1>
        {subtitle && <div className="page-subtitle">{subtitle}</div>}
        {actions && <div className="page-actions">{actions}</div>}
      </header>
      <div className="page-body">{children}</div>
    </div>
  );
}