import { Injectable, signal } from '@angular/core';

/**
 * Serve a arte autoral de personagem (idle/wallpaper/banner/...), uma preocupação
 * SEPARADA dos sprites do Tibia em-jogo (esses vivem no AssetsService). O mapeamento
 * arte↔id mora no frontend via `assets/kaelis/manifest.json` — WaifuDef não tem
 * campos de arte e não deve ganhar nenhum.
 *
 * Cada getter devolve uma URL pronta ou `null` quando a Kaeli não tem aquele asset;
 * os componentes caem no fallback (sprite Tibia / gradiente do elemento) nesse caso.
 */
@Injectable({ providedIn: 'root' })
export class KaeliArtService {
  /** manifest: id da Kaeli → nomes de asset presentes (sem extensão). */
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
      /* sem manifest = todas as Kaelis caem no fallback; não é erro fatal */
    } finally {
      this.loaded.set(true);
    }
  }

  /** Pasta do asset = id sem o prefixo `waifu:` (ex.: `waifu:velvet` → `velvet`). */
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

  /** URLs das poses de idle, em ordem (1→2→3). `[]` se a Kaeli não tem arte. */
  idles(id: string): string[] {
    return (this.manifest()[id] ?? [])
      .filter((n) => /^idle-\d+$/.test(n))
      .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
      .map((n) => this.url(id, n));
  }

  /**
   * CUT-03: loop premium de idle (`.webm`, gerado no ComfyUI/LivePortrait a partir do
   * `idle-1`). Presente só quando `idle-loop` está no manifest E o arquivo foi dropado —
   * caso contrário `null` e o `<app-kaeli-idle>` cai no breathing CSS (CUT-02). É o único
   * asset `.webm` (os demais são `.png`), por isso não passa pelo `url()`/`asset()` genéricos.
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

  /** Gradiente de fallback por elemento (SVG do Prompt 0). Sempre devolve uma URL. */
  elementGradient(element: string): string {
    const el = ELEMENT_PLACEHOLDERS.has(element) ? element : 'physical';
    return `/assets/kaelis/_placeholder/${el}.svg`;
  }
}

const ELEMENT_PLACEHOLDERS = new Set([
  'physical', 'fire', 'ice', 'energy', 'earth', 'death', 'holy',
]);
