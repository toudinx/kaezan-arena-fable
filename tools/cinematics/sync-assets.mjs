// Copies each Kaeli's `thumb.png` + `bg-landscape.png` from the frontend
// (source of truth: frontend/public/assets/kaelis/<slug>/) into this project's
// public/<slug>/ so Remotion's staticFile() can read them. The synced copies
// are git-ignored — re-run this whenever the art changes.
//
//   node sync-assets.mjs
import { readFileSync, copyFileSync, mkdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const roster = JSON.parse(readFileSync(join(here, "src", "kaelis.json"), "utf8"));
const srcRoot = join(here, "..", "..", "frontend", "public", "assets", "kaelis");
const dstRoot = join(here, "public");
const FILES = ["thumb.png", "bg-landscape.png"];

let copied = 0;
let missing = 0;
for (const { slug } of roster) {
  const dstDir = join(dstRoot, slug);
  mkdirSync(dstDir, { recursive: true });
  for (const file of FILES) {
    const src = join(srcRoot, slug, file);
    if (!existsSync(src)) {
      console.warn(`! missing ${slug}/${file} in frontend assets — skipped`);
      missing++;
      continue;
    }
    copyFileSync(src, join(dstDir, file));
    copied++;
  }
}
console.log(`synced ${copied} file(s) into public/${missing ? ` (${missing} missing)` : ""}`);
if (missing) process.exit(1);
