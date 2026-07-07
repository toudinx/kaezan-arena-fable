#!/usr/bin/env node
// Exports authored prefabs (OTBM crops) to the backend content directory.
// Usage: node export.mjs [--report-only]
// Any missing sprite (or fatal gap) aborts the export with a consolidated
// gap report and exit code 1; missing species are reported but non-fatal.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { isAbsolute, join, resolve } from "node:path";
import { buildPrefab } from "./lib/prefab.mjs";

const reportOnly = process.argv.includes("--report-only");
const config = JSON.parse(readFileSync(new URL("./config.json", import.meta.url), "utf8"));
const entries = JSON.parse(readFileSync(new URL("./prefabs-config.json", import.meta.url), "utf8"));

const results = [];
for (const entry of entries) {
  const { prefab, gaps } = buildPrefab(config, entry);
  results.push({ entry, prefab, gaps });
}

const blocking = results.filter(r => r.gaps.missingSprites.length > 0 || r.gaps.fatal.length > 0);
const withMissingSpecies = results.filter(r => r.gaps.missingSpecies.length > 0);

if (blocking.length > 0 || withMissingSpecies.length > 0) {
  console.log("== GAP REPORT ==");
  for (const { entry, gaps } of results) {
    if (gaps.missingSprites.length === 0 && gaps.missingSpecies.length === 0 && gaps.fatal.length === 0) continue;
    console.log(`\n${entry.id}`);
    for (const reason of gaps.fatal) console.log(`  FATAL: ${reason}`);
    if (gaps.missingSprites.length > 0) console.log(`  missing sprites (${gaps.missingSprites.length}): ${gaps.missingSprites.join(",")}`);
    if (gaps.missingSpecies.length > 0) console.log(`  missing species: ${gaps.missingSpecies.join(", ")}`);
  }
  const allMissingSprites = [...new Set(results.flatMap(r => r.gaps.missingSprites))].sort((a, b) => a - b);
  if (allMissingSprites.length > 0) {
    console.log(`\nall missing sprite ids (${allMissingSprites.length}), for AssetExtractor content-config semantic groups:`);
    console.log(allMissingSprites.join(","));
  }
}

if (reportOnly) {
  console.log(`\nreport-only: ${results.length} prefab(s) built, ${blocking.length} blocked by gaps, nothing written`);
  process.exit(0);
}

if (blocking.length > 0) {
  console.error(`\nexport aborted: ${blocking.length} prefab(s) have missing sprites or fatal gaps; nothing written`);
  process.exit(1);
}

const outDir = isAbsolute(config.prefabsOut) ? config.prefabsOut : resolve(process.cwd(), config.prefabsOut);
mkdirSync(outDir, { recursive: true });
for (const { entry, prefab } of results) {
  const fileName = `${entry.id.replace(/^prefab:/, "")}.json`;
  const outPath = join(outDir, fileName);
  writeFileSync(outPath, JSON.stringify(prefab));
  console.log(`wrote ${outPath} (${prefab.w}x${prefab.h}, ${prefab.mouths.length} mouth(s), theme: ${prefab.spawnTheme.join(", ") || "-"})`);
}
console.log(`${results.length} prefab(s) exported`);
