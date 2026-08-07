import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { LogEntry } from '@/shared/ui/apm';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { cn } from '@/shared/lib/utils';

const HOUR = 60 * 60 * 1000;

export function LogsPage() {
  const { t } = useTranslation();
  useDocumentTitle('Logs');

  const [stream, setStream] = useState<string>('');
  const [range, setRange] = useState<'1h' | '6h' | '24h'>('1h');
  const [traceId, setTraceId] = useState<string>('');

  const endUnixMs = Date.now();
  const startUnixMs = endUnixMs - (range === '1h' ? HOUR : range === '6h' ? 6 * HOUR : 24 * HOUR);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['logs', { stream, traceId, startUnixMs, endUnixMs }],
    queryFn: ({ signal }) =>
      api.listLogs(
        {
          // The backend narrows logs by trace id and stream. There is no
          // free-text parameter yet — ad-hoc LogsQL search is a later phase — so
          // the toolbar filters by service stream instead of pretending.
          stream: stream || undefined,
          traceId: traceId || undefined,
          startUnixMs,
          endUnixMs,
          limit: 500,
        },
        signal,
      ),
    refetchInterval: 30_000,
  });

  return (
    <PageTemplate
      title={t('logs.title')}
      subtitle={`${data?.items.length ?? 0} log entries · last ${range}`}
    >
      <Toolbar
        range={range}
        onRangeChange={setRange}
        query={stream}
        onQueryChange={setStream}
        traceId={traceId}
        onTraceIdChange={setTraceId}
      />

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load logs: {(error as Error).message}
        </div>
      )}

      {isLoading ? (
        <SkeletonList />
      ) : data && data.items.length === 0 ? (
        <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          {t('logs.empty.title')}
        </div>
      ) : (
        <div className="overflow-hidden rounded-md border border-border bg-card">
          {data?.items.map((entry, i) => (
            <LogEntry
              key={`${entry.timestamp}-${i}`}
              timestamp={entry.timestamp}
              level={entry.level}
              service={entry.service}
              traceId={entry.traceId ?? undefined}
              spanId={entry.spanId ?? undefined}
              message={entry.message}
              fields={entry.fields}
            />
          ))}
        </div>
      )}
    </PageTemplate>
  );
}

interface ToolbarProps {
  range: '1h' | '6h' | '24h';
  onRangeChange: (next: '1h' | '6h' | '24h') => void;
  query: string;
  onQueryChange: (next: string) => void;
  traceId: string;
  onTraceIdChange: (next: string) => void;
}

function Toolbar({ range, onRangeChange, query, onQueryChange, traceId, onTraceIdChange }: ToolbarProps) {
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-md border border-border bg-card p-3">
      <div className="flex items-center gap-1">
        {(['1h', '6h', '24h'] as const).map((r) => (
          <button
            key={r}
            onClick={() => onRangeChange(r)}
            className={cn(
              'rounded-sm border px-2 py-1 text-xs',
              range === r
                ? 'border-foreground/60 bg-accent text-accent-foreground'
                : 'border-border bg-background text-muted-foreground hover:border-foreground/30',
            )}
          >
            {r === '1h' ? 'Last 1h' : r === '6h' ? 'Last 6h' : 'Last 24h'}
          </button>
        ))}
      </div>
      <div className="flex-1 min-w-[200px]">
        <input
          type="text"
          placeholder="Service, e.g. checkout-api"
          value={query}
          onChange={(e) => onQueryChange(e.target.value)}
          className="h-7 w-full rounded-sm border border-border bg-background px-2 font-mono text-xs text-foreground placeholder:text-muted-foreground focus:border-foreground/60 focus:outline-none"
        />
      </div>
      <div className="w-56">
        <input
          type="text"
          placeholder="Trace ID"
          value={traceId}
          onChange={(e) => onTraceIdChange(e.target.value)}
          className="h-7 w-full rounded-sm border border-border bg-background px-2 font-mono text-xs text-foreground placeholder:text-muted-foreground focus:border-foreground/60 focus:outline-none"
        />
      </div>
    </div>
  );
}

function SkeletonList() {
  return (
    <div className="space-y-1 rounded-md border border-border bg-card p-3">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="h-7 animate-pulse rounded-sm bg-surface-2" />
      ))}
    </div>
  );
}