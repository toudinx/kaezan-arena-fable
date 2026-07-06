# Cinematic Game Shell UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Home and Dungeon selection screens feel like playable game surfaces instead of report/dashboard pages: no desktop scroll, less duplicated navigation, cinematic wallpaper backdrops, and a compact expedition launch flow.

**Architecture:** Keep the existing Angular standalone-component structure. UI implementation lives in `frontend/src/app/shell/shell.ts`, `frontend/src/app/pages/home/home.ts`, `frontend/src/app/pages/mode/mode.ts`, and small reusable UI/theme helpers only if they reduce duplication. Generated wallpaper assets live under `frontend/public/assets/biomes/generated/` and are referenced through `frontend/src/app/core/tier-biomes.ts`.

**Tech Stack:** Angular 21 standalone components, signals, inline templates/styles, CSS variables, existing Canvas/game backend untouched, built-in image generation for wallpapers, Chrome/headless screenshot checks.

## Global Constraints

- Work directly on branch `main`; do not create `codex/*`, `claude/*`, or feature branches unless the user explicitly asks.
- Do not touch backend engine simulation or authoritative gameplay rules.
- Do not touch admin UI except if a shared shell change naturally affects the route wrapper.
- Visible player-facing UI text must be English.
- Frontend never simulates combat or movement.
- Do not include unrelated existing changes in commits, especially `docs/.obsidian/*` or `docs/Sem titulo.canvas`.
- Use selective staging only.
- If implementation succeeds and required checks pass, commit and push to `origin/main`.
- Before final completion, run `dotnet build` from repo root or backend project and `npx ng build` from `frontend`.
- If wallpapers are generated, final project-referenced image files must be copied into the workspace, not left under `$CODEX_HOME/generated_images`.
- Claude Code must not generate final wallpaper prompts or image assets in this plan. That work belongs to an imagegen-capable Codex session.

---

## Agent Ownership

### Imagegen-Capable Codex

Owns:
- Final wallpaper prompt execution.
- Selecting generated outputs.
- Copying assets into `frontend/public/assets/biomes/generated/`.
- Updating image references after visual QA.

Does not own:
- Backend gameplay logic.
- Admin UI.

### Codex Implementation Agent

Owns:
- Angular/CSS implementation.
- Layout/no-scroll verification.
- Home and Dungeon UI simplification.
- Build/test/commit/push when all checks pass.

May touch:
- `frontend/src/app/shell/shell.ts`
- `frontend/src/app/pages/home/home.ts`
- `frontend/src/app/pages/mode/mode.ts`
- `frontend/src/app/core/tier-biomes.ts`
- Optional new focused UI helper under `frontend/src/app/core/ui/`
- `README.md` if visible behavior changes enough to document.

### Claude Code

Owns:
- Documentation and UX review only.
- May create or edit `docs/design/cinematic_game_shell.md`.
- May review screenshots and list UX findings.

Must not touch:
- `frontend/src/app/shell/shell.ts`
- `frontend/src/app/pages/home/home.ts`
- `frontend/src/app/pages/mode/mode.ts`
- `frontend/src/app/core/tier-biomes.ts`
- `frontend/public/assets/**`
- Final wallpaper prompts or generated image assets.

---

## Task 1: Codex - Establish The Viewport Contract

**Files:**
- Modify: `frontend/src/app/shell/shell.ts`
- Modify: `frontend/src/app/pages/home/home.ts`
- Modify: `frontend/src/app/pages/mode/mode.ts`

**Interfaces:**
- Produces CSS contract: topbar height is exposed as `--shell-h: 56px`.
- Consumes no new TypeScript API.

- [ ] **Step 1: Record the current failing measurement**

Run:

```powershell
Invoke-WebRequest -Uri http://127.0.0.1:4201 -UseBasicParsing | Select-Object -ExpandProperty StatusCode
Invoke-WebRequest -Uri http://127.0.0.1:5210/api/v1/account -UseBasicParsing | Select-Object -ExpandProperty StatusCode
```

Expected:

```text
200
200
```

If the dev servers are not running, start them:

```powershell
powershell -File tools/run-backend.ps1
cd frontend
npx ng serve --host 127.0.0.1 --port 4201 --proxy-config proxy.conf.json --no-open
```

