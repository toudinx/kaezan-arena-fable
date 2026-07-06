import { describe, expect, it } from 'vitest';
import { computeTileShade } from './tile-shade';

describe('computeTileShade', () => {
  // 3x3: rock ring around one open centre
  const blocked = [
    true, true, true,
    true, false, true,
    true, true, true,
  ];

  it('marks every wall-facing side of an enclosed cell', () => {
    const s = computeTileShade(3, 3, blocked);
    expect(s.edges[4]).toBe(1 | 2 | 4 | 8);
  });

  it('leaves blocked cells unmarked and treats out-of-bounds as wall', () => {
    const open = new Array(4).fill(false); // 2x2 all open
    const s = computeTileShade(2, 2, open);
    expect(s.edges[0]).toBe(1 | 8); // NW cell: map edge above and left
    expect(computeTileShade(3, 3, blocked).edges[0]).toBe(0); // blocked cell: no shading
  });

  it('variation is a stable pure function of coordinates', () => {
    const a = computeTileShade(8, 8, new Array(64).fill(false));
    const b = computeTileShade(8, 8, new Array(64).fill(false));
    expect(a.variation).toEqual(b.variation);
    expect(Array.from(a.variation).some((v) => v !== a.variation[0])).toBe(true);
  });
});
