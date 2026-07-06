/**
 * Precomputed cosmetic shading masks for the current floor. A pure function of the map grid —
 * no simulation input — so it can never affect determinism or the replay hash.
 */
export interface TileShade {
  /** Per-cell bitmask: 1=N, 2=E, 4=S, 8=W — which sides border a blocked cell (0 on blocked cells). */
  edges: Uint8Array;
  /** Per-cell brightness bucket 0..3 from a stable integer hash of (x,y): same map, same pattern. */
  variation: Uint8Array;
}

export function computeTileShade(w: number, h: number, blocked: ArrayLike<boolean>): TileShade {
  const edges = new Uint8Array(w * h);
  const variation = new Uint8Array(w * h);
  const isWall = (x: number, y: number) => x < 0 || x >= w || y < 0 || y >= h || !!blocked[y * w + x];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      let hsh = (x * 374761393 + y * 668265263) | 0;
      hsh = Math.imul(hsh ^ (hsh >>> 13), 1274126177);
      variation[i] = ((hsh ^ (hsh >>> 16)) >>> 0) & 3;
      if (blocked[i]) continue;
      let m = 0;
      if (isWall(x, y - 1)) m |= 1;
      if (isWall(x + 1, y)) m |= 2;
      if (isWall(x, y + 1)) m |= 4;
      if (isWall(x - 1, y)) m |= 8;
      edges[i] = m;
    }
  }
  return { edges, variation };
}
