import { Injectable } from '@angular/core';

export interface SpriteGroup {
  kind: string;
  patternX: number;
  patternY: number;
  patternZ: number;
  layers: number;
  phases: [number, number][];
  start: number;
  count: number;
}

export interface AppearanceEntry {
  name: string;
  file: string;
  cellW: number;
  cellH: number;
  cols: number;
  groups: SpriteGroup[];
  flags: {
    groundSpeed?: number;
    unpass?: boolean;
    unsight?: boolean;
    top?: boolean;
    bottom?: boolean;
    clip?: boolean;
    elevation?: number;
    lightBrightness?: number;
    lightColor?: number;
  };
}

export interface TibiaManifest {
  outfits: Record<string, AppearanceEntry>;
  objects: Record<string, AppearanceEntry>;
  effects: Record<string, AppearanceEntry>;
  missiles: Record<string, AppearanceEntry>;
  semantic: Record<string, number[]>;
  objectNames: Record<string, number>;
}

export type ThingCategory = 'outfits' | 'objects' | 'effects' | 'missiles';

/**
 * Loads the Tibia sprite atlases produced by tools/AssetExtractor and provides
 * draw helpers: pattern/phase indexing, outfit mask recoloring (HSI palette),
 * directional missiles, animated effects.
 */
@Injectable({ providedIn: 'root' })
export class AssetsService {
  private manifest: TibiaManifest | null = null;
  private images = new Map<string, HTMLImageElement>();
  private coloredOutfits = new Map<string, HTMLCanvasElement>();
  private outfitBBoxes = new Map<number, { minX: number; minY: number; bw: number; bh: number } | null>();
  private loading: Promise<void> | null = null;

  async load(): Promise<void> {
    if (this.manifest) return;
    if (this.loading) return this.loading;
    this.loading = (async () => {
      const res = await fetch('/assets/tibia/manifest.json');
      this.manifest = (await res.json()) as TibiaManifest;
    })();
    return this.loading;
  }

  get ready(): boolean {
    return this.manifest !== null;
  }

  entry(category: ThingCategory, id: number): AppearanceEntry | null {
    return this.manifest?.[category]?.[String(id)] ?? null;
  }

  ids(category: ThingCategory): number[] {
    return Object.keys(this.manifest?.[category] ?? {}).map(Number);
  }

  semantic(key: string): number[] {
    return this.manifest?.semantic[key] ?? [];
  }

  /** Preload every atlas referenced by the manifest categories given (best effort). */
  async preload(categories: ThingCategory[]): Promise<void> {
    await this.load();
    const jobs: Promise<unknown>[] = [];
    for (const cat of categories) {
      for (const entry of Object.values(this.manifest![cat])) {
        jobs.push(this.image(entry.file).catch(() => undefined));
      }
    }
    await Promise.all(jobs);
  }

  async image(file: string): Promise<HTMLImageElement> {
    const cached = this.images.get(file);
    if (cached) return cached;
    const img = new Image();
    img.src = `/assets/tibia/${file}`;
    await new Promise<void>((resolve, reject) => {
      if (img.complete && img.naturalWidth > 0) return resolve();
      img.onload = () => resolve();
      img.onerror = () => reject(new Error(`failed to load ${file}`));
    });
    this.images.set(file, img);
    return img;
  }

  imageSync(file: string): HTMLImageElement | null {
    const img = this.images.get(file);
    if (img) return img;
    // kick off async load for next frames
    void this.image(file).catch(() => undefined);
    return null;
  }

  // ---------- outfit colors (Tibia HSI palette, otclient outfit.cpp) ----------

  static outfitColor(color: number): [number, number, number] {
    const H_STEPS = 19;
    const SI_VALUES = 7;
    if (color >= H_STEPS * SI_VALUES) color = 0;
    let h = 0;
    let s = 0;
    let i = 0;
    if (color % H_STEPS !== 0) {
      h = (color % H_STEPS) / 18.0;
      s = 1;
      i = 1;
      switch (Math.floor(color / H_STEPS)) {
        case 0: s = 0.25; i = 1.0; break;
        case 1: s = 0.25; i = 0.75; break;
        case 2: s = 0.5; i = 0.75; break;
        case 3: s = 0.667; i = 0.75; break;
        case 4: s = 1.0; i = 1.0; break;
        case 5: s = 1.0; i = 0.75; break;
        case 6: s = 1.0; i = 0.5; break;
      }
    } else {
      i = 1 - color / H_STEPS / SI_VALUES;
    }
    if (i === 0) return [0, 0, 0];
    if (s === 0) {
      const v = Math.floor(i * 255);
      return [v, v, v];
    }
    let red = 0;
    let green = 0;
    let blue = 0;
    if (h < 1 / 6) {
      red = i;
      blue = i * (1 - s);
      green = blue + (i - blue) * 6 * h;
    } else if (h < 2 / 6) {
      green = i;
      blue = i * (1 - s);
      red = green - (i - blue) * (6 * h - 1);
    } else if (h < 3 / 6) {
      green = i;
      red = i * (1 - s);
      blue = red + (i - red) * (6 * h - 2);
    } else if (h < 4 / 6) {
      blue = i;
      red = i * (1 - s);
      green = blue - (i - red) * (6 * h - 3);
    } else if (h < 5 / 6) {
      blue = i;
      green = i * (1 - s);
      red = green + (i - green) * (6 * h - 4);
    } else {
      red = i;
      green = i * (1 - s);
      blue = red - (i - green) * (6 * h - 5);
    }
    return [Math.floor(red * 255), Math.floor(green * 255), Math.floor(blue * 255)];
  }

