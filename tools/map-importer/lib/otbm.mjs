import { readFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const otbm2json = require("../vendor/otbm2json.js");

let cached = null;

class TileIndex {
  #shards = new Map();
  #size = 0;

  get size() {
    return this.#size;
  }

  get(key) {
    return this.#shardForKey(key, false)?.get(key);
  }

  set(key, value) {
    const shard = this.#shardForKey(key, true);
    if (!shard.has(key)) this.#size++;
    shard.set(key, value);
  }

  #shardForKey(key, create) {
    const z = key.slice(key.lastIndexOf(",") + 1);
    let shard = this.#shards.get(z);
    if (!shard && create) {
      shard = new Map();
      this.#shards.set(z, shard);
    }
    return shard;
  }
}

export function loadMap(config) {
  if (cached) return cached;

  const flags = loadFlags(config.flags);
  const data = otbm2json.read(config.otbm);
  const mapData = data?.data?.nodes?.[0];
  const index = new TileIndex();

  const areas = tileAreas(mapData);
  for (const area of areas) {
    const tiles = area.tiles ?? [];
    for (const tile of tiles) {
      const normalized = normalizeTile(tile, flags);
      index.set(`${area.x + tile.x},${area.y + tile.y},${area.z}`, normalized);
    }
    area.tiles = [];
  }

  cached = index;
  return index;
}

export function cropTiles(index, { x, y, z, w, h }) {
  const out = new Array(w * h).fill(null);
  for (let ly = 0; ly < h; ly++) {
    for (let lx = 0; lx < w; lx++) {
      out[ly * w + lx] = index.get(`${x + lx},${y + ly},${z}`) ?? null;
    }
  }
  return out;
}

export function loadAppearanceFlags(flagsPath) {
  return loadFlags(flagsPath);
}

function tileAreas(node) {
  if (!node) return [];
  return (node.features ?? node.nodes ?? []).filter(child => child.type === otbm2json.HEADERS.OTBM_TILE_AREA);
}

function normalizeTile(tile, flags) {
  const ids = (tile.items ?? []).map(item => item.id).filter(id => Number.isInteger(id));
  let ground = tile.tileid ?? 0;
  let items = ids;

  if (ground === 0 && ids.length > 0 && flags.get(ids[0])?.ground === true) {
    ground = ids[0];
    items = ids.slice(1);
  }

  return { ground, items };
}

function loadFlags(flagsPath) {
  const resolved = isAbsolute(flagsPath) ? flagsPath : resolve(process.cwd(), flagsPath);
  const raw = JSON.parse(readFileSync(resolved, "utf8"));
  return new Map(Object.entries(raw).map(([id, value]) => [Number(id), value]));
}
