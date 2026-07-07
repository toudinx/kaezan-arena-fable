import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { buildTilesets, canonical } from "../lib/tilesets.mjs";
import { loadMap, cropTiles, loadAppearanceFlags } from "../lib/otbm.mjs";

// Prediction gate: the translated tilesets must predict the real otservbr map.
// For every mountain-family cell we predict the rock face from the blob mask of open
// neighbours and require it on the real tile; for every open cell at a family seam we
// predict the border pieces and require them on the real tile. If accuracy drops below
// 95% the edge->mask convention is wrong (N/S or corner/diagonal inversion) — fix the
// translation in lib/tilesets.mjs before anything downstream.

const config = JSON.parse(readFileSync(new URL("../config.json", import.meta.url), "utf8"));

// Two wilderness regions with long mountain/grass/dirt seams (otservbr mainland,
// surface level): NE of Thais towards the Mount Sternum foothills, and the mountain
// range south of Thais. Both were picked for having little hand-built construction
// (towns/roads suppress auto-borders and would only add noise to the gate).
const REGIONS = [
  { x: 32448, y: 32032, z: 7, w: 64, h: 64 },
  { x: 32320, y: 32704, z: 7, w: 64, h: 64 },
];

// Neighbour offsets in canonical blob bit order (bit 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW).
const NEIGH = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];

// The otservbr map predates the current RME art in two documented ways:
// 1. Legacy rock faces: the classic mountain inner faces existed as 1081 (e) / 1082 (s)
//    before the current 4815/4816 art; both generations share 1085 for the SE wrap corner.
const LEGACY_FACES = new Map([[4815, [1081]], [4816, [1082]]]);
// 2. Mappers often toggle RME's *optional* gravel foot border (borders.xml ids 29/51),
//    which REPLACES the standard mountain foot pieces (border id 10) on the open tile.
//    Listed here in canonical edge naming, next to the legacy foot pieces (1083-1086,
//    absent from today's RME data; 1086 is the old wrap piece reused along plain edges
//    by old hand-mapping).
const MOUNTAIN_FOOT_VARIANTS_EXTRA = [
  { n: 4460, e: 4457, s: 4458, w: 4461, cnw: 4465, cne: 4467, csw: 4466, cse: 4468, dnw: 4464, dne: 4463, dsw: 4462, dse: 4459 },
  { e: 1083, s: 1084, cse: 1085 },
  { e: 1086, s: 1086, cse: 1086 },
];

// A wrap corner (or lone diagonal) piece may be substituted by its constituent plain
// edges when an art variant lacks the combined piece (RME decomposes exactly like this
// when the optional gravel set has no corner sprite).
const EDGE_PARTS = {
  cnw: ["n", "w"], cne: ["n", "e"], csw: ["s", "w"], cse: ["s", "e"],
  dnw: ["n", "w"], dne: ["n", "e"], dsw: ["s", "w"], dse: ["s", "e"],
};

// Ramps and stairs (RME stairs tileset) replace borders and faces wholesale; cells
// carrying one are legitimate exceptions, not translation misses.
const RAMPS = new Set([1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958]);

// Mirrors the border resolution the engine painter will use (plan Task 8): concave
// corners swallow their two edges, remaining lone edges emit, then lone diagonals.
function resolveBorderEdges(maskOfB) {
  const edges = [];
  const n = maskOfB & 1, e = maskOfB & 4, s = maskOfB & 16, w = maskOfB & 64;
  if (n && w) edges.push("cnw");
  if (n && e) edges.push("cne");
  if (s && w) edges.push("csw");
  if (s && e) edges.push("cse");
  if (n && !w && !e) edges.push("n");
  if (s && !w && !e) edges.push("s");
  if (w && !n && !s) edges.push("w");
  if (e && !n && !s) edges.push("e");
  if (!n && !w && (maskOfB & 128)) edges.push("dnw");
  if (!n && !e && (maskOfB & 2)) edges.push("dne");
  if (!s && !e && (maskOfB & 8)) edges.push("dse");
  if (!s && !w && (maskOfB & 32)) edges.push("dsw");
  return edges;
}

function acceptableItems(edge, variants) {
  const out = new Set();
  for (const variant of variants) {
    if (variant[edge] !== undefined) out.add(variant[edge]);
    for (const part of EDGE_PARTS[edge] ?? []) {
      if (variant[part] !== undefined) out.add(variant[part]);
    }
  }
  return out;
}

function formatMisses(label, misses) {
  const lines = [`${label} misses grouped by case:`];
  const sorted = [...misses.entries()].sort((a, b) => b[1].length - a[1].length);
  for (const [key, cells] of sorted.slice(0, 15)) {
    lines.push(`  ${key} (${cells.length}): ${cells.slice(0, 3).join(" | ")}`);
  }
  return lines.join("\n");
}