- [ ] **Step 2: Add the shell height CSS variable**

In `frontend/src/app/shell/shell.ts`, update the `.topbar` style block:

```css
.topbar {
  --shell-h: 56px;
  display: flex;
  align-items: center;
  gap: clamp(14px, 2vw, 28px);
  min-height: var(--shell-h);
  padding: 8px clamp(14px, 2.5vw, 28px);
  position: sticky;
  top: 0;
  z-index: 50;
  background: linear-gradient(180deg, rgba(14, 14, 24, 0.9), rgba(12, 12, 21, 0.72));
  -webkit-backdrop-filter: blur(22px) saturate(1.25);
  backdrop-filter: blur(22px) saturate(1.25);
  border-bottom: 1px solid var(--line-strong);
  box-shadow: var(--glass-edge), 0 12px 38px rgba(0, 0, 0, 0.28);
}
```

Then update the routed `main` style in the same file:

```css
main {
  min-height: calc(100dvh - var(--shell-h, 56px));
}
```

- [ ] **Step 3: Replace hardcoded `53px` in Home**

In `frontend/src/app/pages/home/home.ts`, replace both instances of:

```css
min-height: calc(100dvh - 53px);
```

with:

```css
height: calc(100dvh - var(--shell-h, 56px));
min-height: 620px;
```

For the mobile media rule, keep the same height contract and retain the bottom padding:

```css
.hub {
  height: calc(100dvh - var(--shell-h, 56px));
  min-height: 620px;
  padding-bottom: 80px;
}
```

- [ ] **Step 4: Replace hardcoded `53px` in Dungeon mode**

In `frontend/src/app/pages/mode/mode.ts`, replace:

```css
min-height: calc(100dvh - 53px);
padding: clamp(20px, 3vw, 34px) clamp(18px, 4vw, 64px) clamp(28px, 5vw, 56px);
```

with:

```css
height: calc(100dvh - var(--shell-h, 56px));
min-height: 680px;
padding: clamp(18px, 2.5vw, 30px) clamp(18px, 4vw, 64px);
```

Then replace:

```css
min-height: calc(100dvh - 125px);
```

with:

```css
height: calc(100% - 44px);
min-height: 0;
```

- [ ] **Step 5: Verify desktop no-scroll baseline**

Use Chrome headless or the in-app browser to check `/` and `/hunt/dungeon` at about `2048x1152`.

Acceptance:
- `document.documentElement.scrollHeight <= window.innerHeight + 1` on `/`.
- `document.documentElement.scrollHeight <= window.innerHeight + 1` on `/hunt/dungeon`.
- No vertical scrollbar in desktop screenshots.

Commit only this task if it is green:

```powershell
git add frontend/src/app/shell/shell.ts frontend/src/app/pages/home/home.ts frontend/src/app/pages/mode/mode.ts
git commit -m "fix(ui): lock game shell screens to viewport"
```

---

## Task 2: Codex - Redesign Dungeon As A Deploy Screen

**Files:**
- Modify: `frontend/src/app/pages/mode/mode.ts`

**Interfaces:**
- Keeps existing `runCount`, `sessionMaxRuns`, `sessionStopLosses`, `sessionTierUp`, `start`, and `startIdleSession` methods.
- Adds UI-only signal `readonly autoSettingsOpen = signal(false);`.
- Produces no backend changes.

- [ ] **Step 1: Add drawer state**

In `ModeSelectPage`, near the existing idle-session signals, add:

```ts
readonly autoSettingsOpen = signal(false);
```

- [ ] **Step 2: Move idle controls out of the main intel stack**

In the template, replace the inline `<div class="idle-session">...</div>` block with this compact action row inside the main `.intel` flow, directly after `.farm-plan`:

```html
<div class="auto-row">
  <div>
    <span>Auto Expedition</span>
    <small>Advanced queue rules are tucked away.</small>
  </div>
  <button class="text-chip" type="button" (click)="autoSettingsOpen.set(true)">
    Queue Settings
  </button>
</div>
```

Then add this drawer markup near the end of the `.page` section, still inside the `@if (m.id === 'dungeon')` branch so it only appears on Dungeon:

