import { Component, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { GAME_MODES, GameModeDef } from '../../core/game-modes';
import { TIER_BIOMES } from '../../core/tier-biomes';

@Component({
  selector: 'app-hunt',
  standalone: true,
  template: `
    <section class="hunt grain">
      <div class="scene" aria-hidden="true"></div>
      <div class="veil" aria-hidden="true"></div>

      <aside class="mode-rail" aria-label="Hunt modes">
        <span class="eyebrow">Hunt</span>
        @for (m of modes; track m.id) {
          <button class="rail-item mode"
                  [class.active]="selectedModeId() === m.id"
                  [style.--rail]="m.theme"
                  (click)="selectMode(m)">
            <span class="mode-mark">{{ m.icon }}</span>
            <span class="mode-copy">
              <b>{{ m.name }}</b>
              <small>{{ m.tagline }}</small>
            </span>
            @if (m.status !== 'live') { <span class="soon">Soon</span> }
          </button>
        }
      </aside>

      @if (selectedMode(); as m) {
        <main class="preview" [style.--mt]="m.theme">
          <div class="preview-top">
            <span class="mode-eyebrow">{{ m.tagline }}</span>
            <h1>{{ m.name }}</h1>
            <p>{{ m.description }}</p>
          </div>

          <div class="depth">
            <span class="depth-lbl">The descent - 5 strata</span>
            <div class="rungs">
              @for (b of strata; track b.tier) {
                <span class="rung" [style.--bc]="b.accent">
                  <i></i>
                  <b>{{ roman(b.tier) }}</b>
                  <small>{{ b.label }}</small>
                </span>
              }
            </div>
          </div>

          <div class="mode-facts">
            <span><b>5</b><small>Tiers</small></span>
            <span><b>2</b><small>Floors</small></span>
            <span><b>1</b><small>Boss</small></span>
          </div>

          <div class="actions">
            @if (m.status === 'live') {
              <button class="pill-btn" (click)="enter(m)">Enter expedition</button>
            } @else {
              <button class="pill-btn secondary" disabled>Soon</button>
            }
          </div>
        </main>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .hunt {
      position: relative;
      isolation: isolate;
      overflow: hidden;
      min-height: calc(100dvh - 53px);
      display: grid;
      grid-template-columns: minmax(250px, 330px) minmax(0, 1fr);
      gap: clamp(34px, 6vw, 110px);
      align-items: center;
      padding: clamp(28px, 5vh, 58px) clamp(22px, 6vw, 92px);
      background: var(--bg-0);
    }
    .scene {
      position: absolute;
      inset: 0;
      z-index: -4;
      background-image: url('/assets/biomes/tier-5.webp');
      background-size: cover;
      background-position: center;
      filter: saturate(0.8) brightness(0.72);
      transform: scale(1.03);
    }
    .veil {
      position: absolute;
      inset: 0;
      z-index: -3;
      pointer-events: none;
      background:
        radial-gradient(60% 76% at 18% 55%, rgba(123,107,242,0.24), transparent 68%),
        radial-gradient(44% 56% at 82% 18%, rgba(232,169,60,0.12), transparent 70%),
        linear-gradient(90deg, rgba(7,7,13,0.88), rgba(7,7,13,0.38) 46%, rgba(7,7,13,0.72)),
        linear-gradient(180deg, rgba(7,7,13,0.52), rgba(7,7,13,0.9));
    }

    .mode-rail {
      display: flex;
      flex-direction: column;
      gap: 12px;
      min-width: 0;
    }
    .mode-rail .eyebrow { margin-bottom: var(--sp-2); color: var(--accent-bright); }
    .mode-mark {
      width: 46px;
      height: 46px;
      display: grid;
      place-items: center;
      border-radius: 50%;
      border: 1px solid color-mix(in srgb, var(--rail) 55%, var(--line-strong));
      color: color-mix(in srgb, var(--rail) 70%, white);
      font-size: 1.25rem;
    }
    .mode-copy { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .mode-copy b {
      overflow: hidden;
      color: var(--text);
      font-family: var(--font-display);
      font-size: 1.18rem;
      font-weight: 650;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .mode-copy small { overflow: hidden; color: var(--text-mute); font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
    .soon {
      color: var(--text-faint);
      font-size: 9px;
      font-weight: 900;
      letter-spacing: 0.1em;
      text-transform: uppercase;
    }

    .preview {
      min-height: min(64vh, 620px);
      display: flex;
      flex-direction: column;
      justify-content: center;
      gap: clamp(22px, 4vh, 40px);
      max-width: 760px;
      justify-self: end;
      width: 100%;
    }
    .mode-eyebrow {
      color: color-mix(in srgb, var(--mt) 78%, white);
      font-size: var(--fs-xs);
      font-weight: 900;
      letter-spacing: var(--tracking-eyebrow);
      text-transform: uppercase;
    }
    .preview h1 {
      margin: var(--sp-2) 0 var(--sp-3);
      color: var(--text);
      font-family: var(--font-display);
      font-size: clamp(3.6rem, 8vw, 7.6rem);
      font-weight: 650;
      line-height: 0.9;
      text-shadow: 0 12px 48px rgba(0,0,0,0.72);
    }
    .preview p {
      max-width: 48ch;
      margin: 0;
      color: var(--text-dim);
      font-size: 1.05rem;
      line-height: var(--lh-body);
    }
    .depth { width: min(560px, 100%); }
    .depth-lbl {
      color: var(--text-mute);
      font-size: var(--fs-xs);
      font-weight: 800;
      letter-spacing: var(--tracking-eyebrow);
      text-transform: uppercase;
    }
    .rungs {
      position: relative;
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      gap: 0;
      margin-top: var(--sp-4);
    }
    .rungs::before {
      content: '';
      position: absolute;
      left: 8px;
      right: 8px;
      top: 9px;
      height: 1px;
      background: linear-gradient(90deg, #8cbf4d, #d99a3c, #a662ff, #ff6a3d, #7b6bf2);
      opacity: 0.62;
    }
    .rung {
      position: relative;
      z-index: 1;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 3px;
      min-width: 0;
    }
    .rung i {
      width: 18px;
      height: 18px;
      border: 1px solid color-mix(in srgb, var(--bc) 70%, white 12%);
      transform: rotate(45deg);
      background: color-mix(in srgb, var(--bc) 25%, rgba(7,7,13,0.8));
      box-shadow: 0 0 18px color-mix(in srgb, var(--bc) 45%, transparent);
    }
    .rung b { margin-top: 8px; color: var(--bc); font-family: var(--font-display); font-size: 1rem; font-weight: 700; line-height: 1; }
    .rung small { color: var(--text-mute); font-size: 11px; }
    .mode-facts {
      display: flex;
      flex-wrap: wrap;
      gap: clamp(24px, 5vw, 70px);
      padding: var(--sp-4) 0;
      border-top: 1px solid var(--line);
      border-bottom: 1px solid var(--line);
    }
    .mode-facts span { display: flex; flex-direction: column; gap: 4px; }
    .mode-facts b { color: var(--text); font-family: var(--font-display); font-size: 2rem; font-weight: 650; line-height: 1; }
    .mode-facts small { color: var(--text-mute); font-size: 10px; font-weight: 900; letter-spacing: 0.1em; text-transform: uppercase; }
    .actions { display: flex; justify-content: flex-end; }

    @media (max-width: 880px) {
      .hunt { grid-template-columns: 1fr; align-items: start; padding: var(--sp-5) var(--sp-4); }
      .preview { justify-self: stretch; min-height: auto; }
      .preview h1 { font-size: clamp(3rem, 16vw, 5rem); }
      .actions { justify-content: stretch; }
      .pill-btn { width: 100%; }
    }
  `],
})
export class HuntPage {
  readonly modes = GAME_MODES;
  readonly selectedModeId = signal(this.modes.find((m) => m.status === 'live')?.id ?? this.modes[0]?.id ?? '');
  readonly selectedMode = computed(() => this.modes.find((m) => m.id === this.selectedModeId()) ?? null);
  readonly strata = Object.entries(TIER_BIOMES).map(([tier, b]) => ({ tier: Number(tier), ...b }));

  constructor(private readonly router: Router) {}

  selectMode(mode: GameModeDef): void {
    this.selectedModeId.set(mode.id);
  }

  roman(tier: number): string {
    return ['I', 'II', 'III', 'IV', 'V'][tier - 1] ?? String(tier);
  }

  enter(mode: GameModeDef): void {
    if (mode.status !== 'live') return;
    // Training skips tier selection: straight to the Kaeli picker (fixed tier 1) flagged as a training run.
    if (mode.id === 'training') {
      void this.router.navigate(['/play', 1], { queryParams: { mode: 'training', runs: 1 } });
      return;
    }
    void this.router.navigate(['/hunt', mode.id]);
  }
}
