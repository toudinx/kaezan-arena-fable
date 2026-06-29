import { Component, computed } from '@angular/core';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-bestiary',
  standalone: true,
  template: `
    <div class="page">
      <h1>Bestiary</h1>
      <p class="sub">Each rank (10/50/100/250 kills) grants +1% permanent damage against that species.</p>
      <div class="grid">
        @for (b of bestiary(); track b.name) {
          <div class="entry panel">
            <b>{{ b.name }}</b>
            <span class="kills">{{ b.kills }} kills</span>
            <span class="rank">Rank {{ b.rank }}</span>
            <div class="bar"><div class="fill" [style.width.%]="b.pct"></div></div>
          </div>
        } @empty {
          <p class="muted">No kills recorded yet - go hunt!</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: var(--sp-6) var(--sp-5); }
    h1 { margin-bottom: var(--sp-2); }
    .sub { color: var(--text-dim); margin: 0 0 var(--sp-5); max-width: 720px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: var(--sp-3); }
    .entry {
      padding: var(--sp-4); display: flex; flex-direction: column; gap: var(--sp-1);
      background: var(--glass-bg);
      -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      border-color: var(--line-strong);
      box-shadow: var(--glass-edge), var(--sh-1);
    }
    .entry b { color: var(--text); }
    .kills { color: var(--text-dim); font-size: 13px; }
    .rank { color: var(--gold-bright); font-weight: 700; font-size: 13px; }
    .bar { height: 6px; background: var(--bg-3); border-radius: var(--r-full); overflow: hidden; border: 1px solid var(--line); }
    .fill { height: 100%; background: linear-gradient(90deg, var(--gold), var(--gold-bright)); box-shadow: 0 0 16px var(--gold-glow); }
    .muted { color: var(--text-mute); }

    @media (max-width: 680px) {
      .page { padding: var(--sp-5) var(--sp-4); }
    }
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
