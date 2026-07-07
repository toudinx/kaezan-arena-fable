#!/usr/bin/env node
// Translates the RME materials (borders + ground brushes) into the backend tilesets.json.
// Usage: node convert-tilesets.mjs [--report-only]
// Prints the translation report (families, wall-set coverage, synthetic slots, skipped
// border refs) plus a sprite gap report against the frontend manifest. Without
// --report-only writes backend Content/tilesets.json; any missing sprite aborts the
// write (extract the sprites first — AssetExtractor semantic groups).
import { readFileSync, writeFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { buildTilesets } from "./lib/tilesets.mjs";

const reportOnly = process.argv.includes("--report-only");
const config = JSON.parse(readFileSync(new URL("./config.json", import.meta.url), "utf8"));

const { tilesets, report } = buildTilesets(config);

// Stable key order for readable diffs: families/sets sorted, edges in canonical order,
// wall slots numeric.
const EDGE_ORDER = ["n", "e", "s", "w", "cnw", "cne", "csw", "cse", "dnw", "dne", "dsw", "dse"];
const sortKeys = (obj, order) => {
  const keys = order
    ? order.filter((k) => obj[k] !== undefined)
    : Object.keys(obj).sort((a, b) => a.localeCompare(b));
  return Object.fromEntries(keys.map((k) => [k, obj[k]]));
};
const output = {
  families: sortKeys(Object.fromEntries(
    Object.entries(tilesets.families).map(([name, fam]) => [name, { kind: fam.kind, items: fam.items, zOrder: fam.zOrder }]))),
  borderSets: sortKeys(Object.fromEntries(
    Object.entries(tilesets.borderSets).map(([key, set]) => [key, sortKeys(set, EDGE_ORDER)]))),
  wallSets: sortKeys(Object.fromEntries(
    Object.entries(tilesets.wallSets).map(([name, slots]) => [name, Object.fromEntries(
      Object.keys(slots).map(Number).sort((a, b) => a - b).map((mask) => [mask, slots[mask]]))]))),
};

console.log("== FAMILIES ==");
for (const fam of report.families) {
  console.log(`  ${fam.name} (${fam.kind}): ${fam.items} item(s), z-order ${fam.zOrder}`);
}
console.log(`\n== BORDER SETS (${report.borderSets.length}) ==`);
for (const key of report.borderSets) console.log(`  ${key}`);
console.log("\n== WALL SETS ==");
for (const [name, cover] of Object.entries(report.wallSets)) {
  console.log(`  ${name}: ${cover.direct}/47 direct piece slots, ${cover.synthetic} body-fallback slots`);
}
if (Object.keys(report.missingEdges).length > 0) {
  console.log("\n== MISSING EDGES (inner sets without these pieces) ==");
  for (const [name, edges] of Object.entries(report.missingEdges)) console.log(`  ${name}: ${edges.join(", ")}`);
}
if (report.skipped.length > 0) {
  console.log("\n== SKIPPED BORDER REFS ==");
  for (const line of report.skipped) console.log(`  ${line}`);
}

// Sprite gap check against the frontend manifest (same contract as export.mjs).
const manifestPath = isAbsolute(config.manifest) ? config.manifest : resolve(process.cwd(), config.manifest);
const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
const knownIds = new Set(Object.keys(manifest.objects ?? {}).map(Number));
const usedIds = new Set();
for (const fam of Object.values(output.families)) for (const id of fam.items) usedIds.add(id);
for (const set of Object.values(output.borderSets)) for (const id of Object.values(set)) usedIds.add(id);
for (const slots of Object.values(output.wallSets)) for (const id of Object.values(slots)) usedIds.add(id);
const missingSprites = [...usedIds].filter((id) => !knownIds.has(id)).sort((a, b) => a - b);

if (missingSprites.length > 0) {
  console.log(`\n== GAP REPORT ==`);
  console.log(`missing sprite ids (${missingSprites.length}), for AssetExtractor content-config semantic groups:`);
  console.log(missingSprites.join(","));
}

if (reportOnly) {
  console.log(`\nreport-only: ${Object.keys(output.families).length} families, `
    + `${Object.keys(output.borderSets).length} border sets, `
    + `${Object.keys(output.wallSets).length} wall sets, `
    + `${missingSprites.length} missing sprite(s), nothing written`);
  process.exit(0);
}

if (missingSprites.length > 0) {
  console.error(`\nconvert aborted: ${missingSprites.length} sprite(s) missing from the frontend manifest; nothing written`);
  process.exit(1);
}

const outPath = isAbsolute(config.tilesetsOut) ? config.tilesetsOut : resolve(process.cwd(), config.tilesetsOut);
writeFileSync(outPath, JSON.stringify(output, null, 2) + "\n");
console.log(`\nwrote ${outPath}`);

// Task 9 (map beauty): multi-tile appearance extract for the backend decor guard. Any id whose
// sprite spans more than 1x1 tiles gets clipped by the per-cell renderer, so biome Decor/Accent
// palettes must reject them (Biomes.ValidateDefaults). Only the oversized ids are shipped — the
// full flags dump is 3MB and lives outside the backend.
const flagsPath = isAbsolute(config.flags) ? config.flags : resolve(process.cwd(), config.flags);
const flags = JSON.parse(readFileSync(flagsPath, "utf8"));
const multiTile = Object.entries(flags)
  .filter(([, f]) => f.w > 1 || f.h > 1)
  .map(([id]) => Number(id))
  .sort((a, b) => a - b);
const lines = [];
for (let i = 0; i < multiTile.length; i += 20) lines.push("    " + multiTile.slice(i, i + 20).join(", "));
const sizesPath = isAbsolute(config.sizesOut) ? config.sizesOut : resolve(process.cwd(), config.sizesOut);
writeFileSync(sizesPath, `{\n  "multiTile": [\n${lines.join(",\n")}\n  ]\n}\n`);
console.log(`wrote ${sizesPath} (${multiTile.length} multi-tile ids)`);
