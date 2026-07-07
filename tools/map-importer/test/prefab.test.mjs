import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { buildPrefab } from "../lib/prefab.mjs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("buildPrefab produces a valid connected prefab", () => {
  // Sub-window of rotworm-cave-sternum (hunt_anatomy.md curated table, line 1).
  const entry = { id: "prefab:test", role: "mob", tier: 1, theme: "cave",
                  x: 32144, y: 32328, z: 9, w: 20, h: 14 };
  const { prefab, gaps } = buildPrefab(config, entry);
  assert.equal(prefab.ground.length, prefab.w * prefab.h);
  assert.equal(prefab.blocked.length, prefab.w * prefab.h);
  assert.ok(prefab.mouths.length >= 1, "needs at least one mouth");
  assert.ok(openCellsConnected(prefab), "open cells must be 4-connected");
  assert.ok(prefab.spawnTheme.length > 0, "mob prefab needs a spawn theme");
  assert.ok(gaps, "gaps report must exist");
});

function openCellsConnected(p) {
  const start = p.blocked.indexOf(0);
  if (start < 0) return false;
  const seen = new Set([start]);
  const stack = [start];
  while (stack.length) {
    const i = stack.pop(), x = i % p.w, y = (i / p.w) | 0;
    for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
      const nx = x + dx, ny = y + dy;
      if (nx < 0 || nx >= p.w || ny < 0 || ny >= p.h) continue;
      const ni = ny * p.w + nx;
      if (!seen.has(ni) && p.blocked[ni] === 0) { seen.add(ni); stack.push(ni); }
    }
  }
  return seen.size === p.blocked.filter(b => b === 0).length;
}