  /**
   * Returns a recolored copy of an outfit atlas: every template pair (layer 1 mask)
   * is baked into the base cells using head/body/legs/feet colors. Drawing then
   * always uses layer 0 indices.
   */
  private coloredAtlas(
    lookType: number, entry: AppearanceEntry, img: HTMLImageElement,
    head: number, body: number, legs: number, feet: number,
  ): HTMLCanvasElement | HTMLImageElement {
    const hasTemplate = entry.groups.some((g) => g.layers >= 2);
    if (!hasTemplate || (head === 0 && body === 0 && legs === 0 && feet === 0)) return img;

    const key = `${lookType}:${head}.${body}.${legs}.${feet}`;
    const cached = this.coloredOutfits.get(key);
    if (cached) return cached;

    const canvas = document.createElement('canvas');
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
    ctx.drawImage(img, 0, 0);

    const colors = {
      head: AssetsService.outfitColor(head),
      body: AssetsService.outfitColor(body),
      legs: AssetsService.outfitColor(legs),
      feet: AssetsService.outfitColor(feet),
    };

    const { cellW, cellH, cols } = entry;
    for (const group of entry.groups) {
      if (group.layers < 2) continue;
      const pairs = group.count / group.layers;
      for (let p = 0; p < pairs; p++) {
        const baseIndex = group.start + p * group.layers;
        const maskIndex = baseIndex + 1;
        const bx = (baseIndex % cols) * cellW;
        const by = Math.floor(baseIndex / cols) * cellH;
        const mx = (maskIndex % cols) * cellW;
        const my = Math.floor(maskIndex / cols) * cellH;

        const base = ctx.getImageData(bx, by, cellW, cellH);
        const mask = ctx.getImageData(mx, my, cellW, cellH);
        const bd = base.data;
        const md = mask.data;
        for (let px = 0; px < bd.length; px += 4) {
          if (md[px + 3] === 0) continue;
          const r = md[px];
          const g = md[px + 1];
          const b = md[px + 2];
          let tint: [number, number, number] | null = null;
          if (r > 200 && g > 200 && b < 60) tint = colors.head;
          else if (r > 200 && g < 60 && b < 60) tint = colors.body;
          else if (r < 60 && g > 200 && b < 60) tint = colors.legs;
          else if (r < 60 && g < 60 && b > 200) tint = colors.feet;
          if (!tint) continue;
          bd[px] = (bd[px] * tint[0]) / 255;
          bd[px + 1] = (bd[px + 1] * tint[1]) / 255;
          bd[px + 2] = (bd[px + 2] * tint[2]) / 255;
        }
        ctx.putImageData(base, bx, by);
        // clear the mask cell so it is never drawn accidentally
        ctx.clearRect(mx, my, cellW, cellH);
      }
    }

    this.coloredOutfits.set(key, canvas);
    return canvas;
  }

  // ---------- drawing ----------

  /** Sprite index math shared by all categories (tibia .dat ordering). */
  private spriteIndex(group: SpriteGroup, phase: number, px: number, py: number, pz: number, layer: number): number {
    return ((((phase * group.patternZ + pz) * group.patternY + py) * group.patternX + px) * group.layers + layer);
  }

  private drawCell(
    ctx: CanvasRenderingContext2D, source: CanvasImageSource, entry: AppearanceEntry,
    index: number, dx: number, dy: number, scale: number,
  ): void {
    const sx = (index % entry.cols) * entry.cellW;
    const sy = Math.floor(index / entry.cols) * entry.cellH;
    // tibia draws anchored to the bottom-right of the tile
    const offX = (entry.cellW - 32) * scale;
    const offY = (entry.cellH - 32) * scale;
    ctx.drawImage(
      source, sx, sy, entry.cellW, entry.cellH,
      dx - offX, dy - offY, entry.cellW * scale, entry.cellH * scale,
    );
  }

