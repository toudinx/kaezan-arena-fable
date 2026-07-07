import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { loadBorders, loadGroundBrushes } from "../lib/rme.mjs";

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

test("loadBorders parses the 12-edge sets", () => {
  const borders = loadBorders(config.rmeMaterials);
  assert.ok(borders.size > 100, `expected 100+ border sets, got ${borders.size}`);
  const b1 = borders.get(1);
  assert.ok(b1, "border id 1 must exist");
  for (const edge of ["n", "e", "s", "w", "cnw", "cne", "csw", "cse", "dnw", "dne", "dsw", "dse"]) {
    assert.ok(Number.isInteger(b1[edge]), `border 1 missing edge ${edge}`);
  }
});

test("loadGroundBrushes parses items, z-order and border refs", () => {
  const brushes = loadGroundBrushes(config.rmeMaterials);
  const grass = brushes.get("grass");
  assert.ok(grass, "grass brush must exist");
  assert.ok(grass.items.length >= 10, "grass has many item variants");
  assert.ok(grass.zOrder > 0);
  assert.ok(grass.borders.some((b) => b.align === "outer"), "grass has an outer border");
  const mountain = brushes.get("mountain");
  assert.ok(mountain, "mountain brush must exist");
  assert.ok(mountain.borders.some((b) => b.align === "inner"), "mountain has inner borders");
});
