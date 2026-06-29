import { Component, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { ItemIcon } from '../../core/item-icon';
import { isGearMaterial } from '../../core/types';

@Component({
  selector: 'app-backpack',
  standalone: true,
  imports: [ItemIcon],
  template: `
    <div class="page">
      <h1>Backpack</h1>
      <p class="sub">Loot collected in dungeons. Sell items for gold.</p>
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
                  <span class="sale-tag">Sell value loot</span>
                }
              }
            </div>
            <div class="actions">
              <button class="btn secondary" [disabled]="busy()" (click)="sell(item.itemId, 1)">
                Sell 1 (+{{ salePrice(item.itemId) }} 🪙)
              </button>
              @if (item.count > 1) {
                <button class="btn secondary" [disabled]="busy()" (click)="sell(item.itemId, item.count)">
                  All (+{{ salePrice(item.itemId) * item.count }} 🪙)
                </button>
              }
            </div>
          </div>
        } @empty {
          <p class="muted">Empty backpack - go hunt! Tibia monsters drop their classic loot.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: var(--sp-6) var(--sp-5); }
    h1 { margin-bottom: var(--sp-2); }
    .sub { color: var(--text-dim); margin: 0; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: var(--sp-3); margin: var(--sp-5) 0 var(--sp-6); }
    .item {
      display: flex; align-items: center; gap: var(--sp-3); padding: var(--sp-3) var(--sp-4);
      background: var(--glass-bg);
      -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      border-color: var(--line-strong);
      box-shadow: var(--glass-edge), var(--sh-1);
    }
    .meta { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .meta b { color: var(--text); }
    .count { color: var(--text-dim); font-size: 13px; }
    .equip-tag { color: var(--accent-bright); font-size: 11px; font-weight: 700; }
    .sale-tag { color: var(--text-mute); font-size: 10px; text-transform: uppercase; letter-spacing: 0.08em; }
    .actions { display: flex; flex-direction: column; gap: 6px; min-width: 100px; }
    .actions .btn { padding: 5px 10px; font-size: 12px; white-space: normal; }
    .muted { color: var(--text-mute); }

    @media (max-width: 680px) {
      .page { padding: var(--sp-5) var(--sp-4); }
      .item { align-items: flex-start; }
      .actions { width: 100%; }
      .actions .btn { width: 100%; }
    }
  `],
})
export class BackpackPage {
  // G-09: Echo material lives on the Kaeli equipment screen, not in the sell backpack.
  readonly inventory = computed(() =>
    (this.api.account()?.inventory ?? []).filter((item) => !isGearMaterial(item.itemId)));
  readonly busy = signal(false);
  readonly salePrices = computed(() =>
    new Map((this.api.catalog()?.items ?? []).map((item) => [item.itemId, item.salePrice])),
  );

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
      helmet: 'Helmet', armor: 'Armor', weapon: 'Weapon',
      necklace: 'Necklace', ring: 'Ring', mount: 'Mount',
    }[slot] ?? slot;
  }

  itemStats(item: { attack: number; armor: number; defense: number; mountSpeed: number }): string {
    return [
      item.attack ? `ATK ${item.attack}` : '',
      item.armor ? `ARM ${item.armor}` : '',
      item.defense ? `DEF ${item.defense}` : '',
      item.mountSpeed ? `SPD ${item.mountSpeed}` : '',
    ].filter(Boolean).join(' · ') || 'equippable';
  }

  async sell(itemId: number, count: number): Promise<void> {
    this.busy.set(true);
    try { await this.api.sellItem(itemId, count); } catch (e) { alert((e as Error).message); } finally { this.busy.set(false); }
  }
}
