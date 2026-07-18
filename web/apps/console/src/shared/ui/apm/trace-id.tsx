import { useState } from 'react';
import { cn } from '@/shared/lib/utils';

export interface TraceIdProps {
  /** Full trace or span ID (32 or 16 hex chars). */
  id: string;
  /** Number of chars to show before truncation. Default 12. */
  prefix?: number;
  className?: string;
}

/**
 * Trace / span ID display. Truncated to `prefix` chars + ellipsis.
 * Hover shows the full ID in a tooltip. Click copies the full ID to
 * the clipboard and shows a brief "copied" feedback.
 *
 * Usage:
 *   <TraceId id={trace.traceId} />
 *   <TraceId id={span.spanId} prefix={16} />
 */
export function TraceId({ id, prefix = 12, className }: TraceIdProps) {
  const [copied, setCopied] = useState(false);
  const truncated = id.length > prefix ? `${id.slice(0, prefix)}…` : id;

  async function handleClick() {
    try {
      await navigator.clipboard.writeText(id);
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    } catch {
      // clipboard blocked — silent failure
    }
  }

  return (
    <code
      className={cn('trace-id', copied && 'trace-id-copied', className)}
      title={id}
      onClick={handleClick}
      role="button"
      tabIndex={0}
    >
      {copied ? '✓ copied' : truncated}
    </code>
  );
}