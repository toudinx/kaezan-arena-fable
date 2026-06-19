import { Component, computed, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { GAME_MODES } from '../../core/game-modes';

/** Tela de seleção interna de um modo. Por enquanto só "dungeon" (lista de 5 tiers). */
@Component({
  selector: 'app-mode',
  standalone: true,
  template: `
    <div class="page">
      <button class="back" (click)="back()">‹ Modos de jogo</button>

      @if (mode(); as m) {
        <div class="head">
          <span class="mode-eyebrow" [style.color]="m.theme">{{ m.icon }} {{ m.tagline }}</span>
          <h1>{{ m.name }}</h1>
        </div>

        @if (m.id === 'dungeon') {
          <div class="layout">
            <div class="tier-list">
              @for (t of tiers(); track t.tier) {
                <div class="tier-row"
                     [class.active]="selectedNum() === t.tier"
                     [class.locked]="locked(t.requiredAccountLevel)"
                     (click)="select(t.tier)">
                  <div class="row-left">
                    <span class="badge">T{{ t.tier }}</span>
                    <div class="row-info">
                      <span class="row-name">{{ t.name }}</span>
                      <span class="row-meta">×{{ t.statMultiplier }}</span>
                    </div>
                  </div>
                  <div class="row-right">
                    @if (clears(t.tier) > 0) { <span class="clears-badge">✓{{ clears(t.tier) }}</span> }
                    @if (locked(t.requiredAccountLevel)) {
                      <span class="lock-icon">🔒</span>
                    } @else {
                      <span class="arrow" [class.active]="selectedNum() === t.tier">›</span>
                    }
                  </div>
                </div>
              }
            </div>

            @if (selectedTier(); as t) {
              <div class="preview panel">
                <div class="boss-banner" [attr.data-tier]="t.tier">
                  <div class="boss-tier-label">Tier {{ t.tier }}</div>
                  <div class="boss-name">{{ t.boss }}</div>
                  <div class="boss-sub">Boss Final · ×{{ t.statMultiplier }}</div>
                </div>
                <div class="preview-body">
                  <h2>{{ t.name }}</h2>
                  <p class="preview-desc">{{ t.description }}</p>

                  <div class="stats-row">
                    <div class="stat"><span class="stat-lbl">Multiplicador</span><span class="stat-val">×{{ t.statMultiplier }}</span></div>
                    <div class="stat"><span class="stat-lbl">Limpezas</span><span class="stat-val clears-val">{{ clears(t.tier) }}</span></div>
                    <div class="stat"><span class="stat-lbl">Mobs comuns</span><span class="stat-val">{{ t.commonMobs.length }}</span></div>
                    <div class="stat"><span class="stat-lbl">Elites</span><span class="stat-val">{{ t.eliteMobs.length }}</span></div>
                  </div>

                  <div class="mob-block">
                    <span class="mob-lbl">Mobs</span>
                    <p class="mob-list">{{ t.commonMobs.join(', ') }}</p>
                  </div>
                  <div class="mob-block">
                    <span class="mob-lbl elite">Elites</span>
                    <p class="mob-list elite">{{ t.eliteMobs.join(', ') }}</p>
                  </div>

                  @if (locked(t.requiredAccountLevel)) {
                    <div class="lock-msg">🔒 Desbloqueado no nível de conta {{ t.requiredAccountLevel }}</div>
                  } @else {
                    <button class="btn enter-btn" (click)="start(t.tier)">Escolher Kaeli — ×{{ t.statMultiplier }}</button>
                  }
                </div>
              </div>
            }
          </div>
        } @else {
          <div class="soon panel">
            <span class="soon-icon">{{ m.icon }}</span>
            <p>Este modo ainda está em desenvolvimento.</p>
          </div>
        }
      } @else {
        <p class="muted">Modo desconhecido.</p>
      }
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: var(--sp-6) var(--sp-5); }
    .back { background: none; border: none; color: var(--text-dim); font-size: var(--fs-sm); cursor: pointer; padding: 0 0 var(--sp-4); }
    .back:hover { color: var(--accent-bright); }
    .back:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; border-radius: var(--r-sm); }
    .head { margin-bottom: var(--sp-5); }
    .mode-eyebrow { font-size: var(--fs-xs); font-weight: 800; letter-spacing: var(--tracking-eyebrow); text-transform: uppercase; }
    .head h1 { margin: var(--sp-2) 0 0; font-size: var(--fs-h1); }

    .layout { display: grid; grid-template-columns: 280px 1fr; gap: var(--sp-4); align-items: start; }
    .tier-list { display: flex; flex-direction: column; gap: var(--sp-2); }
    .tier-row {
      display: flex; align-items: center; justify-content: space-between;
      padding: var(--sp-3) var(--sp-4); border-radius: var(--r-md);
      background: var(--glass-bg); border: 1px solid var(--line);
      box-shadow: var(--glass-edge);
      cursor: pointer; transition: background var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out);
    }
    .tier-row:hover:not(.locked) { background: var(--bg-4); border-color: var(--accent); transform: translateX(2px); }
    .tier-row.active { background: color-mix(in srgb, var(--accent) 16%, var(--glass-bg-strong)); border-color: var(--accent-bright); }
    .tier-row.locked { opacity: 0.5; cursor: default; }
    .row-left { display: flex; align-items: center; gap: var(--sp-3); }
    .badge {
      background: linear-gradient(180deg, var(--gold-bright), var(--gold)); color: var(--bg-0); font-weight: 800; font-size: 11px;
      padding: 3px 8px; border-radius: var(--r-sm); min-width: 30px; text-align: center; flex-shrink: 0;
    }
    .tier-row.active .badge { background: linear-gradient(180deg, var(--accent-bright), var(--accent)); color: var(--bg-0); }
    .row-info { display: flex; flex-direction: column; }
    .row-name { font-weight: 700; font-size: 14px; color: var(--text); }
    .row-meta { font-size: 12px; color: var(--text-mute); }
    .row-right { display: flex; align-items: center; gap: 6px; }
    .clears-badge { color: var(--accent-bright); font-size: 11px; font-weight: 700; }
    .lock-icon { font-size: 13px; }
    .arrow { color: var(--text-mute); font-size: 20px; font-weight: 700; line-height: 1; }
    .arrow.active { color: var(--accent-bright); }

    .preview { overflow: hidden; padding: 0; }
    .boss-banner { padding: var(--sp-6) var(--sp-5) var(--sp-5); background: linear-gradient(135deg, var(--bg-2) 0%, var(--bg-3) 100%); border-bottom: 1px solid var(--line); box-shadow: var(--glass-edge); }
    .boss-banner[data-tier="1"] { background: linear-gradient(135deg, color-mix(in srgb, var(--el-earth) 22%, var(--bg-2)) 0%, var(--bg-1) 100%); }
    .boss-banner[data-tier="2"] { background: linear-gradient(135deg, color-mix(in srgb, var(--gold) 18%, var(--bg-2)) 0%, var(--bg-1) 100%); }
    .boss-banner[data-tier="3"] { background: linear-gradient(135deg, color-mix(in srgb, var(--el-death) 24%, var(--bg-2)) 0%, var(--bg-1) 100%); }
    .boss-banner[data-tier="4"] { background: linear-gradient(135deg, color-mix(in srgb, var(--el-fire) 22%, var(--bg-2)) 0%, var(--bg-1) 100%); }
    .boss-banner[data-tier="5"] { background: linear-gradient(135deg, color-mix(in srgb, var(--accent) 24%, var(--bg-2)) 0%, var(--bg-0) 100%); }
    .boss-tier-label { color: var(--gold-bright); font-size: 11px; font-weight: 800; letter-spacing: var(--tracking-eyebrow); text-transform: uppercase; margin-bottom: var(--sp-3); }
    .boss-name { font-family: var(--font-display); font-size: 34px; font-weight: 650; color: var(--text); margin-bottom: var(--sp-1); line-height: 1.1; }
    .boss-sub { color: var(--text-dim); font-size: var(--fs-sm); }
    .preview-body { padding: var(--sp-5); display: flex; flex-direction: column; gap: var(--sp-4); }
    h2 { margin: 0; font-size: var(--fs-h2); color: var(--text); }
    .preview-desc { color: var(--text-dim); margin: 0; font-size: var(--fs-sm); }
    .stats-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: var(--sp-3); }
    .stat { display: flex; flex-direction: column; gap: 4px; }
    .stat-lbl { color: var(--text-mute); font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.1em; }
    .stat-val { color: var(--text); font-size: 18px; font-weight: 700; }
    .clears-val { color: var(--accent-bright); }
    .mob-lbl { font-size: 10px; font-weight: 700; color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.1em; }
    .mob-lbl.elite { color: var(--rarity-4); }
    .mob-list { margin: 4px 0 0; color: var(--text-dim); font-size: 13px; }
    .mob-list.elite { color: var(--rarity-4); }
    .lock-msg { color: var(--text-mute); font-weight: 600; font-size: 14px; padding: var(--sp-2) 0; }
    .enter-btn { width: 100%; padding: 14px; font-size: 16px; }

    .soon { text-align: center; padding: var(--sp-7) var(--sp-5); display: flex; flex-direction: column; align-items: center; gap: var(--sp-3); }
    .soon-icon { font-size: 48px; }
    .soon p { color: var(--text-dim); margin: 0; }
    .muted { color: var(--text-mute); }

    @media (max-width: 820px) {
      .page { padding: var(--sp-5) var(--sp-4); }
      .layout { grid-template-columns: 1fr; }
      .stats-row { grid-template-columns: repeat(2, 1fr); }
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

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {
    this.modeId.set(this.route.snapshot.paramMap.get('modeId') ?? 'dungeon');
  }

  locked(required: number): boolean { return (this.api.account()?.accountLevel ?? 1) < required; }
  clears(tier: number): number { return this.api.account()?.tierClears?.[String(tier)] ?? 0; }
  select(tier: number): void { this.selectedNum.set(tier); }
  start(tier: number): void { void this.router.navigate(['/play', tier]); }
  back(): void { void this.router.navigate(['/hunt']); }
}
