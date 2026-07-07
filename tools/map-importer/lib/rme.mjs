import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

function readText(path) {
  return readFileSync(path, "utf8");
}

function parseAttrs(text) {
  const attrs = {};
  const attrRe = /([\w-]+)="([^"]*)"/g;
  let match;
  while ((match = attrRe.exec(text)) !== null) {
    attrs[match[1]] = match[2];
  }
  return attrs;
}

function includedFiles(materialsDir, indexFile, fallbackDir) {
  const indexPath = join(materialsDir, indexFile);
  const xml = readText(indexPath);
  const files = [...xml.matchAll(/<include\s+file="([^"]+)"\s*\/>/g)].map((match) => join(materialsDir, match[1]));
  if (files.length > 0) return files;

  return readdirSync(join(materialsDir, fallbackDir))
    .filter((file) => file.endsWith(".xml"))
    .sort()
    .map((file) => join(materialsDir, fallbackDir, file));
}

function readIncludedXml(materialsDir, indexFile, fallbackDir) {
  return includedFiles(materialsDir, indexFile, fallbackDir)
    .map((file) => readText(file))
    .join("\n");
}

export function loadBorders(materialsDir) {
  const xml = readIncludedXml(materialsDir, "borders.xml", "borders");
  const out = new Map();
  const borderRe = /<border\s+([^>]*)>([\s\S]*?)<\/border>/g;
  const itemRe = /<borderitem\s+([^>]*)\/>/g;
  let borderMatch;

  while ((borderMatch = borderRe.exec(xml)) !== null) {
    const attrs = parseAttrs(borderMatch[1]);
    if (!attrs.id) continue;

    const edges = {};
    let itemMatch;
    while ((itemMatch = itemRe.exec(borderMatch[2])) !== null) {
      const itemAttrs = parseAttrs(itemMatch[1]);
      if (!itemAttrs.edge || !itemAttrs.item) continue;
      edges[itemAttrs.edge] = Number(itemAttrs.item);
    }

    out.set(Number(attrs.id), edges);
  }

  return out;
}

export function loadGroundBrushes(materialsDir) {
  const xml = readIncludedXml(materialsDir, "brushs.xml", "brushs");
  const out = new Map();
  const brushRe = /<brush\s+([^>]*\btype="ground"[^>]*)>([\s\S]*?)<\/brush>/g;
  const itemRe = /<item\s+([^>]*)\/>/g;
  const borderRe = /<border\s+([^>]*)(?:\/>|>)/g;
  let brushMatch;

  while ((brushMatch = brushRe.exec(xml)) !== null) {
    const attrs = parseAttrs(brushMatch[1]);
    if (!attrs.name) continue;

    const lookid = attrs.lookid ?? attrs.server_lookid;
    const body = brushMatch[2];
    const items = [];
    const borders = [];
    let itemMatch;
    while ((itemMatch = itemRe.exec(body)) !== null) {
      const itemAttrs = parseAttrs(itemMatch[1]);
      if (itemAttrs.id) items.push(Number(itemAttrs.id));
    }

    let borderMatch;
    while ((borderMatch = borderRe.exec(body)) !== null) {
      const borderAttrs = parseAttrs(borderMatch[1]);
      if (!borderAttrs.align || !borderAttrs.id) continue;
      borders.push({
        align: borderAttrs.align,
        to: borderAttrs.to ?? null,
        id: Number(borderAttrs.id),
      });
    }

    out.set(attrs.name, {
      name: attrs.name,
      lookid: lookid ? Number(lookid) : 0,
      zOrder: attrs["z-order"] ? Number(attrs["z-order"]) : 0,
      items,
      borders,
    });
  }

  return out;
}