```html
@if (autoSettingsOpen()) {
  <div class="queue-scrim" (click)="autoSettingsOpen.set(false)" aria-hidden="true"></div>
  <aside class="queue-drawer glass-strong" aria-label="Auto expedition queue settings">
    <header>
      <div>
        <span class="eyebrow">Auto Expedition</span>
        <h2>Queue Settings</h2>
      </div>
      <button class="drawer-close" type="button" aria-label="Close queue settings"
              (click)="autoSettingsOpen.set(false)">x</button>
    </header>
    <p class="queue-copy">
      Attempts handles normal repeat runs. Use these rules only when you want the backend to chain
      runs while this tab watches.
    </p>
    <label>Stop after losses in a row
      <input type="number" min="0" max="20" [value]="sessionStopLosses()"
             (input)="sessionStopLosses.set(+$any($event.target).value)" />
    </label>
    <label>Max runs (0 = endless)
      <input type="number" min="0" max="999" [value]="sessionMaxRuns()"
             (input)="sessionMaxRuns.set(+$any($event.target).value)" />
    </label>
    <label>Tier up after wins (0 = never)
      <input type="number" min="0" max="20" [value]="sessionTierUp()"
             (input)="sessionTierUp.set(+$any($event.target).value)" />
    </label>
    @if (selectedTier(); as queueTier) {
      <button class="pill-btn ghost" [disabled]="sessionStarting() || locked(queueTier.requiredAccountLevel)"
              (click)="startIdleSession(queueTier.tier)">
        Start Auto Expedition
      </button>
    }
  </aside>
}
```

- [ ] **Step 3: Reframe attempts and CTA into one launch module**

Replace the existing `.farm-plan` and `.actions` relationship so `Choose Kaeli` is visually part of launch. The structure should remain:

```html
<div class="farm-plan" [class.multi]="runCount() > 1">
  ...
</div>

<div class="actions">
  @if (locked(t.requiredAccountLevel)) {
    <span class="lock-msg">Unlocks at account level {{ t.requiredAccountLevel }}</span>
  } @else {
    <button class="pill-btn" (click)="start(t.tier)">Choose Kaeli</button>
  }
</div>
```

But style them as one launch cluster:
- `.farm-plan` has less vertical padding.
- `.actions` has no `margin-top: auto`.
- `.actions .pill-btn` is full-width inside `.intel`.

- [ ] **Step 4: Tighten the main layout**

In `.layout`, prefer this desktop grid:

```css
.layout {
  height: calc(100% - 44px);
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(210px, 270px) minmax(420px, 1fr) minmax(320px, 390px);
  gap: clamp(18px, 3vw, 48px);
  align-items: stretch;
}
```

In `.stage`, increase boss presence without growing document height:

```css
.stage {
  position: relative;
  min-height: 0;
  display: grid;
  place-items: end center;
  padding-bottom: clamp(18px, 6vh, 70px);
}
.stage app-outfit-preview {
  position: relative;
  z-index: 1;
  image-rendering: pixelated;
  filter: drop-shadow(0 24px 32px rgba(0,0,0,0.76));
  transform: scale(1.08);
}
```

In `.intel`, reduce persistent density:

```css
.intel {
  display: flex;
  flex-direction: column;
  gap: clamp(12px, 1.8vh, 18px);
  align-self: stretch;
  justify-content: center;
  min-width: 0;
}
.mob-lines p {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}
```

- [ ] **Step 5: Add drawer CSS**

Add focused styles:

