import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { buildTilesets, canonical, CANONICAL_MASKS } from "../lib/tilesets.mjs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("buildTilesets emits families, border sets and 47-slot wall sets", () => {
  const { tilesets, report } = buildTilesets(config);
  const grass = tilesets.families["grass"];
  assert.ok(grass && grass.items.length >= 10);
  assert.ok(tilesets.borderSets["grass->none"], "grass needs a ->none border set");
  const mountainWalls = tilesets.wallSets["mountain"];
  assert.ok(mountainWalls, "mountain wall set must exist");
  const filled = Object.keys(mountainWalls).length;
  assert.ok(filled >= 40, `expected >=40/47 blob cases, got ${filled} (synthetics count)`);
  assert.ok(report.synthetic.length < 20, "too many synthetic wall slots");
});

test("canonical mask list matches the C# WallAutotile blob (47 cases)", () => {
  assert.equal(CANONICAL_MASKS.length, 47);
  // diagonal-only masks collapse to the closed body case
  for (const diagonalOnly of [2, 8, 32, 128]) assert.equal(canonical(diagonalOnly), 0);
  // a diagonal survives only when both adjacent edges are open
  assert.equal(canonical(1 | 4 | 2), 7);
  assert.equal(canonical(1 | 16 | 2), 17);
});

test("wall set slots are canonical and cover every family with a body tile", () => {
  const { tilesets } = buildTilesets(config);
  const canonicalSet = new Set(CANONICAL_MASKS);
  for (const [family, slots] of Object.entries(tilesets.wallSets)) {
    for (const key of Object.keys(slots)) {
      const mask = Number(key);
      assert.ok(Number.isInteger(mask), `${family} wall slot ${key} must be numeric`);
      assert.ok(canonicalSet.has(mask), `${family} wall slot ${key} is not a canonical blob mask`);
    }
    assert.ok(slots["0"], `${family} needs a closed body tile in slot 0`);
    assert.equal(slots["0"], tilesets.families[family].items[0]);
  }
});

test("mountain families emit the ->OPEN foot border and reference only curated families", () => {
  const { tilesets } = buildTilesets(config);
  assert.ok(tilesets.borderSets["mountain->OPEN"], "mountain needs its foot-of-rock ->OPEN set");
  const familyNames = new Set(Object.keys(tilesets.families));
  for (const key of Object.keys(tilesets.borderSets)) {
    const [from, to] = key.split("->");
    assert.ok(familyNames.has(from), `border set ${key}: unknown source family`);
    assert.ok(to === "none" || to === "OPEN" || familyNames.has(to), `border set ${key}: unknown target family`);
  }
});

test("lava is emitted as a bordered ground family", () => {
  const { tilesets } = buildTilesets(config);
  const lava = tilesets.families["lava"];
  assert.ok(lava, "lava must be available as an accent ground family");
  assert.equal(lava.kind, "ground");
  assert.ok(lava.items.length >= 1, "lava needs at least one ground tile");
  assert.ok(tilesets.borderSets["lava->none"], "lava needs an outer border set");
});
