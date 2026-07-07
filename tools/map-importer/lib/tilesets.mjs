import { readFileSync } from "node:fs";
import { loadBorders, loadGroundBrushes } from "./rme.mjs";

// RME edge name -> blob mask of OPEN neighbours (bit 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW).
// The d* masks are NOT canonical (a lone open diagonal collapses to the closed body case
// in WallAutotile.Canonical), so those edges never occupy a wall-set slot on their own.
export const EDGE_TO_MASK = {
  n: 1, e: 4, s: 16, w: 64,               // one open side
  cnw: 1 | 64, cne: 1 | 4, cse: 4 | 16, csw: 16 | 64, // two open sides (corner)
  dnw: 128, dne: 2, dse: 8, dsw: 32,      // diagonal-only open
};

// Same rule as WallAutotile.Canonical (C#): a diagonal bit survives only when both of its
// adjacent edge bits are open.
export function canonical(mask) {
  const n = mask & 1, e = mask & 4, s = mask & 16, w = mask & 64;
  if (!(n && e)) mask &= ~2;
  if (!(s && e)) mask &= ~8;
  if (!(s && w)) mask &= ~32;
  if (!(n && w)) mask &= ~128;
  return mask;
}

export const CANONICAL_MASKS = [...new Set(Array.from({ length: 256 }, (_, m) => canonical(m)))].sort((a, b) => a - b);

// Piece choice for a canonical mask, in priority order: a concave corner shows two rock
// faces so it beats a single face; south/east come first because Tibia's perspective only
// draws faces on the S/E sides (classic mountain only ships e/s pieces). The first entry
// whose required open edges are present in the mask AND whose piece exists in the inner
// border set wins. Masks resolved this way are principled, not synthetic.
const PIECE_PRIORITY = [
  { edge: "cse", need: 4 | 16 },
  { edge: "csw", need: 16 | 64 },
  { edge: "cne", need: 1 | 4 },
  { edge: "cnw", need: 1 | 64 },
  { edge: "s", need: 16 },
  { edge: "e", need: 4 },
  { edge: "n", need: 1 },
  { edge: "w", need: 64 },
];

const ALL_EDGES = Object.keys(EDGE_TO_MASK);

function popcount(v) {
  let count = 0;
  while (v) { v &= v - 1; count++; }
  return count;
}

// Prefer the generic rule (no `to`), then the explicit "none" rule; pair-specific rules
// (e.g. mountain inner to="icy mountain") are transitions between wall families we skip.
function pickBorderRef(brush, align) {
  const generic = brush.borders.find((b) => b.align === align && b.to === null);
  if (generic) return generic;
  return brush.borders.find((b) => b.align === align && b.to === "none") ?? null;
}

function buildWallSet(name, brush, edges, report) {
  const slots = {};
  for (const mask of CANONICAL_MASKS) {
    if (mask === 0) {
      slots[mask] = brush.items[0]; // closed case = mountain body tile
      continue;
    }
    const piece = PIECE_PRIORITY.find((p) => (mask & p.need) === p.need && edges[p.edge] !== undefined);
    if (piece !== undefined) slots[mask] = edges[piece.edge];
  }

  // Remaining slots (masks whose visible faces have no piece in this set, e.g. N/W-only
  // exposure on the classic mountain) borrow from the nearest filled mask by Hamming
  // distance on the blob bits, tiebreak lower mask. Recorded as synthetic.
  const unfilled = CANONICAL_MASKS.filter((mask) => slots[mask] === undefined);
  const donors = CANONICAL_MASKS.filter((mask) => slots[mask] !== undefined);
  for (const mask of unfilled) {
    let best = -1;
    let bestDist = Number.MAX_SAFE_INTEGER;
    for (const donor of donors) {
      const dist = popcount(mask ^ donor);
      if (dist < bestDist || (dist === bestDist && donor < best)) {
        bestDist = dist;
        best = donor;
      }
    }
    slots[mask] = slots[best];
    report.synthetic.push({ family: name, mask, from: best });
  }

  const missing = ALL_EDGES.filter((edge) => edges[edge] === undefined);
  if (missing.length > 0) report.missingEdges[name] = missing;
  report.wallSets[name] = { direct: donors.length, synthetic: unfilled.length };
  return slots;
}

export function buildTilesets(config) {
  const curated = JSON.parse(readFileSync(new URL("../tilesets-config.json", import.meta.url), "utf8"));
  const brushes = loadGroundBrushes(config.rmeMaterials);
  const borders = loadBorders(config.rmeMaterials);
  const familyNames = new Set([...curated.grounds, ...curated.mountains]);

  const families = {};
  const borderSets = {};
  const wallSets = {};
  const report = { families: [], borderSets: [], wallSets: {}, synthetic: [], missingEdges: {}, skipped: [] };

  const requireBrush = (name) => {
    const brush = brushes.get(name);
    if (!brush) throw new Error(`tilesets-config.json references unknown RME brush "${name}"`);
    if (brush.items.length === 0) throw new Error(`RME brush "${name}" has no ground items`);
    return brush;
  };

  const copyEdges = (name, ref) => {
    const edges = borders.get(ref.id);
    if (!edges || Object.keys(edges).length === 0) {
      report.skipped.push(`${name}: border id ${ref.id} not found or empty`);
      return null;
    }
    return { ...edges };
  };

  const emitOuterBorders = (name, brush, genericKey) => {
    for (const ref of brush.borders) {
      if (ref.align !== "outer") continue;
      let key;
      if (ref.to === null) key = genericKey;
      else if (ref.to === "none") {
        // RME "to=none" means a void neighbour (map edge); our maps have no void seams.
        report.skipped.push(`${name}: outer border to=none (RME void) skipped`);
        continue;
      } else if (familyNames.has(ref.to)) key = `${name}->${ref.to}`;
      else {
        report.skipped.push(`${name}: outer border to="${ref.to}" is not a curated family`);
        continue;
      }
      const edges = copyEdges(name, ref);
      if (edges !== null) borderSets[key] = edges;
    }
  };

  for (const name of [...curated.grounds].sort()) {
    const brush = requireBrush(name);
    families[name] = { kind: "ground", items: brush.items, zOrder: brush.zOrder };
    // Inner borders of grounds are inverted transitions RME uses for map voids; the
    // painter v1 only consumes outer + ->none, so they are ignored in this slice.
    emitOuterBorders(name, brush, `${name}->none`);
    report.families.push({ name, kind: "ground", items: brush.items.length, zOrder: brush.zOrder });
  }

  for (const name of [...curated.mountains].sort()) {
    const brush = requireBrush(name);
    families[name] = { kind: "mountain", items: brush.items, zOrder: brush.zOrder };
    // Outer border of a mountain is drawn on the neighbouring open floor (foot-of-rock).
    emitOuterBorders(name, brush, `${name}->OPEN`);
    const inner = pickBorderRef(brush, "inner");
    if (inner === null) {
      throw new Error(`mountain brush "${name}" has no inner border set (no rock faces); pick another brush`);
    }
    const edges = copyEdges(name, inner);
    if (edges === null) throw new Error(`mountain brush "${name}": inner border id ${inner.id} missing from borders.xml`);
    wallSets[name] = buildWallSet(name, brush, edges, report);
    report.families.push({ name, kind: "mountain", items: brush.items.length, zOrder: brush.zOrder });
  }

  report.borderSets = Object.keys(borderSets).sort();
  return { tilesets: { families, borderSets, wallSets }, report };
}
