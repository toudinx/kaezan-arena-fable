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
 *
 * --- CUT-02 (idle breathing in-engine — camada definitiva) ------------------------
 * `[breathing]` (on por padrão) aplica uma "respiração" senoidal (técnica (a) vencedora no
 * spike CUT-01): transform CSS em loop sobre a arte, ancorado nos pés (`transform-origin:
 * center bottom`) → tórax/ombros sobem com a base fixa. Grátis/in-engine/determinístico, zero
 * asset novo, e sem JS timer (CSS puro → não vaza). Transform não dispara layout (sem shift).
 * Atua SOB o crossfade de poses (transform na `.art`, opacity nas `.layer` — eixos disjuntos,
 * não competem). Amplitude e período são parametrizáveis (`[breatheAmplitude]` em %, default
 * 0.7; `[breathePeriodMs]`, default 4500) e viram as CSS vars `--breathe-amp`/`--breathe-period`.
 * Desliga sob `prefers-reduced-motion` (guarda dupla: classe no template + `animation: none`).
 * ---------------------------------------------------------------------------------
 *
 * --- CUT-03 (idle loop premium via ComfyUI — opt-in) ------------------------------
 * Quando a Kaeli tem um `idle-loop.webm` (manifest + arquivo dropado; gerado no
 * ComfyUI/LivePortrait a partir do `idle-1`), tocamos esse loop orgânico NO LUGAR do
 * crossfade+breathing CSS. É puramente opt-in: sem webm, nada muda (cai no CUT-02). O
 * `<video>` é `muted`/`loop`/`playsinline` (autoplay permitido) e usa `idle-1` como
 * `poster` (sem flash até o webm decodificar). Dois fallbacks tornam o caminho seguro:
 *   (1) sob `prefers-reduced-motion` o webm nem entra (mostra arte/breathing congelado);
 *   (2) se o webm falhar (404/decode), `(error)` marca `videoFailed` → volta pro breathing.
 * Assim "1 Kaeli com webm toca o loop; as demais caem no breathing CSS sem regressão".
 * ---------------------------------------------------------------------------------
 */
@Component({
  selector: 'app-kaeli-idle',
  standalone: true,
  imports: [OutfitPreview],
  template: `
    @if (useVideo()) {
      <video class="art video" [style.--fit]="fit()" [src]="idleLoop()"
             [poster]="images()[0]" [muted]="true" (loadedmetadata)="onVideoReady($event)"
             (error)="onVideoError()" autoplay loop playsinline preload="auto"></video>
    } @else if (images().length > 0) {
      <div class="art" [class.breathe]="breathing() && !reduceMotion" [style.--fit]="fit()"
           [style.--breathe-amp]="breatheAmplitude()" [style.--breathe-period]="breathePeriodMs() + 'ms'">
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

    /* CUT-03: loop premium. Mesmo enquadramento das poses (object-fit/position) p/ trocar
       sem "pulo" entre webm e breathing CSS. Sem breathing por cima — o movimento já vem no webm. */
    .video {
      width: 100%; height: 100%;
      object-fit: var(--fit, contain);
      object-position: center bottom;
      user-select: none; pointer-events: none;
    }

    /* CUT-02: respiração senoidal. Origem nos pés -> tórax/ombros sobem, base fixa.
       Amplitude (--breathe-amp, em %) e período (--breathe-period) vêm dos inputs;
       defaults propositalmente baixos p/ "vida" sem chamar atenção. translateY e scale
       derivam da mesma amplitude p/ um movimento coeso (peito infla ao subir). */
    .breathe {
      transform-origin: center bottom;
      animation: kaeli-breathe var(--breathe-period, 4500ms) ease-in-out infinite;
      will-change: transform;
    }
    @keyframes kaeli-breathe {
      0%, 100% { transform: translateY(0) scaleY(1) scaleX(1); }
      50%      {
        transform:
          translateY(calc(var(--breathe-amp, 0.7) * -1%))
          scaleY(calc(1 + var(--breathe-amp, 0.7) * 0.017))
          scaleX(calc(1 - var(--breathe-amp, 0.7) * 0.009));
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .fade { transition: none; }
      .breathe { animation: none; }
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
  /** CUT-02: liga a respiração CSS senoidal sobre a arte autoral (on por padrão). */
  breathing = input(true);
  /** Amplitude da respiração em % de altura (vira `--breathe-amp`); sutil por padrão. */
  breatheAmplitude = input(0.7);
  /** Período do ciclo de respiração em ms (vira `--breathe-period`). */
  breathePeriodMs = input(4500);

  private readonly art = inject(KaeliArtService);
  private readonly api = inject(ApiService);

  readonly images = computed(() => this.art.idles(this.waifuId()));

  /** CUT-03: URL do loop premium (`.webm`) quando existe; senão null. */
  readonly idleLoop = computed(() => this.art.idleLoop(this.waifuId()));
  /** Marcado quando o webm falha (404/decode) → força o fallback pro breathing CSS. */
  private readonly videoFailed = signal(false);
  /** Toca o webm só com arquivo presente, fora de reduced-motion e sem ter falhado. */
  readonly useVideo = computed(
    () => !!this.idleLoop() && !this.reduceMotion && !this.videoFailed(),
  );

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

  protected readonly reduceMotion =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    // Reinicia a rotação sempre que a lista de poses muda (troca de Kaeli).
    effect(() => {
      const imgs = this.images();
      this.waifuId();            // troca de Kaeli rearma o webm (limpa falha anterior)
      this.videoFailed.set(false);
      this.reset();
      if (imgs.length === 0) return;
      this.index = 0;
      this.baseSrc.set(imgs[0]);
      if (imgs.length > 1 && !this.reduceMotion) {
        this.timer = setInterval(() => this.advance(), Math.max(1000, this.intervalMs()));
      }
    });
  }

  /**
   * CUT-03: garante `muted` no *property* (Angular só seta o atributo, e sem mute o
   * navegador bloqueia o autoplay) e dispara o play. Se a política do navegador recusar,
   * o webm fica no poster (idle-1) — sem quebrar nada.
   */
  onVideoReady(ev: Event): void {
    const el = ev.target as HTMLVideoElement;
    el.muted = true;
    void el.play().catch(() => { /* autoplay bloqueado: fica no poster, sem erro fatal */ });
  }

  /** CUT-03: webm indisponível/corrompido → desiste do loop e usa o breathing CSS. */
  onVideoError(): void {
    this.videoFailed.set(true);
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
