import { Component, computed } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-hunt',
  standalone: true,
  template: `
    <div class="page">
      <h1>Caçada</h1>
      <p class="sub">Dungeons geradas proceduralmente — salas de mobs, baús e um boss no fundo. Desça a escada para alcançar o covil.</p>
      <div class="tiers">
        @for (t of tiers(); track t.tier) {
          <div class="tier panel" [class.locked]="locked(t.requiredAccountLevel)">
            <div class="head">
              <span class="num">Tier {{ t.tier }}</span>
              <h2>{{ t.name }}</h2>
            </div>
            <p class="desc">{{ t.description }}</p>
            <div class="mobs">
              <span class="label">Mobs:</span> {{ t.commonMobs.join(', ') }}
            </div>
            <div class="mobs elite">
              <span class="label">Elites:</span> {{ t.eliteMobs.join(', ') }}
            </div>
            <div class="boss">👑 Boss: <b>{{ t.boss }}</b></div>
            @if (locked(t.requiredAccountLevel)) {
              <div class="lock">🔒 Requer conta Lv. {{ t.requiredAccountLevel }}</div>
            } @else {
              <button class="btn" (click)="start(t.tier)">Entrar — x{{ t.statMultiplier }}</button>
            }
            @if (clears(t.tier) > 0) {
              <span class="clears">✓ {{ clears(t.tier) }} limpeza(s)</span>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1200px; margin: 0 auto; padding: 24px; }
    .sub { color: #9c9ab0; margin-bottom: 24px; }
    .tiers { display: grid; grid-template-columns: repeat(auto-fill, minmax(330px, 1fr)); gap: 16px; }
    .tier { display: flex; flex-direction: column; gap: 8px; position: relative; }
    .tier.locked { opacity: 0.55; }
    .head { display: flex; align-items: baseline; gap: 10px; }
    .num { color: #e8a93c; font-weight: 800; font-size: 13px; }
    h2 { margin: 0; font-size: 20px; }
    .desc { color: #9c9ab0; font-size: 13px; margin: 0; min-height: 36px; }
    .mobs { font-size: 12px; color: #b8b6c8; }
    .mobs.elite { color: #c084fc; }
    .label { color: #707088; font-weight: 700; }
    .boss { font-size: 14px; color: #ff8c4d; }
    .lock { color: #707088; font-weight: 600; padding: 10px 0; }
    .clears { position: absolute; top: 12px; right: 14px; color: #2dd4bf; font-size: 12px; font-weight: 700; }
    .btn { margin-top: 6px; }
  `],
})
export class HuntPage {
  readonly tiers = computed(() => this.api.catalog()?.tiers ?? []);

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

  start(tier: number): void {
    void this.router.navigate(['/game', tier]);
  }
}
