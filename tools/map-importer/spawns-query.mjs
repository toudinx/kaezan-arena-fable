#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { spawnsInBBox } from "./lib/spawns.mjs";

const config = JSON.parse(readFileSync(new URL("./config.json", import.meta.url), "utf8"));
const args = parseArgs(process.argv.slice(2));
const bbox = {
  x: numberArg(args, "x"),
  y: numberArg(args, "y"),
  z: numberArg(args, "z"),
  w: numberArg(args, "w"),
  h: numberArg(args, "h")
};

const spawns = spawnsInBBox(config.monsterXml, bbox);
for (const spawn of spawns) {
  console.log(`${String(spawn.count).padStart(4, " ")} ${spawn.name}`);
}

function parseArgs(argv) {
  const parsed = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith("--")) continue;
    parsed[arg.slice(2)] = argv[++i];
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
