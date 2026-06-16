import { Component, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-hunt',
  standalone: true,
  template: `
    <div class="page">
      <h1>Caçada</h1>
      <p class="sub">Dungeons geradas proceduralmente — salas de mobs, baús e um boss no fundo.</p>
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
                @if (clears(t.tier) > 0) {
                  <span class="clears-badge">✓{{ clears(t.tier) }}</span>
                }
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
                <div class="stat">
                  <span class="stat-lbl">Multiplicador</span>
                  <span class="stat-val">×{{ t.statMultiplier }}</span>
                </div>
                <div class="stat">
                  <span class="stat-lbl">Limpezas</span>
                  <span class="stat-val clears-val">{{ clears(t.tier) }}</span>
                </div>
                <div class="stat">
                  <span class="stat-lbl">Mobs comuns</span>
                  <span class="stat-val">{{ t.commonMobs.length }}</span>
                </div>
                <div class="stat">
                  <span class="stat-lbl">Elites</span>
                  <span class="stat-val">{{ t.eliteMobs.length }}</span>
                </div>
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
                <button class="btn enter-btn" (click)="start(t.tier)">
                  Entrar — ×{{ t.statMultiplier }}
                </button>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: 24px; }
    .sub { color: #9c9ab0; margin-bottom: 24px; }

    .layout { display: grid; grid-template-columns: 280px 1fr; gap: 16px; align-items: start; }

    /* ---- Tier list ---- */
    .tier-list { display: flex; flex-direction: column; gap: 8px; }
    .tier-row {
      display: flex; align-items: center; justify-content: space-between;
      padding: 12px 14px; border-radius: 10px;
      background: #13131e; border: 1px solid #26263a;
      cursor: pointer; transition: background 0.15s, border-color 0.15s;
    }
    .tier-row:hover:not(.locked) { background: #1d1d2c; border-color: #3a3a5a; }
    .tier-row.active { background: #16242a; border-color: #2dd4bf; }
    .tier-row.locked { opacity: 0.5; cursor: default; }

    .row-left { display: flex; align-items: center; gap: 12px; }
    .badge {
      background: #e8a93c; color: #0c0c14;
      font-weight: 800; font-size: 11px; padding: 3px 8px; border-radius: 5px;
      min-width: 30px; text-align: center; flex-shrink: 0;
    }
    .tier-row.active .badge { background: #2dd4bf; }
    .row-info { display: flex; flex-direction: column; }
    .row-name { font-weight: 700; font-size: 14px; color: #e8e8f8; }
    .row-meta { font-size: 12px; color: #707088; }

    .row-right { display: flex; align-items: center; gap: 6px; }
    .clears-badge { color: #2dd4bf; font-size: 11px; font-weight: 700; }
    .lock-icon { font-size: 13px; }
    .arrow { color: #707088; font-size: 20px; font-weight: 700; line-height: 1; }
    .arrow.active { color: #2dd4bf; }

    /* ---- Preview panel ---- */
    .preview { overflow: hidden; padding: 0; }

    .boss-banner {
      padding: 28px 24px 22px;
      background: linear-gradient(135deg, #13131e 0%, #1a1a2e 100%);
      border-bottom: 1px solid #26263a;
    }
    .boss-banner[data-tier="1"] { background: linear-gradient(135deg, #121e14 0%, #162014 100%); }
    .boss-banner[data-tier="2"] { background: linear-gradient(135deg, #1e1410 0%, #2a1c10 100%); }
    .boss-banner[data-tier="3"] { background: linear-gradient(135deg, #12101e 0%, #1e1030 100%); }
    .boss-banner[data-tier="4"] { background: linear-gradient(135deg, #1e1010 0%, #2a1212 100%); }
    .boss-banner[data-tier="5"] { background: linear-gradient(135deg, #080610 0%, #14081e 100%); }

    .boss-tier-label { color: #e8a93c; font-size: 11px; font-weight: 800; letter-spacing: 2px; text-transform: uppercase; margin-bottom: 10px; }
    .boss-name { font-size: 32px; font-weight: 800; color: #fff; margin-bottom: 4px; line-height: 1.1; }
    .boss-sub { color: #9c9ab0; font-size: 13px; }

    .preview-body { padding: 22px 24px; display: flex; flex-direction: column; gap: 16px; }
    h2 { margin: 0; font-size: 18px; color: #e8e8f8; }
    .preview-desc { color: #9c9ab0; margin: 0; font-size: 14px; }

    .stats-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
    .stat { display: flex; flex-direction: column; gap: 4px; }
    .stat-lbl { color: #707088; font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px; }
    .stat-val { color: #e8e8f8; font-size: 18px; font-weight: 700; }
    .clears-val { color: #2dd4bf; }

    .mob-block { }
    .mob-lbl { font-size: 10px; font-weight: 700; color: #707088; text-transform: uppercase; letter-spacing: 1px; }
    .mob-lbl.elite { color: #9d60d4; }
    .mob-list { margin: 4px 0 0; color: #b8b6c8; font-size: 13px; }
    .mob-list.elite { color: #c084fc; }

    .lock-msg { color: #707088; font-weight: 600; font-size: 14px; padding: 8px 0; }
    .enter-btn { width: 100%; padding: 14px; font-size: 16px; }
  `],
})
export class HuntPage {
  readonly tiers = computed(() => this.api.catalog()?.tiers ?? []);
  readonly selectedNum = signal(1);
  readonly selectedTier = computed(() => {
    const tiers = this.tiers();
    return tiers.find((t) => t.tier === this.selectedNum()) ?? tiers[0] ?? null;
  });

  constructor(
    private readonly api: ApiService,
    private readonly router: Router,
  ) {}

  locked(required: number): boolean {
    return (this.api.account()?.accountLevel ?? 1) < required;
  }

  clears(tier: number): number {
    return this.api.account()?.tierClears?.[String(tier)] ?? 0;
  }

  select(tier: number): void {
    this.selectedNum.set(tier);
  }

  start(tier: number): void {
    void this.router.navigate(['/game', tier]);
  }
}
