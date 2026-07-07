import { readFileSync } from "node:fs";

export function spawnsInBBox(xmlPath, { x, y, z, w, h }) {
  const xml = readFileSync(xmlPath, "utf8");
  const counts = new Map();
  const spawnRe = /<(?:spawn|monster)\s+centerx="(\d+)"\s+centery="(\d+)"\s+centerz="(\d+)"\s+radius="(-?\d+)"[^>]*>([\s\S]*?)<\/(?:spawn|monster)>/g;
  const monsterRe = /<monster\s+name="([^"]+)"/g;
  let spawnMatch;

  while ((spawnMatch = spawnRe.exec(xml)) !== null) {
    const cx = Number(spawnMatch[1]);
    const cy = Number(spawnMatch[2]);
    const cz = Number(spawnMatch[3]);
    const radius = Math.max(Number(spawnMatch[4]), 0);

    if (cz !== z) continue;
    if (cx + radius < x || cx - radius >= x + w || cy + radius < y || cy - radius >= y + h) continue;

    let monsterMatch;
    while ((monsterMatch = monsterRe.exec(spawnMatch[5])) !== null) {
      counts.set(monsterMatch[1], (counts.get(monsterMatch[1]) ?? 0) + 1);
    }
  }

  return [...counts.entries()]
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
}
