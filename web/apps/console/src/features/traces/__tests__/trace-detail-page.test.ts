import { describe, expect, it } from 'vitest';
import { buildWaterfallTree, logsForSpan } from '../trace-detail-page';
import type { LogEntry, Span } from '@/shared/api/types';

/**
 * These two functions carry the only non-presentational logic on the request
 * view: the span tree the waterfall renders, and the join that answers "which
 * logs belong to this span". Everything else on the page is layout. Until now
 * the front-end's only tests covered duration and timestamp formatting, so a
 * regression in either of these would have surfaced as a wrong picture rather
 * than a failing build.
 */

const RESOURCE = {
  serviceName: 'svc',
  serviceNamespace: null,
  deploymentEnvironment: null,
  attributes: {},
};

function span(partial: Partial<Span> & Pick<Span, 'spanId'>): Span {
  return {
    parentSpanId: null,
    service: 'svc',
    operation: 'op',
    startTime: 1_000,
    durationMs: 10,
    status: 'ok',
    kind: 'internal',
    resource: RESOURCE,
    tags: {},
    events: [],
    ...partial,
  };
}

function entry(partial: Partial<LogEntry> = {}): LogEntry {
  return {
    timestamp: 1_000,
    level: 'info',
    service: 'svc',
    traceId: 'trace',
    spanId: null,
    message: 'message',
    fields: {},
    ...partial,
  };
}

describe('buildWaterfallTree', () => {
  it('nests children under their parent and stamps depth', () => {
    const tree = buildWaterfallTree(
      [
        span({ spanId: 'root' }),
        span({ spanId: 'child', parentSpanId: 'root' }),
        span({ spanId: 'grandchild', parentSpanId: 'child' }),
      ],
      1_000,
    );

    expect(tree).toHaveLength(1);
    expect(tree[0]?.depth).toBe(0);
    expect(tree[0]?.children?.[0]?.id).toBe('child');
    expect(tree[0]?.children?.[0]?.depth).toBe(1);
    expect(tree[0]?.children?.[0]?.children?.[0]?.depth).toBe(2);
  });

  it('computes start offsets relative to the trace start, not absolute time', () => {
    const tree = buildWaterfallTree([span({ spanId: 'root', startTime: 1_350 })], 1_000);

    expect(tree[0]?.startOffsetMs).toBe(350);
  });

  it('lifts an orphan to a root instead of dropping it', () => {
    // A span whose parent was sampled away or arrived in a later batch. Losing
    // it would silently understate the trace.
    const tree = buildWaterfallTree(
      [span({ spanId: 'root' }), span({ spanId: 'orphan', parentSpanId: 'missing' })],
      1_000,
    );

    expect(tree.map((node) => node.id).sort()).toEqual(['orphan', 'root']);
  });

  it('keeps every span when several roots exist', () => {
    const tree = buildWaterfallTree(
      [span({ spanId: 'a' }), span({ spanId: 'b' }), span({ spanId: 'b-child', parentSpanId: 'b' })],
      1_000,
    );

    const count = (nodes: ReturnType<typeof buildWaterfallTree>): number =>
      nodes.reduce((total, node) => total + 1 + count(node.children ?? []), 0);

    expect(count(tree)).toBe(3);
  });

  it('returns nothing for an empty span list', () => {
    expect(buildWaterfallTree([], 1_000)).toEqual([]);
  });
});

describe('logsForSpan', () => {
  const logs = [
    entry({ spanId: 'a', message: 'in a' }),
    entry({ spanId: 'b', message: 'in b' }),
    entry({ spanId: 'a', message: 'also in a' }),
    entry({ spanId: null, message: 'no span' }),
  ];

  it('returns every log when no span is selected', () => {
    expect(logsForSpan(logs, undefined)).toHaveLength(4);
  });

  it('narrows to the logs carrying the selected span id', () => {
    expect(logsForSpan(logs, 'a').map((log) => log.message)).toEqual(['in a', 'also in a']);
  });

  it('never attributes a log without a span id to a span', () => {
    // The alternative — falling back to a time window — is the guessing this
    // product exists to avoid.
    expect(logsForSpan(logs, 'a').some((log) => log.spanId === null)).toBe(false);
  });

  it('returns nothing for a span that emitted no logs', () => {
    expect(logsForSpan(logs, 'unlogged')).toEqual([]);
  });
});