  /**
   * Draws an outfit at (dx, dy) = top-left of its tile on screen.
   * dir: 0=N 1=E 2=S 3=W; phaseTimeMs drives walk/idle animation.
   */
  drawOutfit(
    ctx: CanvasRenderingContext2D, lookType: number, dx: number, dy: number, scale: number,
    dir: number, moving: boolean, phaseTimeMs: number,
    head = 0, body = 0, legs = 0, feet = 0, addons = 0, mountLookType = 0,
  ): void {
    const entry = this.entry('outfits', lookType);
    if (!entry) return;
    const img = this.imageSync(entry.file);
    if (!img) return;

    let group = entry.groups.find((g) => g.kind === (moving ? 'moving' : 'idle')) ?? entry.groups[0];
    if (!group) return;

    const phases = Math.max(group.count / (group.patternX * group.patternY * group.patternZ * group.layers), 1);
    const phaseDur = group.phases[0]?.[0] || (moving ? 110 : 300);
    const phase = Math.floor(phaseTimeMs / phaseDur) % phases;
    const px = Math.min(dir, group.patternX - 1);

    if (mountLookType > 0) {
      const mountEntry = this.entry('outfits', mountLookType);
      const mountImage = mountEntry ? this.imageSync(mountEntry.file) : null;
      const mountGroup = mountEntry?.groups.find((g) => g.kind === (moving ? 'moving' : 'idle'))
        ?? mountEntry?.groups[0];
      if (mountEntry && mountImage && mountGroup) {
        const mountPhases = Math.max(
          mountGroup.count
            / (mountGroup.patternX * mountGroup.patternY * mountGroup.patternZ * mountGroup.layers),
          1,
        );
        const mountDuration = mountGroup.phases[0]?.[0] || (moving ? 110 : 300);
        const mountPhase = Math.floor(phaseTimeMs / mountDuration) % mountPhases;
        const mountX = Math.min(dir, mountGroup.patternX - 1);
        const mountIndex = mountGroup.start + this.spriteIndex(mountGroup, mountPhase, mountX, 0, 0, 0);
        this.drawCell(ctx, mountImage, mountEntry, mountIndex, dx, dy, scale);
      }
    }

    const source = this.coloredAtlas(lookType, entry, img, head, body, legs, feet);
    const pz = mountLookType > 0 ? Math.min(1, group.patternZ - 1) : 0;

    const rows: number[] = [0];
    if (group.patternY > 1 && addons) {
      if (addons & 1) rows.push(1);
      if (addons & 2 && group.patternY > 2) rows.push(2);
    }

    for (const py of rows) {
      if (py >= group.patternY) continue;
      const index = group.start + this.spriteIndex(group, phase, px, py, pz, 0);
      if (index >= group.start + group.count) continue;
      this.drawCell(ctx, source, entry, index, dx, dy, scale);
    }
  }

  /**
   * Measures the opaque bounding box of an outfit's south idle frame (cached per lookType).
   * Sprites are stored in 32px or 64px cells anchored bottom-right of a 32px tile, so the actual
   * creature can sit anywhere in the cell; the bbox lets previews scale/center on real pixels.
   * Returns null until the atlas image is loaded (caller should retry / fall back).
   */
  private measureOutfitBBox(lookType: number): { minX: number; minY: number; bw: number; bh: number } | null {
    const cached = this.outfitBBoxes.get(lookType);
    if (cached !== undefined) return cached;
    const entry = this.entry('outfits', lookType);
    if (!entry) { this.outfitBBoxes.set(lookType, null); return null; }
    if (!this.imageSync(entry.file)) return null; // atlas not loaded yet — retry next frame

    const { cellW, cellH } = entry;
    const canvas = document.createElement('canvas');
    canvas.width = cellW;
    canvas.height = cellH;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return null;
    ctx.imageSmoothingEnabled = false;
    // south idle frame, no mount/recolor (alpha is colour-independent), cell anchored at (0,0)
    this.drawOutfit(ctx, lookType, cellW - 32, cellH - 32, 1, 2, false, 0);
    const data = ctx.getImageData(0, 0, cellW, cellH).data;
    let minX = cellW, minY = cellH, maxX = -1, maxY = -1;
    for (let y = 0; y < cellH; y++) {
      for (let x = 0; x < cellW; x++) {
        if (data[(y * cellW + x) * 4 + 3] > 8) {
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
        }
      }
    }
    if (maxX < 0) return null; // nothing drawn yet (image still decoding) — retry, do not cache
    const bbox = { minX, minY, bw: maxX - minX + 1, bh: maxY - minY + 1 };
    this.outfitBBoxes.set(lookType, bbox);
    return bbox;
  }

