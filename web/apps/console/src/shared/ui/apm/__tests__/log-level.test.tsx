import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LogLevel, parseLogLevel } from '../log-level';

describe('parseLogLevel', () => {
  it('returns the level for known values (lowercase)', () => {
    expect(parseLogLevel('error')).toBe('error');
    expect(parseLogLevel('info')).toBe('info');
    expect(parseLogLevel('warn')).toBe('warn');
  });

  it('normalizes case (uppercase)', () => {
    expect(parseLogLevel('ERROR')).toBe('error');
    expect(parseLogLevel('Info')).toBe('info');
  });

  it('normalizes aliases (warning → warn, critical → fatal)', () => {
    expect(parseLogLevel('warning')).toBe('warn');
    expect(parseLogLevel('critical')).toBe('fatal');
  });

  it('returns undefined for unknown values', () => {
    expect(parseLogLevel('foobar')).toBeUndefined();
    expect(parseLogLevel('')).toBeUndefined();
    expect(parseLogLevel(null)).toBeUndefined();
    expect(parseLogLevel(undefined)).toBeUndefined();
  });
});

describe('LogLevel component', () => {
  it('renders the level label in uppercase when no children provided', () => {
    render(<LogLevel level="error" />);
    const chip = screen.getByText('ERROR');
    expect(chip).toBeInTheDocument();
  });

  it('applies the correct class for each level', () => {
    const { container } = render(<LogLevel level="warn" />);
    const chip = container.querySelector('.log-level-warn');
    expect(chip).toBeInTheDocument();
  });

  it('uses children when provided instead of uppercased level', () => {
    render(<LogLevel level="error">ERR</LogLevel>);
    expect(screen.getByText('ERR')).toBeInTheDocument();
    expect(screen.queryByText('error')).toBeNull();
  });

  it('applies className prop on top of base classes', () => {
    const { container } = render(<LogLevel level="info" className="ml-2" />);
    const chip = container.querySelector('.log-level');
    expect(chip?.className).toContain('ml-2');
    expect(chip?.className).toContain('log-level-info');
  });
});