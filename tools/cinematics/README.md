# tools/cinematics — Kaezan Arena Fable

Remotion (programmatic React video) project that renders the **5★ summon cutscenes**.
It is **isolated from the Angular app** — it is not part of the frontend bundle. The only
output that crosses the boundary is a `.webm` copied into
`frontend/public/assets/cinematics/`.

> Requires **FFmpeg** on PATH (`ffmpeg -version`) and Node 20+.

## Compositions

| id | duration | output |
|---|---|---|
| `GachaSummon` | 15s @ 30fps · 1920×1080 | `gacha-5star.webm` |

The cutscene runs in five beats: dark cathedral → arcane circle charge (iris purple) →
**gold 5★ burst** → the **summon card** rises (the Kaeli's `thumb` portrait in an aurum 5★
frame, with a glow + light sweep) + rising embers → nameplate fills in (`★★★★★ · VELVET`).
Colors mirror the `Cathedral Ink + Aurum` tokens from `frontend/src/styles.css`
(iris `#7b6bf2` = anticipation, aurum `#e8a93c` = the 5★ reward).

> The reveal uses the **`thumb`** (square portrait, which already has its own background),
> not the full-body `idle` — the idles are `rgb24` with a flattened white background, so
> compositing them over the cathedral leaves a cut-out box. The framed card reads more like
> a gacha "result" anyway. Assets used: `public/velvet/thumb.png` + `bg-landscape.png`.

## Workflow

```bash
npm install
npm run studio   # live preview / scrub the timeline
npm run deploy   # render webm AND copy it into frontend/public/assets/cinematics/
```

`npm run render` renders to `out/gacha-5star.webm`; `npm run deploy` also copies it to the
frontend. `npm run still` renders a single frame for a quick layout check.

## Adding another Kaeli

`GachaSummon` is parametrized (`name`, `title`, `thumbSrc`, `bgSrc`). To make a new 5★ reveal,
drop that Kaeli's `thumb.png` + `bg-landscape.png` into `public/`, register another
`<Composition>` in `src/Root.tsx` pointing at them, then `npm run deploy` with the matching
output name.

## Optional integration in the app

Playing the clip on a 5★ featured pull lives in `frontend/.../pages/recruit/recruit.ts` and is a
separate task (Prompt 10 only produces the asset). Suggested seam: when a pull yields the featured
5★, show a fullscreen `<video autoplay muted playsinline>` of
`assets/cinematics/gacha-5star.webm` with a skip button, then fall through to the existing CSS
reveal (Prompt 5).
