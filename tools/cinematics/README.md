# tools/cinematics — Kaezan Arena Fable

Remotion (programmatic React video) project that renders the **5★ summon cutscenes**.
It is **isolated from the Angular app** — it is not part of the frontend bundle. The only
output that crosses the boundary is a `.webm` copied into
`frontend/public/assets/cinematics/`.

> Requires **FFmpeg** on PATH (`ffmpeg -version`) and Node 20+.

## Compositions

One composition **per Kaeli**, id `summon-<slug>` (15s @ 30fps · 1920×1080), all driven by the
same `GachaSummon` component — registered in `src/Root.tsx` from the roster in `src/kaelis.ts`:

| id | element accent | output |
|---|---|---|
| `summon-eloa`   | holy `#ffe39c`     | `summon-eloa.webm` |
| `summon-seren`  | physical `#c8bba6` | `summon-seren.webm` |
| `summon-velvet` | death `#a662ff`    | `summon-velvet.webm` |
| `summon-rin`    | fire `#ff6a3d`     | `summon-rin.webm` |
| `summon-rynna`  | energy `#2fe0c4`   | `summon-rynna.webm` |
| `summon-lunara` | ice `#6fd6ff`      | `summon-lunara.webm` |
| `summon-gaia`   | earth `#8cbf4d`    | `summon-gaia.webm` |

The cutscene runs in five beats: dark cathedral → arcane circle charge (**element accent**) →
**gold 5★ burst** → the **summon card** rises (the Kaeli's `thumb` portrait in an aurum 5★
frame, with a glow + light sweep) + rising embers → nameplate fills in (`★★★★★ · NAME`).
Colors mirror the `Cathedral Ink + Aurum` tokens from `frontend/src/styles.css`. Only the
**build-up** is element-coded (accent derived from the `--el-*` tokens, per Kaeli, in
`src/kaelis.ts`); the **5★ climax** (burst, frame, stars) stays aurum `#e8a93c` for every Kaeli
so the rarity language reads identically. Roster (name/title/element) lives in `src/kaelis.json`.

> The reveal uses the **`thumb`** (square portrait, which already has its own background),
> not the full-body `idle` — the idles are `rgb24` with a flattened white background, so
> compositing them over the cathedral leaves a cut-out box. The framed card reads more like
> a gacha "result" anyway. Assets used: `public/<slug>/thumb.png` + `bg-landscape.png`.

## Workflow

```bash
npm install
npm run sync     # copy each Kaeli's thumb + bg-landscape from the frontend into public/
npm run studio   # live preview / scrub the timeline (auto-syncs first)
npm run deploy   # render EVERY Kaeli's webm AND copy each into frontend/public/assets/cinematics/
```

`public/<slug>/` is **git-ignored** — `sync-assets.mjs` regenerates it from the frontend's
source-of-truth art (`frontend/public/assets/kaelis/<slug>/`); `studio`/`deploy`/`still` run it
automatically. `npm run deploy` renders all seven via `render-all.mjs`; pass slugs to render a
subset: `node render-all.mjs velvet rin`. `npm run render` renders just `summon-velvet`.
`npm run still` renders a single frame of `summon-velvet` for a quick layout check.

## Adding another Kaeli

The whole roster is data-driven. Add a row to `src/kaelis.json` (`slug`, `name`, `element`,
`title`) — the slug must match the asset folder under `frontend/public/assets/kaelis/<slug>/`,
and the element must be one of the seven keys in `ELEMENT_ACCENTS` (`src/kaelis.ts`). That's it:
the composition (`summon-<slug>`), accent color, and asset sync all derive from the row. Then
`npm run deploy` (or `node render-all.mjs <slug>`).

## Optional integration in the app

Playing the clip on a 5★ featured pull lives in `frontend/.../pages/recruit/recruit.ts` and is a
separate task (Prompt 10 only produces the asset). Suggested seam: when a pull yields the featured
5★, show a fullscreen `<video autoplay muted playsinline>` of
`assets/cinematics/summon-<slug>.webm` (matching the pulled Kaeli) with a skip button, then fall
through to the existing CSS reveal (Prompt 5).