  /**
   * Draws an outfit scaled and centered to fit a square box of `boxSize` px — for static previews
   * and thumbnails. Unlike {@link drawOutfit} (which anchors to a 32px game tile and lets larger
   * 2×2 creatures like Cyclops or A Greedy Eye spill upward), this centres on the sprite's real
   * pixels and shrinks oversized creatures so they stay inside the box. 1-tile creatures keep their
   * legacy size (box/48); only content larger than a tile is scaled down.
   */
  drawOutfitFitted(
    ctx: CanvasRenderingContext2D, lookType: number, boxSize: number,
    dir: number, moving: boolean, phaseTimeMs: number,
    head = 0, body = 0, legs = 0, feet = 0, addons = 0, mountLookType = 0,
  ): void {
    const entry = this.entry('outfits', lookType);
    if (!entry) return;
    const { cellW, cellH } = entry;
    const bbox = this.measureOutfitBBox(lookType);
    let scale: number;
    let dx: number;
    let dy: number;
    if (bbox) {
      scale = Math.min(boxSize / 48, (boxSize / Math.max(bbox.bw, bbox.bh)) * 0.92);
      dx = (boxSize - bbox.bw * scale) / 2 - bbox.minX * scale + (cellW - 32) * scale;
      dy = (boxSize - bbox.bh * scale) / 2 - bbox.minY * scale + (cellH - 32) * scale;
    } else {
      // legacy fallback until the bbox can be measured (atlas still decoding)
      scale = boxSize / 48;
      dx = (boxSize - 32 * scale) / 2;
      dy = (boxSize - 32 * scale) / 2;
    }
    this.drawOutfit(ctx, lookType, dx, dy, scale, dir, moving, phaseTimeMs, head, body, legs, feet, addons, mountLookType);
  }

  /** Draws an object (item/tile). For grounds, pass tileX/tileY for pattern variation. */
  drawObject(
    ctx: CanvasRenderingContext2D, id: number, dx: number, dy: number, scale: number,
    tileX = 0, tileY = 0, phaseTimeMs = 0,
  ): void {
    const entry = this.entry('objects', id);
    if (!entry) return;
    const img = this.imageSync(entry.file);
    if (!img) return;
    const group = entry.groups[0];
    if (!group) return;

    const phases = Math.max(group.count / (group.patternX * group.patternY * group.patternZ * group.layers), 1);
    const phaseDur = group.phases[0]?.[0] || 400;
    const phase = phases > 1 ? Math.floor(phaseTimeMs / phaseDur) % phases : 0;
    const px = tileX % group.patternX;
    const py = tileY % group.patternY;
    const index = group.start + this.spriteIndex(group, phase, px, py, 0, 0);
    this.drawCell(ctx, img, entry, index, dx, dy, scale);
  }

  /** Draws one frame of a magic effect; returns false when the animation is over. */
  drawEffect(
    ctx: CanvasRenderingContext2D, id: number, dx: number, dy: number, scale: number, elapsedMs: number,
  ): boolean {
    const entry = this.entry('effects', id);
    if (!entry) return false;
    const img = this.imageSync(entry.file);
    if (!img) return true;
    const group = entry.groups[0];
    if (!group) return false;

    const phases = Math.max(group.count / (group.patternX * group.patternY * group.patternZ * group.layers), 1);
    const phaseDur = group.phases[0]?.[0] || 75;
    const phase = Math.floor(elapsedMs / phaseDur);
    if (phase >= phases) return false;
    const index = group.start + this.spriteIndex(group, phase, 0, 0, 0, 0);
    this.drawCell(ctx, img, entry, index, dx, dy, scale);
    return true;
  }

  /** Draws a missile oriented by its travel direction (3x3 directional pattern). */
  drawMissile(
    ctx: CanvasRenderingContext2D, id: number, dx: number, dy: number, scale: number,
    dirX: number, dirY: number,
  ): void {
    const entry = this.entry('missiles', id);
    if (!entry) return;
    const img = this.imageSync(entry.file);
    if (!img) return;
    const group = entry.groups[0];
    if (!group) return;

    const px = group.patternX >= 3 ? Math.sign(dirX) + 1 : 0;
    const py = group.patternY >= 3 ? Math.sign(dirY) + 1 : 0;
    const index = group.start + this.spriteIndex(group, 0, px, py, 0, 0);
    this.drawCell(ctx, img, entry, index, dx, dy, scale);
  }
}
