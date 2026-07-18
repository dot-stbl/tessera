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
 * Standard page layout: title + subtitle + actions, then content.
 * Used for most pages in tessera console.
 *
 * Usage:
 *   <PageTemplate title="Traces" subtitle="..." actions={<Button>...</Button>}>
 *     <TraceListTable />
 *   </PageTemplate>
 */
export function PageTemplate({ title, subtitle, actions, children, className }: PageTemplateProps) {
  return (
    <div className={cn('page', className)}>
      <header className="page-header">
        <div>
          <h1 className="page-title">{title}</h1>
          {subtitle && <div className="page-subtitle">{subtitle}</div>}
        </div>
        {actions && <div>{actions}</div>}
      </header>
      <div className="page-body">{children}</div>
    </div>
  );
}