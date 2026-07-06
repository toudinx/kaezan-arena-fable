import { describe, expect, it } from 'vitest';
import { takeNewEvents } from './event-seq';
import type { EventDto } from './types';

const ev = (seq: number): EventDto =>
  ({ kind: 'hit', x: 0, y: 0, toX: 0, toY: 0, value: 0, text: '', actorId: 0, crit: false, seq });

describe('takeNewEvents', () => {
  it('passes everything through on first snapshot', () => {
    const r = takeNewEvents([ev(0), ev(1), ev(2)], -1);
    expect(r.fresh.map((e) => e.seq)).toEqual([0, 1, 2]);
    expect(r.lastSeq).toBe(2);
  });

  it('drops events already ingested (replay window overlap)', () => {
    const r = takeNewEvents([ev(1), ev(2), ev(3)], 2);
    expect(r.fresh.map((e) => e.seq)).toEqual([3]);
    expect(r.lastSeq).toBe(3);
  });

  it('keeps the cursor when nothing is new', () => {
    const r = takeNewEvents([ev(1), ev(2)], 5);
    expect(r.fresh).toEqual([]);
    expect(r.lastSeq).toBe(5);
  });
});