```css
.auto-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  padding: 10px 12px;
  border: 1px solid var(--line);
  border-radius: var(--r-sm);
  background: rgba(12, 12, 20, 0.34);
}
.auto-row span {
  display: block;
  color: var(--text);
  font-size: 0.78rem;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.auto-row small { color: var(--text-mute); font-size: 0.75rem; }
.text-chip {
  border: 1px solid color-mix(in srgb, var(--bc) 34%, var(--line-strong));
  border-radius: var(--r-full);
  background: color-mix(in srgb, var(--bc) 10%, rgba(255,255,255,0.04));
  color: color-mix(in srgb, var(--bc) 78%, white);
  font-size: 0.72rem;
  font-weight: 900;
  padding: 7px 11px;
  white-space: nowrap;
}
.queue-scrim {
  position: absolute;
  inset: 0;
  z-index: 8;
  background: rgba(7, 7, 13, 0.58);
}
.queue-drawer {
  position: absolute;
  top: 0;
  right: 0;
  bottom: 0;
  z-index: 9;
  width: min(420px, 94vw);
  padding: var(--sp-5);
  border-radius: 0;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.queue-drawer header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.queue-drawer h2 { margin: 3px 0 0; }
.drawer-close {
  width: 34px;
  height: 34px;
  border: 1px solid var(--line-strong);
  border-radius: var(--r-full);
  background: rgba(255,255,255,0.05);
  color: var(--text-dim);
  font-weight: 900;
}
.queue-copy { margin: 0; color: var(--text-dim); line-height: var(--lh-body); }
.queue-drawer label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  color: var(--text-dim);
  font-size: 0.82rem;
}
.queue-drawer input {
  width: 76px;
  padding: 6px 9px;
  border: 1px solid color-mix(in srgb, var(--bc) 34%, var(--line-strong));
  border-radius: var(--r-sm);
  background: rgba(255, 255, 255, 0.05);
  color: var(--text);
  text-align: right;
}
```

- [ ] **Step 6: Verify Dungeon acceptance**

Acceptance:
- `/hunt/dungeon` at `2048x1152` has no vertical scrollbar.
- `Idle Session` text is not visible on the default Dungeon screen.
- `Auto Expedition` row is visible but compact.
- Clicking `Queue Settings` opens the drawer.
- `Attempts` remains visible and functional.
- `Choose Kaeli` remains the main CTA.

Commit:

```powershell
git add frontend/src/app/pages/mode/mode.ts
git commit -m "feat(ui): make dungeon launch cinematic"
```

---

## Task 3: Codex - Simplify Home Navigation

**Files:**
- Modify: `frontend/src/app/pages/home/home.ts`

**Interfaces:**
- Keeps `navItems` computed available if still useful.
- Produces no routing changes.

- [ ] **Step 1: Replace duplicate full nav rail with contextual actions**

In the Home template, replace the full `nav class="rail"` loop with three actions:

```html
<nav class="rail home-actions" aria-label="Home actions">
  <a class="rail-item glass primary-action" routerLink="/hunt">
    <span class="ri-icon">&#9876;</span>
    <span class="ri-text">
      <strong>Start Hunt</strong>
      <small>{{ api.catalog()?.tiers?.length ?? 0 }} dungeons</small>
    </span>
  </a>
  <a class="rail-item glass gold" routerLink="/recruit">
    <span class="ri-icon">&#10022;</span>
    <span class="ri-text">
      <strong>Recruit</strong>
      <small>Active banner</small>
    </span>
  </a>
  <button class="rail-item glass" type="button" (click)="drawerOpen.set(true)">
    <span class="ri-icon">&#128220;</span>
    <span class="ri-text">
      <strong>Contracts</strong>
      <small>{{ claimable() }} ready</small>
    </span>
  </button>
</nav>
```

If direct template access to `api` is private, add a computed:

```ts
readonly dungeonCount = computed(() => this.api.catalog()?.tiers.length ?? 0);
```

and use `{{ dungeonCount() }} dungeons`.

- [ ] **Step 2: Reduce rail visual weight**

Update `.rail`:

```css
.rail {
  position: absolute;
  right: clamp(16px, 2.5vw, 32px);
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  flex-direction: column;
  gap: 10px;
  z-index: 2;
  width: min(248px, 18vw);
}
.home-actions .rail-item {
  min-height: 74px;
}
.home-actions .primary-action {
  border-color: color-mix(in srgb, var(--accent) 42%, var(--line-strong));
  box-shadow: var(--glass-edge), 0 14px 36px rgba(123, 107, 242, 0.18);
}
```

- [ ] **Step 3: Keep mobile bottom rail sane**

In the mobile media block, keep the existing bottom rail behavior, but verify only three actions appear and text does not overflow.

