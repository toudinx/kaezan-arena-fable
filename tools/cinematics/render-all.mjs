// Renders one summon webm per Kaeli composition (id `summon-<slug>`) and copies
// each into frontend/public/assets/cinematics/summon-<slug>.webm.
//
//   node render-all.mjs            # all Kaelis in the roster
//   node render-all.mjs velvet rin # only the given slugs
//
// Requires FFmpeg on PATH (Remotion uses it for the webm muxing). Heavy — this
// is a desktop step, like the ComfyUI renders elsewhere in the visual roadmap.
import { execFileSync } from "node:child_process";
import { readFileSync, copyFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const roster = JSON.parse(readFileSync(join(here, "src", "kaelis.json"), "utf8"));
const want = process.argv.slice(2);
const targets = want.length ? roster.filter((k) => want.includes(k.slug)) : roster;

if (!targets.length) {
  console.error(`no matching Kaelis. known slugs: ${roster.map((k) => k.slug).join(", ")}`);
  process.exit(1);
}

// Keep public/<slug>/ assets fresh before rendering.
execFileSync("node", [join(here, "sync-assets.mjs")], { stdio: "inherit" });

const outDir = join(here, "out");
const dstDir = join(here, "..", "..", "frontend", "public", "assets", "cinematics");
mkdirSync(outDir, { recursive: true });
mkdirSync(dstDir, { recursive: true });

// shell:true so Node resolves `npx`/`npx.cmd` (Windows can't spawn a .cmd directly).
const npx = process.platform === "win32" ? "npx.cmd" : "npx";
for (const { slug } of targets) {
  const id = `summon-${slug}`;
  const out = join(outDir, `${id}.webm`);
  console.log(`\n=== rendering ${id} ===`);
  execFileSync(npx, ["remotion", "render", id, out, "--codec=vp8"], { stdio: "inherit", shell: true });
  copyFileSync(out, join(dstDir, `${id}.webm`));
  console.log(`copied → frontend/public/assets/cinematics/${id}.webm`);
}
console.log(`\ndone: ${targets.length} summon webm(s).`);
