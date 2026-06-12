import { Component, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { ItemIcon } from '../../core/item-icon';

@Component({
  selector: 'app-backpack',
  standalone: true,
  imports: [ItemIcon],
  template: `
    <div class="page">
      <h1>Mochila</h1>
      <p class="sub">Saque coletado nas dungeons. Venda itens por ouro.</p>
      <div class="grid">
        @for (item of inventory(); track item.itemId) {
          <div class="item panel">
            <app-item-icon [itemId]="item.itemId" [size]="56" />
            <div class="meta">
              <b>{{ item.name }}</b>
              <span class="count">×{{ item.count }}</span>
              @if (catalogItem(item.itemId); as def) {
                @if (def.slot) {
                  <span class="equip-tag">{{ slotLabel(def.slot) }} · {{ itemStats(def) }}</span>
                } @else {
                  <span class="sale-tag">Loot de venda</span>
                }
              }
            </div>
            <div class="actions">
              <button class="btn secondary" [disabled]="busy()" (click)="sell(item.itemId, 1)">
                Vender 1 (+{{ salePrice(item.itemId) }} 🪙)
              </button>
              @if (item.count > 1) {
                <button class="btn secondary" [disabled]="busy()" (click)="sell(item.itemId, item.count)">
                  Tudo (+{{ salePrice(item.itemId) * item.count }} 🪙)
                </button>
              }
            </div>
          </div>
        } @empty {
          <p class="muted">Mochila vazia — vá caçar! Os monstros de Tibia dropam o loot clássico deles.</p>
        }
      </div>
      <h2>Bestiário</h2>
      <p class="sub">Cada rank (10/50/100/250 abates) dá +1% de dano permanente contra a espécie.</p>
      <div class="bestiary">
        @for (b of bestiary(); track b.name) {
          <div class="best panel">
            <b>{{ b.name }}</b>
            <span class="kills">{{ b.kills }} abates</span>
            <span class="rank">Rank {{ b.rank }}</span>
            <div class="bar"><div class="fill" [style.width.%]="b.pct"></div></div>
          </div>
        } @empty {
          <p class="muted">Nenhum abate registrado ainda.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: 24px; }
    .sub { color: #9c9ab0; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 12px; margin: 16px 0 30px; }
    .item { display: flex; align-items: center; gap: 12px; padding: 10px 14px; }
    .meta { flex: 1; display: flex; flex-direction: column; }
    .count { color: #9c9ab0; font-size: 13px; }
    .equip-tag { color: #2dd4bf; font-size: 11px; font-weight: 700; }
    .sale-tag { color: #707088; font-size: 10px; }
    .actions { display: flex; flex-direction: column; gap: 6px; }
    .actions .btn { padding: 5px 10px; font-size: 12px; }
    .bestiary { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 12px; margin-top: 14px; }
    .best { padding: 12px 14px; display: flex; flex-direction: column; gap: 4px; }
    .kills { color: #9c9ab0; font-size: 13px; }
    .rank { color: #e8a93c; font-weight: 700; font-size: 13px; }
    .bar { height: 5px; background: #23232f; border-radius: 3px; overflow: hidden; }
    .fill { height: 100%; background: linear-gradient(90deg, #e8a93c, #d97706); }
    .muted { color: #707088; }
  `],
})
export class BackpackPage {
  readonly inventory = computed(() => this.api.account()?.inventory ?? []);
  readonly busy = signal(false);
  readonly salePrices = computed(() =>
    new Map((this.api.catalog()?.items ?? []).map((item) => [item.itemId, item.salePrice])),
  );

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

  salePrice(itemId: number): number {
    return this.salePrices().get(itemId)
      ?? this.api.catalog()?.itemFallbackSalePrice
      ?? 0;
  }

  catalogItem(itemId: number) {
    return this.api.catalog()?.items.find((item) => item.itemId === itemId);
  }

  slotLabel(slot: string): string {
    return {
      helmet: 'Capacete', armor: 'Armadura', weapon: 'Arma',
      necklace: 'Colar', ring: 'Anel', mount: 'Montaria',
    }[slot] ?? slot;
  }

  itemStats(item: { attack: number; armor: number; defense: number; mountSpeed: number }): string {
    return [
      item.attack ? `ATK ${item.attack}` : '',
      item.armor ? `ARM ${item.armor}` : '',
      item.defense ? `DEF ${item.defense}` : '',
      item.mountSpeed ? `VEL ${item.mountSpeed}` : '',
    ].filter(Boolean).join(' · ') || 'equipável';
  }

  async sell(itemId: number, count: number): Promise<void> {
    this.busy.set(true);
    try { await this.api.sellItem(itemId, count); } catch (e) { alert((e as Error).message); } finally { this.busy.set(false); }
  }
}