Acceptance:
- Topbar remains the only full navigation.
- Home right side shows at most Start Hunt, Recruit, Contracts.
- No duplicate Kaelis/Backpack/Bestiary links on Home.
- No desktop scroll.

Commit:

```powershell
git add frontend/src/app/pages/home/home.ts
git commit -m "feat(ui): focus home actions"
```

---

## Task 4: Imagegen-Capable Codex - Generate Biome Wallpapers

**Files:**
- Create directory: `frontend/public/assets/biomes/generated/`
- Add generated assets:
  - `frontend/public/assets/biomes/generated/tier-1-cave-cinematic.png`
  - `frontend/public/assets/biomes/generated/tier-2-fort-cinematic.png`
  - `frontend/public/assets/biomes/generated/tier-3-crypt-cinematic.png`
  - `frontend/public/assets/biomes/generated/tier-4-lair-cinematic.png`
  - `frontend/public/assets/biomes/generated/tier-5-abyss-cinematic.png`
- Modify: `frontend/src/app/core/tier-biomes.ts`

**Interfaces:**
- Produces `TierBiome.bg` replacement paths.
- Does not change gameplay, catalog data, or backend.

- [ ] **Step 1: Generate the tier 1 wallpaper with built-in imagegen**

Use this exact prompt:

```text
Use case: stylized-concept
Asset type: browser game dungeon selection wallpaper, 2048x1152 landscape
Primary request: a cinematic fantasy cave interior for a premium gacha roguelike game screen
Scene/backdrop: living crystal cave, wet stone path receding into darkness, moss, small green crystal glows, layered cavern arches, distant tunnel opening
Subject: environment only, no characters, no monsters, no UI, no text
Style/medium: polished dark fantasy concept art, high detail, atmospheric but readable
Composition/framing: wide 16:9 wallpaper, strong central floor area for a boss sprite overlay, left and right edges darker for UI panels, no important detail at the extreme bottom edge
Lighting/mood: emerald cave light, soft fog, premium dramatic contrast, mysterious but playable
Color palette: deep black-green, moss, muted stone, restrained crystal highlights
Constraints: no text, no logo, no watermark, no character, no creature, no weapons, no user interface, no frame border
Avoid: flat gradient background, abstract bokeh, generic stock fantasy, bright daytime lighting
```

Save selected output as:

```text
frontend/public/assets/biomes/generated/tier-1-cave-cinematic.png
```

- [ ] **Step 2: Generate the tier 2 wallpaper**

Use this exact prompt:

```text
Use case: stylized-concept
Asset type: browser game dungeon selection wallpaper, 2048x1152 landscape
Primary request: a cinematic ruined fort interior reclaimed by nature for a premium gacha roguelike game screen
Scene/backdrop: old stone fortress hall, broken arches, grass and roots through cracked floor, amber torch haze, distant gate, hints of banners without readable symbols
Subject: environment only, no characters, no monsters, no UI, no text
Style/medium: polished dark fantasy concept art, high detail, atmospheric but readable
Composition/framing: wide 16:9 wallpaper, open central floor for a boss sprite overlay, darker side edges for UI panels
Lighting/mood: warm amber fort light mixed with green overgrowth, heroic but dangerous
Color palette: dark olive, old stone, muted gold, ember orange
Constraints: no text, no logo, no watermark, no character, no creature, no weapons, no user interface, no frame border
Avoid: sunny castle courtyard, clean palace, cartoon style, flat gradient background
```

Save selected output as:

```text
frontend/public/assets/biomes/generated/tier-2-fort-cinematic.png
```

- [ ] **Step 3: Generate the tier 3 wallpaper**

Use this exact prompt:

```text
Use case: stylized-concept
Asset type: browser game dungeon selection wallpaper, 2048x1152 landscape
Primary request: a cinematic gothic crypt interior for a premium gacha roguelike game screen
Scene/backdrop: ancient underground crypt, ribbed stone vaults, cracked sarcophagi silhouettes at the edges, violet soul-light, dust motes, bone-white stone details kept subtle
Subject: environment only, no characters, no monsters, no UI, no text
Style/medium: polished dark fantasy concept art, high detail, atmospheric but readable
Composition/framing: wide 16:9 wallpaper, central aisle and floor reserved for a boss sprite overlay, left and right sides dark enough for translucent UI
Lighting/mood: violet necromantic glow, solemn, haunted, premium contrast
Color palette: black stone, deep purple, cold bone, faint silver
Constraints: no text, no logo, no watermark, no character, no creature, no weapons, no user interface, no frame border
Avoid: gore, skull piles as the main subject, bright cathedral window scene, flat gradient background
```

