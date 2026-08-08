import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import type { ServiceSummary } from '@/shared/api/types';
import { PageTemplate } from '@/shared/ui/app-shell';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { cn } from '@/shared/lib/utils';
import { DEFAULT_TIME_RANGE, resolveTimeWindow } from '@/shared/lib/search-params';

export function ServicesPage() {
  const { t } = useTranslation();
  useDocumentTitle('Services');

  // A raw Date.now() here lands in the query key, so every render was a cache
  // miss: the skeleton never cleared and requests fired in a loop. The window is
  // quantized instead — stable between renders, and still advancing.
  const { startUnixMs, endUnixMs } = useMemo(() => resolveTimeWindow(DEFAULT_TIME_RANGE), []);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['services', { startUnixMs, endUnixMs }],
    queryFn: ({ signal }) => api.listServices({ startUnixMs, endUnixMs }, signal),
    refetchInterval: 60_000,
  });

  return (
    <PageTemplate
      title={t('services.title')}
      subtitle={`${data?.length ?? 0} services · last 1h`}
    >
      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load services: {(error as Error).message}
        </div>
      )}

      {isLoading ? (
        <SkeletonTable />
      ) : data && data.length === 0 ? (
        <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
          {t('services.empty.title')}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {data?.map((service) => (
            <ServiceCard key={service.name} service={service} />
          ))}
        </div>
      )}
    </PageTemplate>
  );
}

function ServiceCard({ service }: { service: ServiceSummary }) {
  const errorRate = service.spanCount > 0 ? (service.errorCount / service.spanCount) * 100 : 0;
  const { startUnixMs, endUnixMs } = useMemo(() => resolveTimeWindow(DEFAULT_TIME_RANGE), []);
  const red = useQuery({
    queryKey: ['service-red', service.name, { startUnixMs, endUnixMs }],
    queryFn: ({ signal }) =>
      api.getServiceRed(service.name, { startUnixMs, endUnixMs }, signal),
    staleTime: 60_000,
  });

  return (
    <div className="rounded-md border border-border bg-card p-4">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="text-sm font-semibold text-foreground">{service.name}</h3>
        <span
          className={cn(
            'rounded-sm px-2 py-0.5 text-xs',
            errorRate > 5
              ? 'bg-err-soft text-err-ink'
              : errorRate > 1
                ? 'bg-warn-soft text-warn-ink'
                : 'bg-ok-soft text-ok-ink',
          )}
        >
          {errorRate.toFixed(1)}% err
        </span>
      </div>

      {red.data && (
        <div className="mb-3 grid grid-cols-3 gap-2 text-xs">
          <div>
            <div className="text-muted-foreground">Rate</div>
            <div className="font-mono text-foreground">
              {red.data.requestRatePerSec == null
                ? '—'
                : `${red.data.requestRatePerSec.toFixed(1)}/s`}
            </div>
          </div>
          <div>
            <div className="text-muted-foreground">p95</div>
            <div className="font-mono text-foreground">
              {red.data.durationP95Ms == null ? '—' : `${Math.round(red.data.durationP95Ms)}ms`}
            </div>
          </div>
          <div>
            <div className="text-muted-foreground">Source</div>
            <div className="font-mono text-foreground">{red.data.source}</div>
          </div>
        </div>
      )}

      <div className="mb-3 grid grid-cols-2 gap-2 text-xs">
        <div>
          <div className="text-muted-foreground">Spans</div>
          <div className="font-mono text-foreground">{service.spanCount.toLocaleString()}</div>
        </div>
        <div>
          <div className="text-muted-foreground">Errors</div>
          <div className="font-mono text-foreground">{service.errorCount.toLocaleString()}</div>
        </div>
      </div>

      <div className="space-y-1">
        <div className="text-xs text-muted-foreground">Top operations</div>
        {service.operations.slice(0, 3).map((op) => (
          <div key={op.name} className="flex items-center justify-between text-xs">
            <span className="truncate text-foreground">{op.name}</span>
            <span className="font-mono text-muted-foreground">{op.count.toLocaleString()}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function SkeletonTable() {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="h-40 animate-pulse rounded-md border border-border bg-card" />
      ))}
    </div>
  );
}