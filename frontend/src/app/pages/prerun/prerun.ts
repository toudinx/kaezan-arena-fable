import { Component, OnDestroy, computed, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { normalizeFarmRunCount, readFarmRunCount } from '../../core/farm-settings';
import { KaeliArtService } from '../../core/kaeli-art.service';
import { OutfitPreview } from '../../core/outfit-preview';
import { tierBiome } from '../../core/tier-biomes';
import { ELEMENT_LABELS, RARITY_COLORS, SkinDef, WaifuDef } from '../../core/types';

/** Tela de deploy: escolher a Kaeli da hunt, sem editar build/equipamento. */
@Component({
  selector: 'app-prerun',
  standalone: true,
  imports: [OutfitPreview],
  template: `
    <section class="page grain"
             [style.--bc]="biome(tierNum()).accent"
             [style.--bd]="biome(tierNum()).deep"
             [style.--bg-img]="'url(' + biome(tierNum()).bg + ')'">
      <div class="scene" aria-hidden="true"></div>
      <div class="wash" aria-hidden="true"></div>
      <div class="tier-ghost" aria-hidden="true">{{ roman(tierNum()) }}</div>

      <button class="back" (click)="back()">‹ Voltar aos tiers</button>

      @if (tierDef(); as t) {
        <main class="deploy">
          <aside class="selector" aria-label="Selecionar Kaeli">
            <div class="selector-head">
              <span class="tier-badge">Tier {{ t.tier }} · {{ biome(t.tier).label }} · ×{{ t.statMultiplier }}</span>
              <h1>{{ t.name }}</h1>
              <p>Escolha quem vai encarar <b>{{ t.boss }}</b>.</p>
            </div>

            <div class="roster">
              @for (w of ownedWaifus(); track w.id) {
                <button class="kaeli-row"
                        [class.active]="selected()?.id === w.id"
                        [style.--rc]="rarityColor(w.rarity)"
                        (click)="select(w)">
                  <span class="portrait">
                    @if (thumb(w.id); as art) {
                      <img class="thumb" [src]="art" [alt]="w.name" decoding="async" />
                    } @else {
                      <app-outfit-preview
                        [lookType]="skinFor(w).lookType" [head]="skinFor(w).head" [body]="skinFor(w).body"
                        [legs]="skinFor(w).legs" [feet]="skinFor(w).feet" [addons]="skinFor(w).addons ?? 0"
                        [mountLookType]="skinFor(w).mountLookType ?? 0" [size]="50" [animate]="false" />
                    }
                  </span>
                  <span class="row-copy">
                    <b>{{ w.name }}</b>
                    <small>{{ elementLabel(w.element) }}</small>
                    <span class="stars" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</span>
                  </span>
                </button>
              } @empty {
                <p class="muted">Você ainda não recrutou nenhuma Kaeli.</p>
              }
            </div>
          </aside>

          @if (selected(); as w) {
            <section class="hero" [style.--rc]="rarityColor(w.rarity)">
              <div class="hero-copy">
                <span class="hero-eyebrow">{{ elementLabel(w.element) }}</span>
                <h2>{{ w.name }}</h2>
                <p>{{ w.title }}</p>
              </div>

              <div class="art-stage">
                <app-outfit-preview
                  [lookType]="skinFor(w).lookType" [head]="skinFor(w).head" [body]="skinFor(w).body"
                  [legs]="skinFor(w).legs" [feet]="skinFor(w).feet" [addons]="skinFor(w).addons ?? 0"
                  [mountLookType]="skinFor(w).mountLookType ?? 0" [size]="360" />
                <div class="floor-glow" aria-hidden="true"></div>
              </div>

              <div class="actions">
                <span class="run-plan">{{ runCount() }} run{{ runCount() > 1 ? 's' : '' }}</span>
                <button class="pill-btn secondary" (click)="details(w)">Detalhes</button>
                <button class="pill-btn" (click)="enter()">Continuar</button>
              </div>
            </section>
          }
        </main>
      } @else {
        <p class="muted">Carregando tier...</p>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .page {
      position: relative;
      isolation: isolate;
      overflow: hidden;
      height: calc(100dvh - 64px);
      min-height: 640px;
      padding: clamp(12px, 2vw, 24px) clamp(20px, 5vw, 76px) clamp(16px, 3vw, 30px);
      background: var(--bg-0);
    }
    .scene {
      position: absolute;
      inset: 0;
      z-index: -4;
      background-image: var(--bg-img);
      background-size: cover;
      background-position: center;
      filter: saturate(0.72) brightness(0.62);
      transform: scale(1.03);
    }
    .wash {
      position: absolute;
      inset: 0;
      z-index: -3;
      pointer-events: none;
      background:
        radial-gradient(50% 70% at 68% 48%, color-mix(in srgb, var(--bc) 22%, transparent), transparent 66%),
        linear-gradient(90deg, rgba(7,7,13,0.9), rgba(7,7,13,0.52) 42%, rgba(7,7,13,0.86)),
        linear-gradient(180deg, rgba(7,7,13,0.56), rgba(7,7,13,0.92));
    }
    .tier-ghost {
      position: absolute;
      right: clamp(80px, 14vw, 220px);
      top: clamp(56px, 10vh, 118px);
      z-index: -2;
      color: color-mix(in srgb, var(--bc) 34%, white);
      font-family: var(--font-display);
      font-size: clamp(11rem, 30vw, 26rem);
      font-weight: 650;
      line-height: 0.8;
      opacity: 0.14;
      pointer-events: none;
    }
    .back {
      position: relative;
      z-index: 2;
      background: none;
      border: none;
      color: var(--text-dim);
      font-size: var(--fs-sm);
      padding: 0 0 var(--sp-3);
    }
    .back:hover { color: var(--accent-bright); }
    .back:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; border-radius: var(--r-sm); }

    .deploy {
      position: relative;
      z-index: 1;
      height: calc(100% - 34px);
      min-height: 0;
      display: grid;
      grid-template-columns: minmax(430px, 540px) minmax(0, 1fr);
      gap: clamp(28px, 5vw, 88px);
      align-items: center;
      max-width: 1320px;
      margin: 0 auto;
    }
    .selector {
      display: flex;
      flex-direction: column;
      gap: var(--sp-4);
      min-width: 0;
    }
    .tier-badge {
      color: var(--bc);
      font-size: var(--fs-xs);
      font-weight: 900;
      letter-spacing: var(--tracking-eyebrow);
      text-transform: uppercase;
    }
    .selector h1 {
      margin: var(--sp-2) 0 var(--sp-2);
      color: var(--text);
      font-family: var(--font-display);
      font-size: clamp(2.6rem, 4.7vw, 4.6rem);
      font-weight: 650;
      line-height: 0.94;
      text-shadow: 0 10px 42px rgba(0,0,0,0.75);
    }
    .selector p { max-width: 34ch; margin: 0; color: var(--text-dim); }
    .selector p b { color: color-mix(in srgb, var(--bc) 78%, white); }

    .roster {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px;
      overflow: visible;
    }
    .kaeli-row {
      display: grid;
      grid-template-columns: 54px minmax(0, 1fr);
      align-items: center;
      gap: 10px;
      min-height: 68px;
      padding: 7px 9px 7px 7px;
      border: 1px solid var(--line);
      border-radius: var(--r-md);
      background: rgba(14, 14, 24, 0.48);
      color: inherit;
      text-align: left;
      -webkit-backdrop-filter: blur(12px);
      backdrop-filter: blur(12px);
      box-shadow: var(--glass-edge);
      transition: transform var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out),
        background var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .kaeli-row:hover {
      transform: translateX(3px);
      border-color: color-mix(in srgb, var(--rc) 64%, var(--line-strong));
    }
    .kaeli-row:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; }
    .kaeli-row.active {
      border-color: color-mix(in srgb, var(--rc) 74%, white 10%);
      background: color-mix(in srgb, var(--rc) 13%, rgba(14,14,24,0.62));
      box-shadow: var(--glass-edge), 0 0 30px color-mix(in srgb, var(--rc) 18%, transparent);
    }
    .portrait {
      width: 52px;
      height: 52px;
      display: grid;
      place-items: center;
      border-radius: var(--r-sm);
      background: radial-gradient(circle, color-mix(in srgb, var(--rc) 24%, var(--bg-2)), rgba(7,7,13,0.72));
      overflow: hidden;
    }
    .thumb {
      width: 100%;
      height: 100%;
      object-fit: cover;
      object-position: center top;
    }
    .row-copy { display: flex; flex-direction: column; min-width: 0; gap: 1px; }
    .row-copy b { overflow: hidden; color: var(--text); font-weight: 800; text-overflow: ellipsis; white-space: nowrap; }
    .row-copy small { color: var(--text-mute); font-size: 11px; }
    .stars { font-size: 10px; letter-spacing: 1px; line-height: 1; white-space: nowrap; }

    .hero {
      position: relative;
      height: 100%;
      min-height: 0;
      display: grid;
      grid-template-rows: auto minmax(0, 1fr) auto;
      align-items: stretch;
      min-width: 0;
    }
    .hero-copy {
      position: relative;
      z-index: 2;
      align-self: start;
      max-width: 48ch;
      justify-self: end;
      text-align: right;
    }
    .hero-eyebrow {
      color: color-mix(in srgb, var(--rc) 72%, white);
      font-size: var(--fs-xs);
      font-weight: 900;
      letter-spacing: var(--tracking-eyebrow);
      text-transform: uppercase;
    }
    .hero h2 {
      margin: var(--sp-2) 0 var(--sp-1);
      color: var(--text);
      font-family: var(--font-display);
      font-size: clamp(3.8rem, 8vw, 7.4rem);
      font-weight: 650;
      line-height: 0.88;
      text-shadow: 0 12px 46px rgba(0,0,0,0.78);
    }
    .hero p {
      margin: 0;
      color: var(--text-dim);
      font-family: var(--font-display);
      font-style: italic;
      font-size: 1rem;
    }
    .art-stage {
      --sprite-lift: clamp(34px, 5vh, 68px);
      position: relative;
      display: grid;
      place-items: end center;
      min-height: 0;
      overflow: visible;
    }
    .art-stage app-outfit-preview {
      position: relative;
      z-index: 1;
      image-rendering: pixelated;
      filter: drop-shadow(0 24px 32px rgba(0,0,0,0.72));
      transform: translateY(calc(-1 * var(--sprite-lift)));
    }
    .floor-glow {
      position: absolute;
      left: 50%;
      bottom: calc(3% + var(--sprite-lift));
      width: min(540px, 78%);
      height: 92px;
      transform: translateX(-50%);
      border-radius: 50%;
      background: radial-gradient(ellipse at center, color-mix(in srgb, var(--rc) 42%, transparent), transparent 70%);
      filter: blur(18px);
      opacity: 0.82;
    }
    .actions {
      position: relative;
      z-index: 2;
      display: flex;
      justify-content: flex-end;
      gap: var(--sp-3);
      flex-wrap: wrap;
      padding-top: var(--sp-3);
    }
    .run-plan {
      display: inline-flex;
      align-items: center;
      min-height: 42px;
      padding: 0 14px;
      border: 1px solid var(--line-strong);
      border-radius: var(--r-full);
      background: rgba(14, 14, 24, 0.58);
      color: var(--text-dim);
      font-size: var(--fs-sm);
      font-weight: 800;
    }
    .muted { color: var(--text-mute); }

    @media (max-width: 940px) {
      .page { height: auto; min-height: calc(100dvh - 64px); overflow: auto; }
      .deploy { grid-template-columns: 1fr; align-items: start; gap: var(--sp-6); }
      .selector { order: 2; }
      .hero { order: 1; min-height: auto; }
      .hero-copy { justify-self: start; text-align: left; }
      .hero h2 { font-size: clamp(3.2rem, 16vw, 5.2rem); }
      .actions { justify-content: stretch; }
      .pill-btn { flex: 1 1 180px; }
      .roster { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
  `],
})
export class PrerunPage implements OnDestroy {
  readonly tierNum = signal(1);
  readonly selected = signal<WaifuDef | null>(null);
  readonly runCount = signal(1);