Save selected output as:

```text
frontend/public/assets/biomes/generated/tier-3-crypt-cinematic.png
```

- [ ] **Step 4: Generate the tier 4 wallpaper**

Use this exact prompt:

```text
Use case: stylized-concept
Asset type: browser game dungeon selection wallpaper, 2048x1152 landscape
Primary request: a cinematic ashscale lava lair interior for a premium gacha roguelike game screen
Scene/backdrop: dark volcanic cavern, basalt ribs, shallow lava pools and orange cracks, smoky heat haze, black rock amphitheater, distant molten glow
Subject: environment only, no characters, no monsters, no UI, no text
Style/medium: polished dark fantasy concept art, high detail, atmospheric but readable
Composition/framing: wide 16:9 wallpaper, open central landing for a boss sprite overlay, dark side edges for UI panels, no important detail at the bottom edge
Lighting/mood: hot orange-red lava against deep black stone, threatening but not overexposed
Color palette: obsidian black, ember red, molten orange, smoke gray
Constraints: no text, no logo, no watermark, no character, no creature, no weapons, no user interface, no frame border
Avoid: bright cartoon lava, sci-fi machinery, dragon silhouette, flat gradient background
```

Save selected output as:

```text
frontend/public/assets/biomes/generated/tier-4-lair-cinematic.png
```

- [ ] **Step 5: Generate the tier 5 wallpaper**

Use this exact prompt:

```text
Use case: stylized-concept
Asset type: browser game dungeon selection wallpaper, 2048x1152 landscape
Primary request: a cinematic abyssal cathedral cavern for a premium gacha roguelike game screen
Scene/backdrop: impossible underground abyss, fractured gothic arches fused with cave rock, violet-blue void light, floating dust, deep chasm shapes in the far background, subtle crystalline forms
Subject: environment only, no characters, no monsters, no UI, no text
Style/medium: polished dark fantasy concept art, high detail, atmospheric but readable
Composition/framing: wide 16:9 wallpaper, central floor platform for a boss sprite overlay, left and right edges darker for UI panels
Lighting/mood: cosmic violet, blue-black, ominous endgame depth, premium dramatic contrast
Color palette: deep indigo, black violet, muted iris, faint cold silver
Constraints: no text, no logo, no watermark, no character, no creature, no weapons, no user interface, no frame border
Avoid: outer space scene, bright nebula, abstract gradient, visible UI, readable runes
```

Save selected output as:

```text
frontend/public/assets/biomes/generated/tier-5-abyss-cinematic.png
```

- [ ] **Step 6: Update biome paths**

In `frontend/src/app/core/tier-biomes.ts`, update `bg` paths:

```ts
export const TIER_BIOMES: Record<number, TierBiome> = {
  1: { accent: '#8cbf4d', deep: '#2c3a17', label: 'Cave', bg: '/assets/biomes/generated/tier-1-cave-cinematic.png' },
  2: { accent: '#d99a3c', deep: '#4a3210', label: 'Fort', bg: '/assets/biomes/generated/tier-2-fort-cinematic.png' },
  3: { accent: '#a662ff', deep: '#2e1a4d', label: 'Crypt', bg: '/assets/biomes/generated/tier-3-crypt-cinematic.png' },
  4: { accent: '#ff6a3d', deep: '#4a1a0e', label: 'Lair', bg: '/assets/biomes/generated/tier-4-lair-cinematic.png' },
  5: { accent: '#7b6bf2', deep: '#1f1a45', label: 'Abyss', bg: '/assets/biomes/generated/tier-5-abyss-cinematic.png' },
};
```

- [ ] **Step 7: Visual acceptance**

