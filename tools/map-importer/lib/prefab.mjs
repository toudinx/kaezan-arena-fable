import { readFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { loadMap, cropTiles, loadAppearanceFlags } from "./otbm.mjs";
import { spawnsInBBox } from "./spawns.mjs";

// Chest appearance ids become POIs (the engine draws chests itself), never decor.
// 2472 = poi.chest, 2478 = poi.treasure (see tools/AssetExtractor/content-config.json).
const CHEST_IDS = new Set([2472, 2478]);

// buildPrefab(config, entry) -> { prefab, gaps }
// entry: { id, role, tier, theme, x, y, z, w, h, mouths? }
// gaps: { missingSpecies: string[], missingSprites: number[], fatal: string[] }
export function buildPrefab(config, entry) {
  const flags = loadAppearanceFlags(config.flags);
  const index = loadMap(config);
  const tiles = cropTiles(index, entry);
  const { w, h } = entry;
  const cellCount = w * h;

  const ground = new Array(cellCount).fill(0);
  const wall = new Array(cellCount).fill(0);
  const decor = new Array(cellCount).fill(0);
  const blocked = new Array(cellCount).fill(0);
  const chestCells = new Set();

  for (let i = 0; i < cellCount; i++) {
    const tile = tiles[i];
    if (!tile || tile.ground === 0) {
      // Void: the generator paints bedrock around the stamp.
      blocked[i] = 1;
      continue;
    }
    ground[i] = tile.ground;
    if (flags.get(tile.ground)?.unpass === true) blocked[i] = 1; // impassable ground (lava, deep water)
    for (const id of tile.items) {
      if (CHEST_IDS.has(id)) {
        chestCells.add(i);
        continue;
      }
      const f = flags.get(id);
      if (f?.unpass === true && f?.ground !== true) {
        wall[i] = id;
        blocked[i] = 1;
      } else {
        // Last item wins; pure top/clip ids (border splashes) stay decor too.
        decor[i] = id;
      }
    }
  }

  keepLargestOpenComponent(blocked, wall, w, h);

  const mouths = Array.isArray(entry.mouths) && entry.mouths.length > 0
    ? entry.mouths.map(m => ({ x: m.x, y: m.y }))
    : findMouths(blocked, w, h);

  const chests = [...chestCells]
    .filter(i => blocked[i] === 0)
    .sort((a, b) => a - b)
    .map(i => ({ x: i % w, y: (i / w) | 0 }));

  const gaps = { missingSpecies: [], missingSprites: [], fatal: [] };
  if (mouths.length === 0) gaps.fatal.push("no open border cell available as mouth");

  const spawnTheme = resolveSpawnTheme(config, entry, gaps);
  if (entry.role === "mob" && spawnTheme.length === 0) {
    gaps.fatal.push("mob prefab has no species present in monsters.json");
  }

  collectMissingSprites(config, [ground, wall, decor], gaps);

  const prefab = {
    id: entry.id,
    role: entry.role,
    tier: entry.tier,
    theme: entry.theme,
    w,
    h,
    ground,
    wall,
    decor,
    blocked,
    mouths,
    chests,
    spawnTheme,
    source: { map: "otservbr", x: entry.x, y: entry.y, z: entry.z }
  };
  return { prefab, gaps };
}

// Keep only the largest 4-connected open component; stray open cells become
// blocked with the dominant wall id of their 8-neighborhood (mirrors the
// DungeonGenerator flood-fill contract: every open cell must be reachable).
function keepLargestOpenComponent(blocked, wall, w, h) {
  const cellCount = w * h;
  const component = new Array(cellCount).fill(-1);
  const sizes = [];

  for (let start = 0; start < cellCount; start++) {
    if (blocked[start] === 1 || component[start] !== -1) continue;
    const id = sizes.length;
    let size = 0;
    const stack = [start];
    component[start] = id;
    while (stack.length > 0) {
      const i = stack.pop();
      size++;
      const x = i % w, y = (i / w) | 0;
      for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
        const ni = ny * w + nx;
        if (blocked[ni] === 0 && component[ni] === -1) {
          component[ni] = id;
          stack.push(ni);
        }
      }
    }
    sizes.push(size);
  }
  if (sizes.length <= 1) return;

  let largest = 0;
  for (let id = 1; id < sizes.length; id++) {
    if (sizes[id] > sizes[largest]) largest = id;
  }
  for (let i = 0; i < cellCount; i++) {
    if (component[i] !== -1 && component[i] !== largest) {
      blocked[i] = 1;
      wall[i] = dominantNeighborWall(wall, blocked, i, w, h);
    }
  }
}

