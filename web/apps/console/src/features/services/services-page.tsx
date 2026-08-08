import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '@/shared/api';
import type { ServiceSummary } from '@/shared/api/types';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Dns, Percent, Speed, Storage } from '@nine-thirty-five/material-symbols-react/rounded/400';
import { Duration } from '@/shared/ui/apm';
import {
  Blank,
  BlankText,
  Cell,
  Column,
  InlineList,
  Listing,
  ListingBody,
  ListingHead,
  LoadingRows,
  NumCell,
  Row,
  StatBar,
  Subject,
  TrackCell,
} from '@/shared/ui/console';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
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
    <PageTemplate title={t('services.title')} subtitle={`${data?.length ?? 0} services · last 1h`}>
      {isError && (
        <Blank title="Could not read the service inventory.">
          <BlankText>{(error as Error).message}</BlankText>
        </Blank>
      )}

      {!isLoading && !isError && <ServiceStats services={data ?? []} />}

      {isLoading ? (
        <LoadingRows count={8} />
      ) : (
        <ServiceTable services={data ?? []} startUnixMs={startUnixMs} endUnixMs={endUnixMs} />
      )}
    </PageTemplate>
  );
}

/**
 * The inventory's own summary. `failing` counts services rather than spans on
 * purpose: one service at 12% and eleven at zero is a different morning from
 * twelve services all at 1%, and a span-weighted average hides exactly that.
 */
function ServiceStats({ services }: { services: ServiceSummary[] }) {
  const spans = services.reduce((sum, service) => sum + service.spanCount, 0);
  const errors = services.reduce((sum, service) => sum + service.errorCount, 0);
  const failing = services.filter((service) => errorRate(service) >= 1);
  const worst = [...services].sort((a, b) => errorRate(b) - errorRate(a))[0];
  const rate = spans > 0 ? (errors / spans) * 100 : 0;

  return (
    <StatBar
      items={[
        {
          icon: Dns,
          label: 'services',
          value: services.length.toLocaleString(),
          sub:
            failing.length > 0
              ? `${failing.length} above a 1% error rate`
              : 'none above a 1% error rate',
        },
        {
          icon: Storage,
          label: 'spans',
          value: spans.toLocaleString(),
          sub: 'in the last hour',
        },
        {
          icon: Percent,
          label: 'error rate',
          value: `${rate < 10 ? rate.toFixed(2) : rate.toFixed(1)}%`,
          bad: rate >= 1,
          sub: `${errors.toLocaleString()} failing spans`,
        },
        {
          icon: Speed,
          label: 'worst',
          value: worst?.name ?? '—',
          bad: worst !== undefined && errorRate(worst) >= 1,
          sub: worst ? `${errorRate(worst).toFixed(1)}% of its spans fail` : 'nothing failing',
        },
      ]}
    />
  );
}

function ServiceTable({
  services,
  startUnixMs,
  endUnixMs,
}: {
  services: ServiceSummary[];
  startUnixMs: number;
  endUnixMs: number;
}) {
  if (services.length === 0) {
    return (
      <Blank title="No services reported spans in the last hour.">
        <BlankText>
          Services appear here as soon as they export a trace. If one is missing, check that its
          collector endpoint is reachable and that sampling is not set to zero.
        </BlankText>
      </Blank>
    );
  }

  // Deliberately no time gutter here. Every other listing in the app is a stream
  // of events and its first column answers "when"; a service inventory has no
  // per-row moment in time, and inventing one would make the gutter decorative
  // in the one place it means nothing. Failing services still lead.
  const ordered = [...services].sort((a, b) => errorRate(b) - errorRate(a));
  const worst = Math.max(...ordered.map(errorRate), 0.0001);

  return (
    <Listing>
      <ListingHead>
        <Column width={220}>Service</Column>
        <Column width={90} align="right">
          Rate
        </Column>
        <Column width={90} align="right">
          p95
        </Column>
        <Column width={96} align="right">
          Errors
        </Column>
        <Column width={74} align="right">
          Failing
        </Column>
        <Column width={120} />
        <Column width={92} align="right">
          Spans
        </Column>
        <Column>Top operations</Column>
      </ListingHead>
      <ListingBody>
        {ordered.map((service) => (
          <ServiceRow
            key={service.name}
            service={service}
            worstRate={worst}
            startUnixMs={startUnixMs}
            endUnixMs={endUnixMs}
          />
        ))}
      </ListingBody>
    </Listing>
  );
}

function errorRate(service: ServiceSummary): number {
  return service.spanCount > 0 ? (service.errorCount / service.spanCount) * 100 : 0;
}

function ServiceRow({
  service,
  worstRate,
  startUnixMs,
  endUnixMs,
}: {
  service: ServiceSummary;
  worstRate: number;
  startUnixMs: number;
  endUnixMs: number;
}) {
  // RED comes from metrics and is a separate, slower call per service, so it
  // arrives after the inventory rather than blocking it. An em dash means "not
  // answered yet or not available" — never a zero, which would read as a fact.
  const red = useQuery({
    queryKey: ['service-red', service.name, { startUnixMs, endUnixMs }],
    queryFn: ({ signal }) => api.getServiceRed(service.name, { startUnixMs, endUnixMs }, signal),
    staleTime: 60_000,
  });

  const rate = errorRate(service);
  const failing = rate >= 1;

  return (
    <Row status={failing ? 'error' : undefined}>
      <Cell>
        <Subject mark={service.name} operation={service.name} />
      </Cell>
      <NumCell>
        {red.data?.requestRatePerSec == null ? '—' : `${red.data.requestRatePerSec.toFixed(1)}/s`}
      </NumCell>
      <NumCell>
        {red.data?.durationP95Ms == null ? '—' : <Duration ms={red.data.durationP95Ms} />}
      </NumCell>
      <NumCell>{service.errorCount.toLocaleString()}</NumCell>
      {/* The number first, the bar second. A bar alone cannot separate 2.1%
       * from 0.27%, and the bar is ranked against the worst row on screen
       * rather than against 100% — otherwise every healthy service collapses
       * into the same stub and the column stops answering anything. */}
      <NumCell bad={failing}>
        {rate === 0 ? '0' : `${rate < 0.1 ? rate.toFixed(2) : rate.toFixed(1)}%`}
      </NumCell>
      <TrackCell share={rate > 0 ? Math.max((rate / worstRate) * 100, 3) : 0} />
      <NumCell>{service.spanCount.toLocaleString()}</NumCell>
      <Cell>
        <InlineList items={service.operations.map((operation) => operation.name)} />
      </Cell>
    </Row>
  );
}
