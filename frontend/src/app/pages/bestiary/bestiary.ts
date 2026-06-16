import { Component, computed } from '@angular/core';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-bestiary',
  standalone: true,
  template: `
    <div class="page">
      <h1>Bestiário</h1>
      <p class="sub">Cada rank (10/50/100/250 abates) dá +1% de dano permanente contra a espécie.</p>
      <div class="grid">
        @for (b of bestiary(); track b.name) {
          <div class="entry panel">
            <b>{{ b.name }}</b>
            <span class="kills">{{ b.kills }} abates</span>
            <span class="rank">Rank {{ b.rank }}</span>
            <div class="bar"><div class="fill" [style.width.%]="b.pct"></div></div>
          </div>
        } @empty {
          <p class="muted">Nenhum abate registrado ainda — vá caçar!</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: 24px; }
    .sub { color: #9c9ab0; margin-bottom: 24px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 12px; }
    .entry { padding: 12px 14px; display: flex; flex-direction: column; gap: 4px; }
    .kills { color: #9c9ab0; font-size: 13px; }
    .rank { color: #e8a93c; font-weight: 700; font-size: 13px; }
    .bar { height: 5px; background: #23232f; border-radius: 3px; overflow: hidden; }
    .fill { height: 100%; background: linear-gradient(90deg, #e8a93c, #d97706); }
    .muted { color: #707088; }
  `],
})
export class BestiaryPage {
  readonly bestiary = computed(() => {
    const kills = this.api.account()?.bestiaryKills ?? {};
    const ranks = this.api.catalog()?.bestiaryRanks ?? [10, 50, 100, 250];
    return Object.entries(kills)
      .map(([name, k]) => {
        const rank = ranks.filter((r) => k >= r).length;
        const next = ranks[rank] ?? ranks[ranks.length - 1];
        return { name, kills: k, rank, pct: Math.min((100 * k) / next, 100) };
      })
      .sort((a, b) => b.kills - a.kills);
  });

  constructor(private readonly api: ApiService) {}
}
