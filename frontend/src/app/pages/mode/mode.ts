import { Component, computed, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { readFarmRunCount, normalizeFarmRunCount, writeFarmRunCount } from '../../core/farm-settings';
import { GAME_MODES } from '../../core/game-modes';
import { ItemIcon } from '../../core/item-icon';
import { OutfitPreview } from '../../core/outfit-preview';
import { tierBiome } from '../../core/tier-biomes';
import { MonsterCatalogEntry } from '../../core/types';

/** Tela de selecao interna de um modo. Por enquanto so "dungeon" (descida em 5 tiers). */
@Component({
  selector: 'app-mode',
  standalone: true,
  imports: [OutfitPreview, ItemIcon],
  template: `
    @if (mode(); as m) {
      <section class="page grain"
               [style.--bc]="m.id === 'dungeon' ? biome(selectedNum()).accent : 'var(--accent)'"
               [style.--bd]="m.id === 'dungeon' ? biome(selectedNum()).deep : 'var(--bg-2)'"
               [style.--bg-img]="'url(' + biome(selectedNum()).bg + ')'">
        <div class="biome-bg" aria-hidden="true"></div>
        <div class="wash" aria-hidden="true"></div>

        <button class="back" (click)="back()">‹ Modos de jogo</button>

        @if (m.id === 'dungeon') {
          <div class="layout">
            <aside class="rail" aria-label="Tiers da caçada">
              @for (t of tiers(); track t.tier) {
                <button class="rail-item tier"
                        [class.active]="selectedNum() === t.tier"
                        [class.locked]="locked(t.requiredAccountLevel)"
                        [style.--rail]="biome(t.tier).accent"
                        (click)="select(t.tier)">
                  <span class="tier-num">{{ roman(t.tier) }}</span>
                  <span class="tier-copy">
                    <b>{{ t.name }}</b>
                    <small>{{ biome(t.tier).label }} · ×{{ t.statMultiplier }}</small>
                  </span>
                  <span class="tier-state">
                    @if (clears(t.tier) > 0) { <i>✓{{ clears(t.tier) }}</i> }
                    @if (locked(t.requiredAccountLevel)) { <i>🔒</i> }
                  </span>
                </button>
              }
            </aside>

            @if (selectedTier(); as t) {
              <main class="stage" [attr.aria-label]="t.boss">
                <div class="stage-orbit" aria-hidden="true"></div>
                @if (bossMonster(); as boss) {
                  <app-outfit-preview
                    [lookType]="boss.outfit.lookType" [head]="boss.outfit.head" [body]="boss.outfit.body"
                    [legs]="boss.outfit.legs" [feet]="boss.outfit.feet" [addons]="boss.outfit.addons"
                    [size]="360" />
                } @else {
                  <span class="boss-glyph">☠</span>
                }
              </main>

              <aside class="intel">
                <div>
                  <span class="mode-eyebrow" [style.color]="biome(t.tier).accent">Tier {{ t.tier }} · {{ biome(t.tier).label }}</span>
                  <h1>{{ t.boss }}</h1>
                  <p class="tier-name">{{ t.name }}</p>
                  <p class="desc">{{ t.description }}</p>
                </div>

                <div class="facts">
                  <span><b>×{{ t.statMultiplier }}</b><small>Multiplicador</small></span>
                  <span><b>{{ t.commonMobs.length }}</b><small>Mobs</small></span>
                  <span><b>{{ t.eliteMobs.length }}</b><small>Elites</small></span>
                  <span><b>{{ clears(t.tier) }}</b><small>Limpezas</small></span>
                </div>

                <div class="mob-lines">
                  <span>Mobs comuns</span>
                  <p>{{ t.commonMobs.join(' · ') }}</p>
                  <span class="elite">Elites</span>
                  <p>{{ t.eliteMobs.join(' · ') }}</p>
                </div>

                <div class="rewards">
                  <span>Recompensas do boss</span>
                  <div class="reward-strip">
                    @for (loot of bossLoot(); track loot.itemId) {
                      <div class="reward-cell" [title]="loot.name + ' · ' + loot.chance + '%'">
                        <app-item-icon [itemId]="loot.itemId" [size]="34" />
                      </div>
                    } @empty {
                      <small class="muted">Loot nao catalogado.</small>
                    }
                  </div>
                </div>

                <div class="farm-plan" [class.multi]="runCount() > 1">
                  <div class="farm-head">
                    <span>Tentativas</span>
                    <b>{{ runCount() }}x</b>
                  </div>
                  <div class="farm-controls">
                    <button class="farm-step" (click)="adjustRunCount(-1)" [disabled]="runCount() <= farmMin()">-</button>
                    <input type="range"
                           [min]="farmMin()" [max]="farmMax()" [value]="runCount()"
                           (input)="setRunCount($any($event.target).value)" />
                    <button class="farm-step" (click)="adjustRunCount(1)" [disabled]="runCount() >= farmMax()">+</button>
                  </div>
                  <div class="farm-cost">
                    <span>{{ farmEnergyPerRun() }} energia por run</span>
                    <b>{{ plannedEnergy() }} / {{ farmEnergyCap() }}</b>
                  </div>
                </div>

                <div class="actions">
                  @if (locked(t.requiredAccountLevel)) {
                    <span class="lock-msg">Desbloqueia no nivel de conta {{ t.requiredAccountLevel }}</span>
                  } @else {
                    <button class="pill-btn" (click)="start(t.tier)">Escolher Kaeli</button>
                  }
                </div>
              </aside>
            }
          </div>
        } @else {
          <div class="soon">
            <span class="soon-icon">{{ m.icon }}</span>
            <p>Este modo ainda esta em desenvolvimento.</p>
          </div>
        }
      </section>
    } @else {
      <p class="muted">Modo desconhecido.</p>
    }
  `,
  styles: [`
    :host { display: block; }
    .page {
      position: relative;
      isolation: isolate;
      overflow: hidden;
      min-height: calc(100dvh - 53px);
      padding: clamp(20px, 3vw, 34px) clamp(18px, 4vw, 64px) clamp(28px, 5vw, 56px);
      background: var(--bg-0);
    }
    .biome-bg {
      position: absolute;
      inset: 0;
      z-index: -4;
      background-image: var(--bg-img);
      background-size: cover;
      background-position: center;
      filter: saturate(0.96) contrast(1.04);
      transform: scale(1.02);
    }
    .wash {
      position: absolute;
      inset: 0;
      z-index: -3;
      pointer-events: none;
      background:
        radial-gradient(38% 54% at 48% 62%, color-mix(in srgb, var(--bc) 24%, transparent), transparent 70%),
        linear-gradient(90deg, rgba(7,7,13,0.78) 0%, rgba(7,7,13,0.18) 40%, rgba(7,7,13,0.78) 100%),
        linear-gradient(180deg, rgba(7,7,13,0.54) 0%, rgba(7,7,13,0.18) 45%, rgba(7,7,13,0.9) 100%);
    }
    .back {
      position: relative;
      z-index: 2;
      background: none;
      border: none;
      color: var(--text-dim);
      font-size: var(--fs-sm);
      padding: 0 0 var(--sp-4);
    }
    .back:hover { color: var(--accent-bright); }
    .back:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; border-radius: var(--r-sm); }

    .layout {
      min-height: calc(100dvh - 125px);
      display: grid;
      grid-template-columns: minmax(210px, 260px) minmax(280px, 1fr) minmax(320px, 440px);
      gap: clamp(22px, 3.6vw, 58px);
      align-items: center;
    }
    .rail { display: flex; flex-direction: column; gap: 12px; }
    .tier.locked { opacity: 0.48; }
    .tier-num {
      width: 50px;
      height: 50px;
      display: grid;
      place-items: center;
      border-radius: 50%;
      border: 1px solid color-mix(in srgb, var(--rail) 58%, var(--line-strong));
      color: color-mix(in srgb, var(--rail) 72%, white);
      font-family: var(--font-display);
      font-size: 1.35rem;
      font-weight: 650;
      line-height: 1;
    }
    .tier-copy { display: flex; flex-direction: column; min-width: 0; gap: 2px; }
    .tier-copy b {
      overflow: hidden;
      color: var(--text);
      font-family: var(--font-display);
      font-size: 1.05rem;
      font-weight: 620;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .tier-copy small { color: var(--text-mute); font-size: 11px; }
    .tier-state { display: flex; gap: 4px; color: var(--rail); font-size: 11px; font-style: normal; font-weight: 800; }
    .tier-state i { font-style: normal; }

    .stage {
      position: relative;
      min-height: min(58vh, 520px);
      display: grid;
      place-items: end center;
      padding-bottom: clamp(12px, 5vh, 60px);
    }
    .stage::after {
      content: '';
      position: absolute;
      left: 50%;
      bottom: 5%;
      width: min(420px, 78%);
      height: 70px;
      transform: translateX(-50%);
      border-radius: 50%;
      background: radial-gradient(ellipse at center, color-mix(in srgb, var(--bc) 48%, transparent), transparent 70%);
      filter: blur(16px);
      opacity: 0.8;
    }
    .stage-orbit {
      position: absolute;
      inset: 18% 14% 12%;
      border: 1px solid color-mix(in srgb, var(--bc) 30%, transparent);
      border-radius: 50%;
      opacity: 0.22;
      transform: rotate(-8deg);
    }
    .stage app-outfit-preview {
      position: relative;
      z-index: 1;
      image-rendering: pixelated;
      filter: drop-shadow(0 24px 32px rgba(0,0,0,0.76));
    }
    .boss-glyph {
      position: relative;
      z-index: 1;
      color: var(--bc);
      font-size: clamp(7rem, 18vw, 15rem);
      opacity: 0.46;
      filter: drop-shadow(0 0 28px color-mix(in srgb, var(--bc) 45%, transparent));
    }

    .intel {
      display: flex;
      flex-direction: column;
      gap: var(--sp-5);
      align-self: stretch;
      justify-content: center;
      min-width: 0;
    }
    .mode-eyebrow {
      font-size: var(--fs-xs);
      font-weight: 800;
      letter-spacing: var(--tracking-eyebrow);
      text-transform: uppercase;
    }
    .intel h1 {
      margin: var(--sp-2) 0 var(--sp-1);
      color: var(--text);
      font-family: var(--font-display);
      font-size: clamp(2.6rem, 5.2vw, 4.8rem);
      font-weight: 650;
      line-height: 0.95;
      text-shadow: 0 8px 36px rgba(0,0,0,0.72);
    }
    .tier-name { margin: 0; color: color-mix(in srgb, var(--bc) 72%, white); font-weight: 800; }
    .desc { max-width: 52ch; margin: var(--sp-3) 0 0; color: var(--text-dim); line-height: var(--lh-body); }
    .facts {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 1px;
      border-top: 1px solid var(--line);
      border-bottom: 1px solid var(--line);
      padding: var(--sp-3) 0;
    }
    .facts span { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .facts b { color: var(--text); font-family: var(--font-display); font-size: 1.4rem; font-weight: 650; line-height: 1; }
    .facts small { color: var(--text-mute); font-size: 10px; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
    .mob-lines { display: flex; flex-direction: column; gap: 5px; }
    .mob-lines span,
    .rewards > span {
      color: var(--text-mute);
      font-size: 10px;
      font-weight: 800;
      letter-spacing: 0.12em;
      text-transform: uppercase;
    }
    .mob-lines span.elite { color: var(--rarity-4); margin-top: 4px; }
    .mob-lines p { margin: 0; color: var(--text-dim); font-size: 0.92rem; line-height: 1.55; }
    .rewards { display: flex; flex-direction: column; gap: 10px; }
    .farm-plan {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 12px 14px;
      border: 1px solid color-mix(in srgb, var(--bc) 26%, var(--line));
      border-radius: var(--r-sm);
      background: rgba(12, 12, 20, 0.48);
      box-shadow: var(--glass-edge);
    }
    .farm-plan.multi {
      border-color: color-mix(in srgb, var(--bc) 58%, white 8%);
      background: color-mix(in srgb, var(--bc) 11%, rgba(12,12,20,0.58));
    }
    .farm-head,
    .farm-cost,
    .farm-controls {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .farm-head { justify-content: space-between; }
    .farm-head span,
    .farm-cost span {
      color: var(--text-mute);
      font-size: 10px;
      font-weight: 800;
      letter-spacing: 0.12em;
      text-transform: uppercase;
    }
    .farm-head b {
      color: var(--text);
      font-family: var(--font-display);
      font-size: 1.35rem;
      font-weight: 650;
      line-height: 1;
    }
    .farm-controls input {
      flex: 1;
      min-width: 0;
      accent-color: var(--bc);
    }
    .farm-step {
      width: 34px;
      height: 28px;
      border: 1px solid color-mix(in srgb, var(--bc) 42%, var(--line-strong));
      border-radius: var(--r-full);
      background: rgba(255,255,255,0.08);
      color: var(--text);
      font-size: 1rem;
      font-weight: 900;
    }
    .farm-step:disabled { opacity: 0.4; }
    .farm-cost { justify-content: space-between; }
    .farm-cost b { color: color-mix(in srgb, var(--bc) 76%, white); font-size: 0.9rem; }
    .actions {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: var(--sp-3);
      margin-top: auto;
      padding-top: var(--sp-2);
    }
    .lock-msg { color: var(--text-dim); font-weight: 700; }
    .soon { text-align: center; padding: var(--sp-7) var(--sp-5); }
    .soon-icon { font-size: 48px; }
    .soon p { color: var(--text-dim); margin: var(--sp-3) 0 0; }
    .muted { color: var(--text-mute); }

    @media (max-width: 1100px) {
      .layout { grid-template-columns: minmax(180px, 240px) minmax(0, 1fr); align-items: start; }
      .stage { order: 3; grid-column: 1 / -1; min-height: 300px; }
      .intel { align-self: start; }
    }
    @media (max-width: 760px) {
      .page { padding: var(--sp-4); }
      .layout { grid-template-columns: 1fr; min-height: auto; }
      .rail { order: 2; }
      .stage { order: 1; grid-column: auto; min-height: 260px; padding-bottom: var(--sp-3); }
      .stage app-outfit-preview { transform: scale(0.78); }
      .intel { order: 3; }
      .facts { grid-template-columns: repeat(2, minmax(0, 1fr)); row-gap: var(--sp-3); }
      .actions { justify-content: stretch; }
      .pill-btn { flex: 1 1 180px; }
    }
  `],
})
export class ModeSelectPage {
  readonly modeId = signal('dungeon');
  readonly mode = computed(() => GAME_MODES.find((m) => m.id === this.modeId()) ?? null);

  readonly tiers = computed(() => this.api.catalog()?.tiers ?? []);
  readonly selectedNum = signal(1);
  readonly selectedTier = computed(() => {
    const tiers = this.tiers();
    return tiers.find((t) => t.tier === this.selectedNum()) ?? tiers[0] ?? null;
  });

  /** Boss do tier selecionado: sprite real do Tibia, casado pelo nome no catalogo de monstros. */
  readonly bossMonster = computed<MonsterCatalogEntry | null>(() => {
    const t = this.selectedTier();
    if (!t) return null;
    return this.api.catalog()?.monsters.find((m) => m.name === t.boss) ?? null;
  });
  readonly bossLoot = computed(() => this.bossMonster()?.loot.slice(0, 6) ?? []);
  readonly runCount = signal(readFarmRunCount());

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {
    this.modeId.set(this.route.snapshot.paramMap.get('modeId') ?? 'dungeon');
  }

  biome(tier: number) { return tierBiome(tier); }
  locked(required: number): boolean { return (this.api.account()?.accountLevel ?? 1) < required; }
  clears(tier: number): number { return this.api.account()?.tierClears?.[String(tier)] ?? 0; }
  roman(tier: number): string { return ['I', 'II', 'III', 'IV', 'V'][tier - 1] ?? String(tier); }
  select(tier: number): void { this.selectedNum.set(tier); }
  farmMin(): number { return this.api.catalog()?.farm.minRuns ?? 1; }
  farmMax(): number { return this.api.catalog()?.farm.maxRuns ?? 5; }
  farmEnergyPerRun(): number { return this.api.catalog()?.farm.energyPerRun ?? 60; }
  farmEnergyCap(): number { return this.api.catalog()?.farm.energyCap ?? 300; }
  plannedEnergy(): number { return this.runCount() * this.farmEnergyPerRun(); }
  setRunCount(value: number | string): void {
    const count = normalizeFarmRunCount(Number(value), this.farmMin(), this.farmMax());
    this.runCount.set(count);
    writeFarmRunCount(count, this.farmMin(), this.farmMax());
  }
  adjustRunCount(delta: number): void { this.setRunCount(this.runCount() + delta); }
  start(tier: number): void { void this.router.navigate(['/play', tier], { queryParams: { runs: this.runCount() } }); }
  back(): void { void this.router.navigate(['/hunt']); }
}
