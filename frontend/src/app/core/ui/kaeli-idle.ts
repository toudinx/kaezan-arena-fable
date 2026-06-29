import {
  Component, OnDestroy, computed, effect, inject, input, signal,
} from '@angular/core';
import { ApiService } from '../api.service';
import { KaeliArtService } from '../kaeli-art.service';
import { OutfitPreview } from '../outfit-preview';

/**
 * Rotating Kaeli idle: 3 poses, swaps every `intervalMs` (default 7s) with a smooth crossfade and
 * no white flash (the previous pose only disappears after the next one is already in). Without
 * authored art, it falls back to the Tibia sprite (`app-outfit-preview`). Respects
 * `prefers-reduced-motion` (no rotation, shows static idle-1).
 *
 *   <app-kaeli-idle [waifuId]="id" [intervalMs]="7000" />
 *
 * Two stacked layers: `base` (always the current pose) and `fade` (the next one, rising from
 * opacity 0->1). At the end of the fade, `base` receives the same pose and `fade` is hidden; because
 * both show the same image at swap time, there is no blink.
 *
 * --- CUT-02 (in-engine idle breathing, definitive layer) -------------------------
 * `[breathing]` (on by default) applies a sinusoidal "breathing" motion (winning technique (a)
 * from spike CUT-01): looping CSS transform over the art, anchored at the feet (`transform-origin:
 * center bottom`) -> chest/shoulders rise while the base stays fixed. Free/in-engine/deterministic,
 * zero new assets, and no JS timer (pure CSS -> no leaks). Transform does not trigger layout
 * (no shift). It runs UNDER the pose crossfade (transform on `.art`, opacity on `.layer`; separate
 * axes, no competition). Amplitude and period are parameterized (`[breatheAmplitude]` in %, default
 * 0.7; `[breathePeriodMs]`, default 4500) and become CSS vars `--breathe-amp`/`--breathe-period`.
 * Disabled under `prefers-reduced-motion` (double guard: template class + `animation: none`).
 * ---------------------------------------------------------------------------------
 *
 * --- CUT-03 (premium idle loop via ComfyUI, opt-in) -------------------------------
 * When a Kaeli has an `idle-loop.webm` (manifest + dropped file; generated in ComfyUI/LivePortrait
 * from `idle-1`), play that organic loop INSTEAD OF crossfade+breathing CSS. It is purely opt-in:
 * without webm, nothing changes (falls back to CUT-02). The `<video>` is `muted`/`loop`/`playsinline`
 * (autoplay allowed) and uses `idle-1` as `poster` (no flash until the webm decodes). Two fallbacks
 * keep the path safe:
 *   (1) under `prefers-reduced-motion`, the webm is not used (shows frozen art/breathing);
 *   (2) if the webm fails (404/decode), `(error)` marks `videoFailed` -> back to breathing.
 * So "1 Kaeli with webm plays the loop; the others fall back to breathing CSS with no regression".
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

    /* CUT-03: premium loop. Same framing as poses (object-fit/position), so switching between webm
       and breathing CSS has no jump. No breathing on top; the movement already comes from the webm. */
    .video {
      width: 100%; height: 100%;
      object-fit: var(--fit, contain);
      object-position: center bottom;
      user-select: none; pointer-events: none;
    }

    /* CUT-02: sinusoidal breathing. Origin at the feet -> chest/shoulders rise, base fixed.
       Amplitude (--breathe-amp, in %) and period (--breathe-period) come from inputs; intentionally
       low defaults add "life" without drawing attention. translateY and scale derive from the same
       amplitude for cohesive motion (chest expands as it rises). */
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
  /** 'contain' (default) | 'cover' */
  fit = input<'contain' | 'cover'>('contain');
  /** Fallback sprite size (px); authored art fills the host. */
  spriteSize = input(220);
  /** CUT-02: enables sinusoidal CSS breathing over authored art (on by default). */
  breathing = input(true);
  /** Breathing amplitude as % of height (becomes `--breathe-amp`); subtle by default. */
  breatheAmplitude = input(0.7);
  /** Breathing cycle period in ms (becomes `--breathe-period`). */
  breathePeriodMs = input(4500);

  private readonly art = inject(KaeliArtService);
  private readonly api = inject(ApiService);

  readonly images = computed(() => this.art.idles(this.waifuId()));

  /** CUT-03: premium loop URL (`.webm`) when present; otherwise null. */
  readonly idleLoop = computed(() => this.art.idleLoop(this.waifuId()));
  /** Marked when the webm fails (404/decode) -> forces fallback to breathing CSS. */
  private readonly videoFailed = signal(false);
  /** Plays the webm only when the file is present, outside reduced-motion, and not failed. */
  readonly useVideo = computed(
    () => !!this.idleLoop() && !this.reduceMotion && !this.videoFailed(),
  );

  /** Fallback outfit: derived from the catalog (Kaeli default appearance). */
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
    // Restart rotation whenever the pose list changes (Kaeli swap).
    effect(() => {
      const imgs = this.images();
      this.waifuId();            // Kaeli swap rearms the webm (clears prior failure)
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
   * CUT-03: ensures `muted` on the property (Angular only sets the attribute, and without mute the
   * browser blocks autoplay) and triggers play. If browser policy refuses, the webm stays on the
   * poster (idle-1) without breaking anything.
   */
  onVideoReady(ev: Event): void {
    const el = ev.target as HTMLVideoElement;
    el.muted = true;
    void el.play().catch(() => { /* autoplay blocked: stays on the poster, no fatal error */ });
  }

  /** CUT-03: webm unavailable/corrupt -> gives up on the loop and uses breathing CSS. */
  onVideoError(): void {
    this.videoFailed.set(true);
  }

  private advance(): void {
    const imgs = this.images();
    if (imgs.length < 2 || this.destroyed) return;
    const nextIndex = (this.index + 1) % imgs.length;
    const url = imgs[nextIndex];
    // Preload the next pose before starting the fade to avoid flash.
    const pre = new Image();
    pre.decoding = 'async';
    pre.onload = () => {
      if (this.destroyed) return;
      this.fadeSrc.set(url);
      this.fadeOn.set(false);
      // next frame: enable opacity to trigger the 0->1 transition
      this.rafId = requestAnimationFrame(() => this.fadeOn.set(true));
      this.fadeTimeout = setTimeout(() => {
        if (this.destroyed) return;
        this.baseSrc.set(url);     // base becomes the new pose...
        this.fadeOn.set(false);    // ...and the fade layer disappears over an identical image
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