Acceptance:
- Each image is environment-only.
- No text, logo, watermark, UI, visible character, or monster.
- Center has room for current boss sprite.
- Side edges are dark enough for tier rail and intel panels.
- If any generated asset fails these checks, regenerate only that tier using the same prompt plus one targeted correction.

Commit:

```powershell
git add frontend/public/assets/biomes/generated frontend/src/app/core/tier-biomes.ts
git commit -m "feat(assets): add cinematic dungeon wallpapers"
```

---

## Task 5: Codex - Add Lightweight Backdrop Effects

**Files:**
- Modify: `frontend/src/app/pages/home/home.ts`
- Modify: `frontend/src/app/pages/mode/mode.ts`
- Modify: `frontend/src/app/pages/hunt/hunt.ts` only if the current mode-selection background needs the same treatment.

**Interfaces:**
- CSS-only atmosphere. No gameplay state.
- Must respect `prefers-reduced-motion`.

- [ ] **Step 1: Add CSS-only atmosphere layers to Dungeon**

In `frontend/src/app/pages/mode/mode.ts`, after `.wash`, add pseudo/extra styles that create fog and subtle particle texture without DOM-heavy animation:

```css
.page::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -2;
  pointer-events: none;
  background:
    radial-gradient(34% 18% at 52% 72%, color-mix(in srgb, var(--bc) 22%, transparent), transparent 70%),
    radial-gradient(1px 1px at 18% 28%, rgba(255,255,255,0.22), transparent 70%),
    radial-gradient(1px 1px at 72% 42%, rgba(255,255,255,0.16), transparent 70%),
    radial-gradient(1px 1px at 46% 18%, rgba(255,255,255,0.12), transparent 70%);
  opacity: 0.75;
  mix-blend-mode: screen;
}
.page::after {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -1;
  pointer-events: none;
  background:
    linear-gradient(180deg, rgba(7,7,13,0.72), transparent 26%, rgba(7,7,13,0.84)),
    radial-gradient(70% 90% at 50% 50%, transparent 42%, rgba(0,0,0,0.58) 100%);
}
```

- [ ] **Step 2: Add reduced-motion guard**

Add:

```css
@media (prefers-reduced-motion: reduce) {
  .page::before,
  .hub::before {
    animation: none !important;
  }
}
```

Only include animation if it is subtle and CSS-only:

```css
@media (prefers-reduced-motion: no-preference) {
  .page::before {
    animation: atmosphere-drift 18s var(--ease-in-out) infinite alternate;
  }
  @keyframes atmosphere-drift {
    from { transform: translate3d(-0.6%, -0.4%, 0); opacity: 0.62; }
    to { transform: translate3d(0.7%, 0.5%, 0); opacity: 0.82; }
  }
}
```

- [ ] **Step 3: Make Home feel more like a game screen**

In `frontend/src/app/pages/home/home.ts`, add a subtle depth layer similar to Dungeon, but do not obscure the Kaeli wallpaper:

```css
.hub::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: 0;
  pointer-events: none;
  background:
    radial-gradient(42% 20% at 52% 80%, rgba(155,140,255,0.16), transparent 70%),
    radial-gradient(1px 1px at 22% 24%, rgba(255,255,255,0.20), transparent 70%),
    radial-gradient(1px 1px at 82% 38%, rgba(255,255,255,0.14), transparent 70%);
  mix-blend-mode: screen;
  opacity: 0.62;
}
```

Ensure `.identity`, `.rail`, `.dailies-fab`, and `.drawer` still have positive `z-index` above this layer.

- [ ] **Step 4: Visual acceptance**

Acceptance:
- Effects make backgrounds richer, but do not look like decorative gradient blobs.
- No UI overlap.
- No unreadable text.
- No excessive motion.

Commit:

```powershell
git add frontend/src/app/pages/home/home.ts frontend/src/app/pages/mode/mode.ts frontend/src/app/pages/hunt/hunt.ts
git commit -m "feat(ui): add cinematic backdrop atmosphere"
```

---

## Task 6: Claude Code - Documentation And UX Review Only

**Files:**
- Create or modify: `docs/design/cinematic_game_shell.md`

**Interfaces:**
- This task produces documentation and review notes only.
- It must not edit frontend files or generated assets.

