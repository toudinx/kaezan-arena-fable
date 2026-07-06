import { describe, expect, it } from 'vitest';
import { PerfRing } from './perf-ring';

describe('PerfRing', () => {
  it('reads percentiles from known samples', () => {
    const ring = new PerfRing(300);
    for (let i = 1; i <= 100; i++) ring.add(i);
    expect(ring.percentile(50)).toBe(50);
    expect(ring.percentile(95)).toBe(95);
  });

  it('wraps at capacity keeping the latest samples', () => {
    const ring = new PerfRing(10);
    for (let i = 0; i < 25; i++) ring.add(i);
    expect(ring.percentile(100)).toBe(24);
    expect(ring.percentile(0)).toBeGreaterThanOrEqual(15);
  });

  it('reads 0 when empty', () => {
    expect(new PerfRing(10).percentile(95)).toBe(0);
  });
});