test("translated tilesets predict the real map (>=95%)", () => {
  const { tilesets } = buildTilesets(config);
  const index = loadMap(config);
  const flags = loadAppearanceFlags(config.flags);

  const familyOfItem = new Map();
  for (const [name, fam] of Object.entries(tilesets.families)) {
    for (const id of fam.items) familyOfItem.set(id, { name, kind: fam.kind, zOrder: fam.zOrder });
  }
  const mountainGrounds = new Set();
  for (const fam of Object.values(tilesets.families)) {
    if (fam.kind === "mountain") for (const id of fam.items) mountainGrounds.add(id);
  }

  const isWalkable = (tile) => {
    if (!tile || tile.ground === 0) return false;
    if (flags.get(tile.ground)?.unpass === true) return false;
    return !tile.items.some((id) => flags.get(id)?.unpass === true);
  };

  let wallHits = 0, wallTotal = 0, borderHits = 0, borderTotal = 0;
  const wallMisses = new Map(), borderMisses = new Map();
  const miss = (map, key, detail) => {
    if (!map.has(key)) map.set(key, []);
    map.get(key).push(detail);
  };

  for (const region of REGIONS) {
    const crop = cropTiles(index, region);
    const at = (x, y) => crop[y * region.w + x];
    for (let y = 1; y < region.h - 1; y++) {
      for (let x = 1; x < region.w - 1; x++) {
        const tile = at(x, y);
        if (!tile || tile.ground === 0) continue;
        const fam = familyOfItem.get(tile.ground);
        if (!fam) continue;
        const stack = [tile.ground, ...tile.items];
        if (stack.some((id) => RAMPS.has(id))) continue;
        const coord = `${region.x + x},${region.y + y}`;

        if (fam.kind === "mountain") {
          // Two mask readings are both legitimate: the game's (open = walkable, items
          // included) and RME auto-border's (open = foreign passable ground). They differ
          // where gravel items block an otherwise open ground; accept either prediction.
          let maskWalk = 0, maskGround = 0, unknown = false;
          for (let b = 0; b < 8; b++) {
            const nt = at(x + NEIGH[b][0], y + NEIGH[b][1]);
            if (!nt || nt.ground === 0) { unknown = true; break; }
            if (isWalkable(nt)) maskWalk |= 1 << b;
            if (!mountainGrounds.has(nt.ground) && flags.get(nt.ground)?.unpass !== true) maskGround |= 1 << b;
          }
          if (unknown) continue;
          const wallSet = tilesets.wallSets[fam.name];
          if (!wallSet) continue;
          const body = tilesets.families[fam.name].items[0];
          const predictions = [...new Set([canonical(maskWalk), canonical(maskGround)])]
            .map((mask) => wallSet[mask])
            .filter((item) => item !== undefined);
          // No face expected by either reading (N/W exposure = bare body): nothing to score.
          if (predictions.length === 0 || predictions.every((item) => item === body)) continue;
          wallTotal++;
          const hit = predictions.some((item) =>
            item === body || stack.includes(item) || (LEGACY_FACES.get(item) ?? []).some((a) => stack.includes(a)));
          if (hit) wallHits++;
          else miss(wallMisses, `${fam.name} mask ${canonical(maskWalk)}/${canonical(maskGround)} -> ${predictions.join("|")}`,
            `${coord} stack=[${stack.join(" ")}]`);
        } else {
          // Border pass: families with higher z-order draw their border over this cell.
          const neighbourMasks = new Map();
          let unknown = false;
          for (let b = 0; b < 8; b++) {
            const nt = at(x + NEIGH[b][0], y + NEIGH[b][1]);
            if (!nt || nt.ground === 0) { unknown = true; break; }
            const nf = familyOfItem.get(nt.ground);
            if (!nf || nf.name === fam.name || nf.zOrder <= fam.zOrder) continue;
            neighbourMasks.set(nf.name, (neighbourMasks.get(nf.name) ?? 0) | (1 << b));
          }
          if (unknown) continue;
          for (const [bName, maskOfB] of neighbourMasks) {
            const bFam = tilesets.families[bName];
            const set = tilesets.borderSets[`${bName}->${fam.name}`]
              ?? tilesets.borderSets[bFam.kind === "mountain" ? `${bName}->OPEN` : `${bName}->none`];
            if (!set) continue;
            const variants = bName === "mountain" ? [set, ...MOUNTAIN_FOOT_VARIANTS_EXTRA] : [set];
            for (const edge of resolveBorderEdges(maskOfB)) {
              const acceptable = acceptableItems(edge, variants);
              if (acceptable.size === 0) continue;
              borderTotal++;
              if (stack.some((id) => acceptable.has(id))) borderHits++;
              else miss(borderMisses, `${bName}->${fam.name} ${edge}`, `${coord} stack=[${stack.join(" ")}]`);
            }
          }
        }
      }
    }
  }

  // Sample floors keep the gate meaningful: a wrong region pick cannot pass vacuously.
  assert.ok(wallTotal >= 300, `expected 300+ wall samples, got ${wallTotal}`);
  assert.ok(borderTotal >= 800, `expected 800+ border samples, got ${borderTotal}`);

  const wallAccuracy = wallHits / wallTotal;
  const borderAccuracy = borderHits / borderTotal;
  console.log(`prediction gate: walls ${wallHits}/${wallTotal} (${(wallAccuracy * 100).toFixed(1)}%), `
    + `borders ${borderHits}/${borderTotal} (${(borderAccuracy * 100).toFixed(1)}%)`);
  assert.ok(wallAccuracy >= 0.95,
    `wall faces: ${wallHits}/${wallTotal} = ${(wallAccuracy * 100).toFixed(1)}%\n${formatMisses("wall", wallMisses)}`);
  assert.ok(borderAccuracy >= 0.95,
    `border pieces: ${borderHits}/${borderTotal} = ${(borderAccuracy * 100).toFixed(1)}%\n${formatMisses("border", borderMisses)}`);
});