- [ ] **Step 1: Create the design note**

Write `docs/design/cinematic_game_shell.md` with this structure:

```markdown
# Cinematic Game Shell

## Goal

Home and Hunt surfaces should read as game screens first: full-bleed fantasy scene, low persistent chrome, strong primary verb, and deeper settings behind drawers.

## Rules

- No desktop scroll on Home or Dungeon selection.
- Topbar is the only full navigation.
- Home contextual actions are Start Hunt, Recruit, and Contracts.
- Dungeon default screen shows tier rail, boss stage, tier intel, attempts, and Choose Kaeli.
- Auto Expedition settings stay behind Queue Settings.
- Backgrounds use wallpapers plus restrained atmosphere effects, not report-style panels.
- Admin remains outside the game flow.

## Review Checklist

- Does the screenshot read as a game scene before it reads as UI?
- Is the center stage clear for character or boss art?
- Is persistent UI under roughly 25 percent of the viewport?
- Is the primary CTA obvious?
- Are advanced/rare settings hidden by default?
- Is all player-facing copy in English?
```

- [ ] **Step 2: Review screenshots after Codex implementation**

Claude Code should produce a short section in the same file:

```markdown
## Post-Implementation UX Review

- Home:
- Dungeon:
- Mobile:
- Remaining polish:
```

Each bullet must be concrete and screenshot-based. No speculative code advice.

- [ ] **Step 3: Commit docs only**

```powershell
git add docs/design/cinematic_game_shell.md
git commit -m "docs(ui): define cinematic game shell"
```

---

## Task 7: Codex - Final Verification, README, Commit, Push

**Files:**
- Modify: `README.md` only if visible behavior or asset workflow changed enough to document.
- No frontend code changes unless fixing verification failures.

- [ ] **Step 1: Run backend build**

```powershell
dotnet build
```

Expected:

```text
Build succeeded.
```

If the solution file is not available from repo root, run:

```powershell
dotnet build backend/src/KaezanArenaFable.Api/KaezanArenaFable.Api.csproj
```

- [ ] **Step 2: Run frontend build**

```powershell
cd frontend
npx ng build
```

Expected:

```text
Application bundle generation complete.
```

- [ ] **Step 3: Run visual smoke**

With backend and frontend running:

```powershell
powershell -File tools/run-backend.ps1
cd frontend
npx ng serve --host 127.0.0.1 --port 4201 --proxy-config proxy.conf.json --no-open
```

Check:
- `http://127.0.0.1:4201/`
- `http://127.0.0.1:4201/hunt`
- `http://127.0.0.1:4201/hunt/dungeon`
- `http://127.0.0.1:4201/play/1`

Acceptance:
- No desktop vertical scroll on Home or Dungeon selection.
- Dungeon default screen has no visible `Idle Session` panel.
- Queue Settings drawer opens and closes.
- Home no longer duplicates all topbar navigation in the right rail.
- New wallpapers load.
- Mobile layout does not overlap text or buttons at about `390x844`.

- [ ] **Step 4: Update README if needed**

If the implementation changes the visible UX language, add one short paragraph under `### Identidade visual` or the Hunt paragraph. Use Portuguese docs style, but do not alter player-facing UI string language.

- [ ] **Step 5: Final selective status**

Run:

```powershell
git status --short
```

Expected:
- Only files intentionally touched by this plan are modified/untracked.
- Do not stage `docs/.obsidian/*` or `docs/Sem titulo.canvas`.

- [ ] **Step 6: Final commit and push**

If previous tasks were not committed individually, commit now:

```powershell
git add README.md frontend/src/app/shell/shell.ts frontend/src/app/pages/home/home.ts frontend/src/app/pages/mode/mode.ts frontend/src/app/pages/hunt/hunt.ts frontend/src/app/core/tier-biomes.ts frontend/public/assets/biomes/generated docs/design/cinematic_game_shell.md
git commit -m "feat(ui): cinematic game shell"
git push origin main
```

If tasks were already committed individually, just push:

```powershell
git push origin main
```

Final response must include:
- What changed.
- Verification commands and results.
- Whether push succeeded.
- Any screenshots/visual notes that still need human review.
