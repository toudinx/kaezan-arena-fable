import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { loadMap, cropTiles } from "../lib/otbm.mjs";
import { spawnsInBBox } from "../lib/spawns.mjs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("loadMap indexes tiles with ground ids", () => {
  const index = loadMap(config);
  assert.ok(index.size > 1_000_000, `expected a big world, got ${index.size} tiles`);
});

test("cropTiles returns w*h cells", () => {
  const index = loadMap(config);
  const crop = cropTiles(index, { x: 32360, y: 32210, z: 7, w: 10, h: 10 });
  assert.equal(crop.length, 100);
  assert.ok(crop.some(t => t !== null && t.ground > 0), "expected at least one ground tile");
});

test("spawnsInBBox returns species counts for a populated region", () => {
  const spawns = spawnsInBBox(config.monsterXml, { x: 31360, y: 31210, z: 7, w: 2000, h: 2000 });
  assert.ok(spawns.length > 0, "expected monster spawns around Thais");
});
