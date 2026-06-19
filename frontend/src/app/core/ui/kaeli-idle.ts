import {
  Component, OnDestroy, computed, effect, inject, input, signal,
} from '@angular/core';
import { ApiService } from '../api.service';
import { KaeliArtService } from '../kaeli-art.service';
import { OutfitPreview } from '../outfit-preview';

/**
 * Idle rotativo da Kaeli: 3 poses, troca a cada `intervalMs` (default 7s) com
 * crossfade suave e sem flash branco (a pose anterior só some depois que a próxima
 * já entrou). Sem arte autoral, cai no sprite Tibia (`app-outfit-preview`).
 * Respeita `prefers-reduced-motion` (sem rotação, mostra idle-1 estático).
 *
 *   <app-kaeli-idle [waifuId]="id" [intervalMs]="7000" />
 *
 * Duas camadas empilhadas: `base` (sempre a pose atual) e `fade` (a próxima, que
 * sobe de opacity 0→1). No fim do fade, `base` recebe a mesma pose e `fade` é
 * escondida — como ambas mostram a mesma imagem no instante da troca, não pisca.
 */
@Component({
  selector: 'app-kaeli-idle',
  standalone: true,
  imports: [OutfitPreview],
  template: `
    @if (images().length > 0) {
      <div class="art" [style.--fit]="fit()">
        <img class="layer base" [src]="baseSrc()" alt="" decoding="async" draggable="false" />
        @if (fadeSrc()) {
          <img class="layer fade" [class.on]="fadeOn()" [src]="fadeSrc()" alt="" decoding="async" draggable="false" />
        }
      </div>
    } @else if (sprite(); as sp) {
      <div class="art sprite">
        <app-outfit-preview
          [lookType]="sp.lookType" [head]="sp.head" [body]="sp.body"
          [legs]="sp.legs" [feet]="sp.feet" [addons]="sp.addons"
          [mountLookType]="sp.mountLookType" [size]="spriteSize()" />
      </div>
    }
  `,
  styles: [`
    :host { display: block; position: relative; width: 100%; height: 100%; }
    .art { position: absolute; inset: 0; }
    .layer {
      position: absolute; inset: 0;
      width: 100%; height: 100%;
      object-fit: var(--fit, contain);
      object-position: center bottom;
      user-select: none;
    }
    .fade { opacity: 0; transition: opacity 800ms var(--ease-out, ease); }
    .fade.on { opacity: 1; }
    .sprite { display: flex; align-items: flex-end; justify-content: center; }
    @media (prefers-reduced-motion: reduce) {
      .fade { transition: none; }
    }
  `],
})
export class KaeliIdle implements OnDestroy {
  waifuId = input.required<string>();
  intervalMs = input(7000);
  /** 'contain' (padrão) | 'cover' */
  fit = input<'contain' | 'cover'>('contain');
  /** tamanho do sprite de fallback (px); a arte autoral preenche o host. */
  spriteSize = input(220);

  private readonly art = inject(KaeliArtService);
  private readonly api = inject(ApiService);

  readonly images = computed(() => this.art.idles(this.waifuId()));

  /** Outfit do fallback: derivado do catálogo (aparência padrão da Kaeli). */
  readonly sprite = computed(() => {
    const w = this.api.catalog()?.waifus.find((x) => x.id === this.waifuId());
    if (!w) return null;
    return {
      lookType: w.lookType, head: w.head, body: w.body,
      legs: w.legs, feet: w.feet, addons: 0, mountLookType: 0,
    };
  });

  readonly baseSrc = signal('');
  readonly fadeSrc = signal('');
  readonly fadeOn = signal(false);

  private index = 0;
  private timer: ReturnType<typeof setInterval> | null = null;
  private fadeTimeout: ReturnType<typeof setTimeout> | null = null;
  private rafId = 0;
  private destroyed = false;

  private readonly reduceMotion =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    // Reinicia a rotação sempre que a lista de poses muda (troca de Kaeli).
    effect(() => {
      const imgs = this.images();
      this.reset();
      if (imgs.length === 0) return;
      this.index = 0;
      this.baseSrc.set(imgs[0]);
      if (imgs.length > 1 && !this.reduceMotion) {
        this.timer = setInterval(() => this.advance(), Math.max(1000, this.intervalMs()));
      }
    });
  }

  private advance(): void {
    const imgs = this.images();
    if (imgs.length < 2 || this.destroyed) return;
    const nextIndex = (this.index + 1) % imgs.length;
    const url = imgs[nextIndex];
    // Pré-carrega a próxima pose antes de iniciar o fade — evita flash.
    const pre = new Image();
    pre.decoding = 'async';
    pre.onload = () => {
      if (this.destroyed) return;
      this.fadeSrc.set(url);
      this.fadeOn.set(false);
      // próximo frame: liga a opacity para disparar a transição 0→1
      this.rafId = requestAnimationFrame(() => this.fadeOn.set(true));
      this.fadeTimeout = setTimeout(() => {
        if (this.destroyed) return;
        this.baseSrc.set(url);     // base passa a ser a nova pose...
        this.fadeOn.set(false);    // ...e a camada de fade some sobre imagem idêntica
        this.fadeSrc.set('');
        this.index = nextIndex;
      }, 850);
    };
    pre.src = url;
  }

  private reset(): void {
    if (this.timer) { clearInterval(this.timer); this.timer = null; }
    if (this.fadeTimeout) { clearTimeout(this.fadeTimeout); this.fadeTimeout = null; }
    if (this.rafId) { cancelAnimationFrame(this.rafId); this.rafId = 0; }
    this.fadeOn.set(false);
    this.fadeSrc.set('');
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.reset();
  }
}
