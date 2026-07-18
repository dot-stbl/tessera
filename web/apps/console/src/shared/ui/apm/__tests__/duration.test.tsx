import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Duration, formatDuration } from '../duration';

describe('formatDuration', () => {
  it('returns nanoseconds for sub-microsecond values', () => {
    expect(formatDuration(0.0005)).toEqual({ value: '500', unit: 'ns' });
  });

  it('returns microseconds for sub-millisecond values', () => {
    expect(formatDuration(0.5)).toEqual({ value: '500.0', unit: 'µs' });
  });

  it('returns milliseconds for sub-second values', () => {
    expect(formatDuration(500)).toEqual({ value: '500', unit: 'ms' });
  });

  it('returns seconds for sub-minute values', () => {
    expect(formatDuration(5_000)).toEqual({ value: '5.0', unit: 's' });
  });

  it('returns minutes for sub-hour values', () => {
    expect(formatDuration(1_800_000)).toEqual({ value: '30.0', unit: 'm' });
  });

  it('returns hours for >= 1 hour values', () => {
    expect(formatDuration(7_200_000)).toEqual({ value: '2.0', unit: 'h' });
  });

  it('handles negative values gracefully', () => {
    expect(formatDuration(-100)).toEqual({ value: '0', unit: 'ms' });
  });
});

describe('Duration component', () => {
  it('renders formatted duration with unit', () => {
    render(<Duration ms={500} />);
    expect(screen.getByText('500')).toBeInTheDocument();
    expect(screen.getByText('ms')).toBeInTheDocument();
  });

  it('applies is-slow class for slow durations', () => {
    const { container } = render(<Duration ms={2000} />);
    const span = container.querySelector('span');
    expect(span?.className).toContain('duration-slow');
  });

  it('applies is-very-slow class for very slow durations', () => {
    const { container } = render(<Duration ms={10_000} />);
    const span = container.querySelector('span');
    expect(span?.className).toContain('duration-very-slow');
  });

  it('uses custom slow threshold', () => {
    const { container } = render(<Duration ms={600} slowThresholdMs={500} />);
    const span = container.querySelector('span');
    expect(span?.className).toContain('duration-slow');
  });

  it('does not apply slow class below threshold', () => {
    const { container } = render(<Duration ms={500} slowThresholdMs={1000} />);
    const span = container.querySelector('span');
    expect(span?.className).not.toContain('duration-slow');
  });
});