  readonly tierDef = computed(() =>
    this.api.catalog()?.tiers.find((t) => t.tier === this.tierNum()) ?? null);

  readonly ownedWaifus = computed(() => {
    const cat = this.api.catalog();
    const acc = this.api.account();
    if (!cat || !acc) return [];
    return cat.waifus
      .filter((w) => acc.ownedWaifus.includes(w.id))
      .sort((a, b) => b.rarity - a.rarity || a.name.localeCompare(b.name));
  });

  private initTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly art: KaeliArtService,
  ) {
    this.tierNum.set(Number(this.route.snapshot.paramMap.get('tier') ?? '1'));
    this.runCount.set(normalizeFarmRunCount(Number(this.route.snapshot.queryParamMap.get('runs') ?? readFarmRunCount())));
    this.initTimer = setInterval(() => {
      if (this.selected()) { this.stopInit(); return; }
      const owned = this.ownedWaifus();
      const acc = this.api.account();
      if (owned.length && acc) {
        this.selected.set(owned.find((w) => w.id === acc.activeWaifuId) ?? owned[0]);
        this.stopInit();
      }
    }, 150);
  }

  private stopInit(): void {
    if (this.initTimer !== null) { clearInterval(this.initTimer); this.initTimer = null; }
  }

  ngOnDestroy(): void { this.stopInit(); }

  rarityColor(r: number): string { return RARITY_COLORS[r] ?? 'var(--text)'; }
  elementLabel(e: string): string { return ELEMENT_LABELS[e] ?? e; }
  biome(tier: number) { return tierBiome(tier); }
  roman(tier: number): string { return ['I', 'II', 'III', 'IV', 'V'][tier - 1] ?? String(tier); }

  thumb(id: string): string | null { return this.art.thumb(id); }

  skinFor(w: WaifuDef): SkinDef {
    const selectedId = this.api.account()?.selectedSkins?.[w.id];
    return w.skins.find((s) => s.id === selectedId) ?? w.skins[0];
  }

  select(w: WaifuDef): void {
    this.selected.set(w);
  }

  details(w: WaifuDef): void {
    void this.router.navigate(['/kaelis'], { queryParams: { waifu: w.id } });
  }

  enter(): void {
    const w = this.selected();
    if (!w) return;
    void this.router.navigate(['/game', this.tierNum()], { queryParams: { waifu: w.id, runs: this.runCount() } });
  }

  back(): void { void this.router.navigate(['/hunt', 'dungeon']); }
}
