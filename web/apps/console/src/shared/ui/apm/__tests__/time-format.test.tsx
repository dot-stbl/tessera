import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TimeFormat, formatAbsolute, formatRelative } from '../time-format';

describe('formatAbsolute', () => {
  it('formats UTC milliseconds as ISO 8601', () => {
    const ms = Date.UTC(2026, 0, 19, 14, 8, 21); // Jan 19 2026 14:08:21 UTC
    expect(formatAbsolute(new Date(ms))).toBe('2026-01-19T14:08:21.000Z');
  });

  it('formats date only', () => {
    const ms = Date.UTC(2026, 0, 19);
    expect(formatAbsolute(new Date(ms), 'date')).toBe('2026-01-19');
  });

  it('formats time only', () => {
    const ms = Date.UTC(2026, 0, 19, 14, 8, 21);
    expect(formatAbsolute(new Date(ms), 'time')).toBe('14:08:21');
  });
});

describe('formatRelative', () => {
  const now = new Date('2026-01-19T14:00:00Z');

  it('returns "just now" for sub-second diffs', () => {
    const date = new Date('2026-01-19T13:59:59.500Z');
    expect(formatRelative(date, now)).toBe('just now');
  });

  it('returns seconds for < 1 minute', () => {
    const date = new Date('2026-01-19T13:59:30Z');
    expect(formatRelative(date, now)).toBe('30 sec ago');
  });

  it('returns minutes for < 1 hour', () => {
    const date = new Date('2026-01-19T13:55:00Z');
    expect(formatRelative(date, now)).toBe('5 min ago');
  });

  it('returns hours for < 1 day', () => {
    const date = new Date('2026-01-19T11:00:00Z');
    expect(formatRelative(date, now)).toBe('3 hr ago');
  });

  it('returns days for < 30 days', () => {
    const date = new Date('2026-01-15T14:00:00Z');
    expect(formatRelative(date, now)).toBe('4 days ago');
  });

  it('returns date for >= 30 days', () => {
    const date = new Date('2025-11-01T14:00:00Z');
    expect(formatRelative(date, now)).toBe('2025-11-01');
  });
});

describe('TimeFormat component', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-01-19T14:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders absolute ISO 8601 by default', () => {
    const ms = Date.UTC(2026, 0, 19, 13, 55);
    render(<TimeFormat ms={ms} />);
    expect(screen.getByText('2026-01-19T13:55:00.000Z')).toBeInTheDocument();
  });

  it('renders relative time when relative=true', () => {
    const ms = new Date('2026-01-19T13:55:00Z').getTime();
    render(<TimeFormat ms={ms} relative />);
    expect(screen.getByText('5 min ago')).toBeInTheDocument();
  });

  it('has tooltip with full ISO time', () => {
    const ms = Date.UTC(2026, 0, 19, 14, 0, 0);
    render(<TimeFormat ms={ms} />);
    const el = screen.getByText('2026-01-19T14:00:00.000Z');
    expect(el.getAttribute('title')).toBe('2026-01-19T14:00:00.000Z');
  });

  it('applies time-relative class when in relative mode', () => {
    const ms = new Date('2026-01-19T13:55:00Z').getTime();
    const { container } = render(<TimeFormat ms={ms} relative />);
    const span = container.querySelector('span');
    expect(span?.className).toContain('time-relative');
  });
});