function dominantNeighborWall(wall, blocked, i, w, h) {
  const x = i % w, y = (i / w) | 0;
  const counts = new Map();
  for (let dy = -1; dy <= 1; dy++) {
    for (let dx = -1; dx <= 1; dx++) {
      if (dx === 0 && dy === 0) continue;
      const nx = x + dx, ny = y + dy;
      if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
      const id = wall[ny * w + nx];
      if (id !== 0) counts.set(id, (counts.get(id) ?? 0) + 1);
    }
  }
  let best = 0, bestCount = 0;
  for (const [id, count] of [...counts.entries()].sort((a, b) => a[0] - b[0])) {
    if (count > bestCount) { best = id; bestCount = count; }
  }
  return best; // 0 = void; the generator paints bedrock there
}

// One mouth per contiguous run of open border cells (midpoint of the run),
// walking the border clockwise from the top-left corner.
function findMouths(blocked, w, h) {
  const border = [];
  for (let x = 0; x < w; x++) border.push([x, 0]);
  for (let y = 1; y < h; y++) border.push([w - 1, y]);
  for (let x = w - 2; x >= 0; x--) border.push([x, h - 1]);
  for (let y = h - 2; y >= 1; y--) border.push([0, y]);

  const mouths = [];
  let run = [];
  const flush = () => {
    if (run.length > 0) {
      const [x, y] = run[(run.length / 2) | 0];
      mouths.push({ x, y });
      run = [];
    }
  };
  for (const [x, y] of border) {
    if (blocked[y * w + x] === 0) run.push([x, y]);
    else flush();
  }
  flush();

  // The border walk is a cycle: merge a run that wraps past the start corner.
  if (mouths.length > 1) {
    const first = border[0], last = border[border.length - 1];
    const open = ([x, y]) => blocked[y * w + x] === 0;
    if (open(first) && open(last)) mouths.pop();
  }
  return mouths;
}

function resolveSpawnTheme(config, entry, gaps) {
  const spawns = spawnsInBBox(resolvePath(config.monsterXml), entry);
  const species = JSON.parse(readFileSync(resolvePath(config.monstersJson), "utf8"));
  const known = new Map(species.map(s => [s.name.toLowerCase(), s.name]));

  const present = [];
  for (const { name } of spawns) {
    const canonical = known.get(name.toLowerCase());
    if (canonical) {
      if (!present.includes(canonical)) present.push(canonical);
    } else if (!gaps.missingSpecies.includes(name)) {
      gaps.missingSpecies.push(name);
    }
  }
  return present;
}

function collectMissingSprites(config, grids, gaps) {
  const manifest = JSON.parse(readFileSync(resolvePath(config.manifest), "utf8"));
  const knownIds = new Set(Object.keys(manifest.objects ?? {}).map(Number));
  const missing = new Set();
  for (const grid of grids) {
    for (const id of grid) {
      if (id !== 0 && !knownIds.has(id)) missing.add(id);
    }
  }
  gaps.missingSprites = [...missing].sort((a, b) => a - b);
}

function resolvePath(p) {
  return isAbsolute(p) ? p : resolve(process.cwd(), p);
}
