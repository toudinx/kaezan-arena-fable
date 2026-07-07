#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { loadAppearanceFlags, loadMap, cropTiles } from "./lib/otbm.mjs";

const config = JSON.parse(readFileSync(new URL("./config.json", import.meta.url), "utf8"));
const args = parseArgs(process.argv.slice(2));
const bbox = {
  x: numberArg(args, "x"),
  y: numberArg(args, "y"),
  z: numberArg(args, "z"),
  w: numberArg(args, "w"),
  h: numberArg(args, "h")
};

const index = loadMap(config);
const flags = loadAppearanceFlags(config.flags);
const crop = cropTiles(index, bbox);

for (let y = 0; y < bbox.h; y++) {
  const row = crop.slice(y * bbox.w, (y + 1) * bbox.w);
  if (args.ids) {
    console.log(row.map(tile => tile?.ground ? String(tile.ground).padStart(5, " ") : "    .").join(" "));
  } else {
    console.log(row.map(tile => asciiTile(tile, flags)).join(""));
  }
}

function asciiTile(tile, flags) {
  if (!tile || tile.ground === 0) return " ";
  const stack = [tile.ground, ...tile.items];
  return stack.some(id => flags.get(id)?.unpass || flags.get(id)?.clip) ? "#" : ".";
}

function parseArgs(argv) {
  const parsed = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith("--")) continue;
    const key = arg.slice(2);
    if (key === "ids") {
      parsed.ids = true;
    } else {
      parsed[key] = argv[++i];
    }
  }
  return parsed;
}

function numberArg(args, key) {
  const value = Number(args[key]);
  if (!Number.isFinite(value)) {
    throw new Error(`missing numeric --${key}`);
  }
  return value;
}
