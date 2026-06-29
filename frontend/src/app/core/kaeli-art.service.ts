import { Injectable, signal } from '@angular/core';

/**
 * Serves authored character art (idle/wallpaper/banner/...), a SEPARATE concern from in-game
 * Tibia sprites (those live in AssetsService). The art<->id mapping lives on the frontend through
 * `assets/kaelis/manifest.json`; WaifuDef has no art fields and should not gain any.
 *
 * Each getter returns a ready URL or `null` when the Kaeli lacks that asset; components fall back
 * to the Tibia sprite / element gradient in that case.
 */
@Injectable({ providedIn: 'root' })
export class KaeliArtService {
  /** Manifest: Kaeli id -> present asset names (without extension). */
  private readonly manifest = signal<Record<string, string[]>>({});
  readonly loaded = signal(false);

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const res = await fetch('/assets/kaelis/manifest.json');
      if (res.ok) this.manifest.set((await res.json()) as Record<string, string[]>);
    } catch {
      /* no manifest = every Kaeli uses fallback art; not a fatal error */
    } finally {
      this.loaded.set(true);
    }
  }

  /** Asset folder = id without the `waifu:` prefix (for example, `waifu:velvet` -> `velvet`). */
  private folder(id: string): string {
    return id.startsWith('waifu:') ? id.slice('waifu:'.length) : id;
  }

  private has(id: string, name: string): boolean {
    return this.manifest()[id]?.includes(name) ?? false;
  }

  private url(id: string, name: string): string {
    return `/assets/kaelis/${this.folder(id)}/${name}.png`;
  }

  private asset(id: string, name: string): string | null {
    return this.has(id, name) ? this.url(id, name) : null;
  }

  /** Idle pose URLs, in order (1->2->3). `[]` if the Kaeli has no art. */
  idles(id: string): string[] {
    return (this.manifest()[id] ?? [])
      .filter((n) => /^idle-\d+$/.test(n))
      .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
      .map((n) => this.url(id, n));
  }

  /**
   * CUT-03: premium idle loop (`.webm`, generated in ComfyUI/LivePortrait from `idle-1`). Present
   * only when `idle-loop` is in the manifest AND the file was dropped in; otherwise `null` and
   * `<app-kaeli-idle>` falls back to breathing CSS (CUT-02). It is the only `.webm` asset (the
   * others are `.png`), so it does not go through the generic `url()`/`asset()` path.
   */
  idleLoop(id: string): string | null {
    return this.has(id, 'idle-loop')
      ? `/assets/kaelis/${this.folder(id)}/idle-loop.webm`
      : null;
  }

  wallpaper(id: string): string | null { return this.asset(id, 'wallpaper'); }
  bgLandscape(id: string): string | null { return this.asset(id, 'bg-landscape'); }
  bgPortrait(id: string): string | null { return this.asset(id, 'bg-portrait'); }
  banner(id: string): string | null { return this.asset(id, 'banner'); }
  thumb(id: string): string | null { return this.asset(id, 'thumb'); }

  /** Fallback gradient by element (Prompt 0 SVG). Always returns a URL. */
  elementGradient(element: string): string {
    const el = ELEMENT_PLACEHOLDERS.has(element) ? element : 'physical';
    return `/assets/kaelis/_placeholder/${el}.svg`;
  }
}

const ELEMENT_PLACEHOLDERS = new Set([
  'physical', 'fire', 'ice', 'energy', 'earth', 'death', 'holy',
]);
