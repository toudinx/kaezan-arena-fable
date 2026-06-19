import { Component, OnDestroy, computed, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ItemIcon } from '../../core/item-icon';
import { OutfitPreview } from '../../core/outfit-preview';
import {
  ClassDef, ELEMENT_LABELS, EquipmentSlot, ItemCatalogEntry, RARITY_COLORS,
  SkinDef, WEAPON_LABELS, WaifuDef, equipKey,
} from '../../core/types';

const EQUIPMENT_SLOTS: { id: EquipmentSlot; label: string }[] = [
  { id: 'helmet', label: 'Capacete' },
  { id: 'armor', label: 'Armadura' },
  { id: 'weapon', label: 'Arma' },
  { id: 'necklace', label: 'Colar' },
  { id: 'ring', label: 'Anel' },
  { id: 'mount', label: 'Montaria' },
];

/** Tela de pré-run (3 colunas): stats | set do tier (troca inline) | roster. */
@Component({
  selector: 'app-prerun',
  standalone: true,
  imports: [OutfitPreview, ItemIcon],
  template: `
    <div class="page">
      <button class="back" (click)="back()">‹ Voltar aos tiers</button>

      @if (tierDef(); as t) {
        <div class="head">
          <span class="tier-badge">Tier {{ t.tier }} · ×{{ t.statMultiplier }}</span>
          <h1>{{ t.name }}</h1>
          <p class="sub">Escolha a Kaeli que vai à caçada — boss: <b>{{ t.boss }}</b></p>
        </div>
      }

      @if (selected(); as w) {
        <div class="cols">
          <!-- ── ESQUERDA: portrait + stats ── -->
          <div class="col hero panel">
            <app-outfit-preview
              [lookType]="skinFor(w).lookType" [head]="skinFor(w).head" [body]="skinFor(w).body"
              [legs]="skinFor(w).legs" [feet]="skinFor(w).feet" [addons]="skinFor(w).addons ?? 0"
              [mountLookType]="mountLookType(w.id)" [size]="172" />
            <div class="stars" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</div>
            <h2>{{ w.name }}</h2>
            <p class="title">{{ w.title }}</p>
            <div class="tags">
              <span class="tag class-tag">{{ classFor(w)?.name }}</span>
              <span class="tag">{{ elementLabel(w.element) }}</span>
              <span class="tag">{{ weaponLabel(w.weapon) }}</span>
            </div>
            <div class="statgrid">
              <div class="sg"><span>ATK</span><b>{{ w.baseAtk }}@if (bonus('atk'); as v) {<i class="plus">{{ v }}</i>}</b></div>
              <div class="sg"><span>HP</span><b>{{ w.baseHp }}@if (bonus('hp'); as v) {<i class="plus">{{ v }}</i>}</b></div>
              <div class="sg"><span>Afinidade</span><b>{{ affinityLevel(w.id) }}</b></div>
              <div class="sg"><span>Ascensão</span><b>A{{ ascension(w.id) }}</b></div>
            </div>
            @if (equipmentTotals(w.id).length) {
              <div class="equip-line">
                <span class="el-lbl">Set T{{ tierNum() }}</span>
                @for (s of equipmentTotals(w.id); track s.label) {
                  <span class="el-stat">{{ s.label }} <b>{{ s.value }}</b></span>
                }
              </div>
            }
            <div class="trait">
              <span class="trait-lbl">{{ w.trait.name }}</span>
              <p>{{ w.trait.description }}</p>
            </div>
            <button class="btn enter" (click)="enter()">Entrar — {{ w.name }}</button>
          </div>

          <!-- ── MEIO: set do tier ── -->
          <div class="col set panel">
            <div class="set-head">
              <span class="set-title">Set · Tier {{ tierNum() }}</span>
              <span class="muted small">Itens travados no tier (0 = sem-tier)</span>
            </div>
            <div class="slots">
              @for (slot of equipmentSlots; track slot.id) {
                <button class="slot" [class.active]="selectedSlot() === slot.id"
                        [class.filled]="equippedItem(w.id, slot.id)"
                        (click)="toggleSlot(slot.id)">
                  <span class="slot-name">{{ slot.label }}</span>
                  @if (equippedItem(w.id, slot.id); as item) {
                    <app-item-icon [itemId]="item.itemId" [size]="40" />
                    <b>{{ item.name }}</b>
                    <small>{{ itemStats(item) }}</small>
                  } @else {
                    <span class="empty">vazio</span>
                  }
                </button>
              }
            </div>

            @if (selectedSlot(); as slot) {
              <div class="picker">
                <div class="picker-head">
                  <b>Trocar {{ slotLabel(slot) }}</b>
                  @if (equippedItem(w.id, slot)) {
                    <button class="btn secondary tiny" [disabled]="busy()" (click)="unequip(w.id, slot)">Desequipar</button>
                  }
                </div>
                <div class="options">
                  @for (item of candidates(w.id, slot); track item.itemId) {
                    <button class="option" [disabled]="busy() || !canEquip(w, item)"
                            [title]="itemRequirement(w, item)" (click)="equip(w.id, slot, item.itemId)">
                      <app-item-icon [itemId]="item.itemId" [size]="34" />
                      <span>
                        <b>{{ item.name }}</b>
                        <small>{{ itemStats(item) }}</small>
                        @if (item.tier > 0) { <small class="t-tag">T{{ item.tier }}</small> }
                        @if (itemRequirement(w, item)) { <small class="req">{{ itemRequirement(w, item) }}</small> }
                      </span>
                    </button>
                  } @empty {
                    <span class="muted small">Nenhum item deste slot para o tier {{ tierNum() }} na Mochila.</span>
                  }
                </div>
              </div>
            } @else {
              <p class="muted small pick-hint">Clique num slot para trocar a peça sem sair desta tela.</p>
            }
          </div>

          <!-- ── DIREITA: roster ── -->
          <div class="col roster-col">
            <span class="roster-title">Suas Kaelis</span>
            <div class="roster">
              @for (o of ownedWaifus(); track o.id) {
                <button class="card" [class.selected]="selected()?.id === o.id"
                        [style.--rc]="rarityColor(o.rarity)" (click)="select(o)">
                  <app-outfit-preview [lookType]="skinFor(o).lookType" [head]="skinFor(o).head"
                    [body]="skinFor(o).body" [legs]="skinFor(o).legs" [feet]="skinFor(o).feet"
                    [addons]="skinFor(o).addons ?? 0" [size]="56" [animate]="false" />
                  <span class="card-name">{{ o.name }}</span>
                  <span class="card-stars" [style.color]="rarityColor(o.rarity)">{{ '★'.repeat(o.rarity) }}</span>
                </button>
              } @empty {
                <p class="muted">Nenhuma Kaeli recrutada.</p>
              }
            </div>
          </div>
        </div>
      } @else {
        <p class="muted">Você ainda não recrutou nenhuma Kaeli. Vá ao banner!</p>
      }
    </div>
  `,
  styles: [`
    .page { max-width: 1220px; margin: 0 auto; padding: var(--sp-6) var(--sp-5); }
    .back { background: none; border: none; color: var(--text-dim); font-size: var(--fs-sm); cursor: pointer; padding: 0 0 var(--sp-4); }
    .back:hover { color: var(--accent-bright); }
    .back:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; border-radius: var(--r-sm); }
    .head { margin-bottom: var(--sp-5); }
    .tier-badge { color: var(--gold-bright); font-size: 11px; font-weight: 800; letter-spacing: var(--tracking-eyebrow); text-transform: uppercase; }
    .head h1 { margin: var(--sp-2) 0 var(--sp-1); font-size: var(--fs-h1); }
    .sub { color: var(--text-dim); margin: 0; }

    .cols { display: grid; grid-template-columns: 280px 1fr 300px; gap: var(--sp-4); align-items: start; }
    .col { }
    .panel {
      background: var(--glass-bg);
      -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      border: 1px solid var(--line-strong);
      border-radius: var(--r-lg);
      box-shadow: var(--glass-edge), var(--sh-2);
    }

    /* ESQUERDA */
    .hero { display: flex; flex-direction: column; align-items: center; gap: var(--sp-2); padding: var(--sp-5) var(--sp-4);
      background: linear-gradient(180deg, color-mix(in srgb, var(--accent) 10%, var(--glass-bg)), var(--glass-bg-strong)); }
    .hero h2 { margin: 2px 0 0; font-size: 21px; }
    .hero .title { margin: 0; color: var(--accent-bright); font-size: 12px; text-align: center; }
    .stars { letter-spacing: 2px; font-size: 14px; }
    .tags { display: flex; gap: 5px; flex-wrap: wrap; justify-content: center; }
    .tag { background: var(--bg-3); border: 1px solid var(--line); border-radius: var(--r-sm); padding: 3px 8px; font-size: 11px; font-weight: 700; }
    .class-tag { color: var(--accent-bright); border-color: color-mix(in srgb, var(--accent) 45%, transparent); }
    .statgrid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--sp-2); width: 100%; margin-top: 4px; }
    .sg { background: var(--bg-2); border: 1px solid var(--line); border-radius: var(--r-sm); padding: 7px 9px;
      display: flex; flex-direction: column; gap: 2px; align-items: center; }
    .sg span { font-size: 9px; color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.08em; }
    .sg b { font-size: 16px; }
    .plus { color: var(--accent-bright); font-size: 11px; font-style: normal; margin-left: 3px; }
    .equip-line { width: 100%; background: color-mix(in srgb, var(--accent) 13%, var(--bg-2)); border: 1px solid color-mix(in srgb, var(--accent) 35%, transparent); border-radius: var(--r-sm);
      padding: 7px 10px; display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .el-lbl { font-size: 9px; font-weight: 800; color: var(--accent-bright); text-transform: uppercase; }
    .el-stat { font-size: 11px; color: var(--text-dim); }
    .el-stat b { color: var(--accent-bright); }
    .trait { width: 100%; background: color-mix(in srgb, var(--rarity-4) 11%, var(--bg-2)); border: 1px solid color-mix(in srgb, var(--rarity-4) 35%, transparent); border-radius: var(--r-sm); padding: 8px 10px; }
    .trait-lbl { color: var(--accent-bright); font-size: 11px; font-weight: 800; }
    .trait p { margin: 3px 0 0; color: var(--text-dim); font-size: 11px; line-height: 1.4; }
    .enter { width: 100%; padding: 13px; font-size: 15px; margin-top: 2px; }

    /* MEIO */
    .set { padding: var(--sp-4); display: flex; flex-direction: column; gap: var(--sp-3); }
    .set-head { display: flex; flex-direction: column; gap: 2px; }
    .set-title { font-size: 13px; font-weight: 800; color: var(--accent-bright); text-transform: uppercase; letter-spacing: 0.08em; }
    .slots { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--sp-2); }
    .slot { min-height: 104px; border: 1px solid var(--line); border-radius: var(--r-md); background: var(--bg-2);
      color: inherit; padding: 8px; display: flex; flex-direction: column; align-items: center;
      justify-content: center; gap: 3px; cursor: pointer; box-shadow: var(--glass-edge);
      transition: border-color var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out); }
    .slot:hover { border-color: var(--line-strong); transform: translateY(-1px); }
    .slot.active { border-color: var(--accent-bright); background: color-mix(in srgb, var(--accent) 14%, var(--bg-2)); }
    .slot:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 2px; }
    .slot.filled .slot-name { color: var(--accent-bright); }
    .slot-name { color: var(--text-mute); font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .slot b { font-size: 11px; text-align: center; }
    .slot small { color: var(--text-dim); font-size: 9px; text-align: center; }
    .slot .empty { color: var(--text-faint); font-size: 12px; }

    .picker { border: 1px solid var(--line); border-radius: var(--r-md); background: var(--glass-bg-strong); padding: var(--sp-3); box-shadow: var(--glass-edge); }
    .picker-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--sp-3); }
    .options { display: flex; flex-direction: column; gap: var(--sp-2); max-height: 280px; overflow-y: auto; }
    .option { display: flex; align-items: center; gap: 10px; text-align: left; cursor: pointer;
      border: 1px solid var(--line); border-radius: var(--r-sm); background: var(--bg-2); color: inherit; padding: 7px 9px; }
    .option:not([disabled]):hover { border-color: var(--accent); }
    .option:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 2px; }
    .option[disabled] { opacity: 0.5; cursor: not-allowed; }
    .option span { display: flex; flex-direction: column; }
    .option small { color: var(--text-dim); font-size: 10px; }
    .t-tag { color: var(--gold-bright) !important; font-weight: 800; }
    .req { color: var(--danger) !important; }
    .pick-hint { margin: 0; }

    /* DIREITA */
    .roster-col { display: flex; flex-direction: column; gap: var(--sp-2); }
    .roster-title { font-size: 13px; font-weight: 800; color: var(--accent-bright); text-transform: uppercase; letter-spacing: 0.08em; }
    .roster { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; max-height: 560px; overflow-y: auto; padding-right: 2px; }
    .roster::-webkit-scrollbar { width: 5px; }
    .roster::-webkit-scrollbar-thumb { background: var(--bg-4); border-radius: var(--r-full); }
    .card { background: var(--glass-bg); border: 1px solid var(--line-strong); border-radius: var(--r-md); padding: 10px 6px 8px;
      display: flex; flex-direction: column; align-items: center; gap: 4px; color: inherit; cursor: pointer;
      box-shadow: inset 0 1px 0 color-mix(in srgb, var(--rc) 40%, transparent); }
    .card:hover { border-color: var(--rc); }
    .card:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 2px; }
    .card.selected { outline: 2px solid var(--accent-bright); outline-offset: 2px; background: color-mix(in srgb, var(--accent) 12%, var(--glass-bg)); }
    .card-name { font-size: 12px; font-weight: 700; text-align: center; }
    .card-stars { font-size: 9px; }

    .btn.tiny { padding: 4px 9px; font-size: 11px; }
    .muted { color: var(--text-mute); }
    .small { font-size: 11px; }

    @media (max-width: 980px) {
      .page { padding: var(--sp-5) var(--sp-4); }
      .cols { grid-template-columns: 1fr; }
      .slots { grid-template-columns: repeat(2, 1fr); }
      .roster { max-height: none; }
    }
  `],
})
export class PrerunPage implements OnDestroy {
  readonly equipmentSlots = EQUIPMENT_SLOTS;
  readonly tierNum = signal(1);
  readonly selected = signal<WaifuDef | null>(null);
  readonly selectedSlot = signal<EquipmentSlot | null>(null);
  readonly busy = signal(false);

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

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {
    this.tierNum.set(Number(this.route.snapshot.paramMap.get('tier') ?? '1'));
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

  private initTimer: ReturnType<typeof setInterval> | null = null;

  private stopInit(): void {
    if (this.initTimer !== null) { clearInterval(this.initTimer); this.initTimer = null; }
  }

  ngOnDestroy(): void { this.stopInit(); }

  rarityColor(r: number): string { return RARITY_COLORS[r] ?? 'var(--text)'; }
  elementLabel(e: string): string { return ELEMENT_LABELS[e] ?? e; }
  weaponLabel(w: string): string { return WEAPON_LABELS[w] ?? w; }

  select(w: WaifuDef): void { this.selected.set(w); this.selectedSlot.set(null); }
  toggleSlot(slot: EquipmentSlot): void { this.selectedSlot.set(this.selectedSlot() === slot ? null : slot); }

  classFor(w: WaifuDef): ClassDef | undefined {
    return this.api.catalog()?.classes.find((c) => c.id === w.classId);
  }
  affinityLevel(id: string): number { return this.api.account()?.affinity?.[id]?.level ?? 1; }
  ascension(id: string): number { return this.api.account()?.ascension?.[id] ?? 0; }

  skinFor(w: WaifuDef): SkinDef {
    const selectedId = this.api.account()?.selectedSkins?.[w.id];
    return w.skins.find((s) => s.id === selectedId) ?? w.skins[0];
  }

  itemById(itemId: number): ItemCatalogEntry | undefined {
    return this.api.catalog()?.items.find((i) => i.itemId === itemId);
  }

  equippedItem(waifuId: string, slot: EquipmentSlot): ItemCatalogEntry | undefined {
    const itemId = this.api.account()?.equipment?.[equipKey(waifuId, this.tierNum())]?.[slot];
    return itemId === undefined ? undefined : this.itemById(itemId);
  }

  mountLookType(waifuId: string): number {
    return this.equippedItem(waifuId, 'mount')?.mountLookType || this.skinFor(this.selected()!).mountLookType || 0;
  }

  candidates(waifuId: string, slot: EquipmentSlot): ItemCatalogEntry[] {
    const inventory = this.api.account()?.inventory ?? [];
    const tier = this.tierNum();
    return inventory
      .map((stack) => this.itemById(stack.itemId))
      .filter((item): item is ItemCatalogEntry =>
        item?.slot === slot && (item.tier === 0 || item.tier === tier))
      .sort((a, b) =>
        Number(this.canEquipById(waifuId, b)) - Number(this.canEquipById(waifuId, a))
        || (b.attack + b.armor + b.defense + b.mountSpeed) - (a.attack + a.armor + a.defense + a.mountSpeed));
  }

  canEquip(waifu: WaifuDef, item: ItemCatalogEntry): boolean { return !this.itemRequirement(waifu, item); }

  itemRequirement(waifu: WaifuDef, item: ItemCatalogEntry): string {
    if (item.allowedClassIds.length && !item.allowedClassIds.includes(waifu.classId))
      return `Restrito a ${item.allowedClassIds.join(', ')}`;
    const mastery = this.api.account()?.mastery?.[waifu.id];
    const total = (mastery?.points ?? 0) + (mastery?.spent ?? 0);
    if (total < item.requiredMasteryPoints) return `Requer ${item.requiredMasteryPoints} maestria`;
    return '';
  }

  private canEquipById(waifuId: string, item: ItemCatalogEntry): boolean {
    const waifu = this.ownedWaifus().find((w) => w.id === waifuId);
    return !!waifu && this.canEquip(waifu, item);
  }

  slotLabel(slot: EquipmentSlot): string {
    return this.equipmentSlots.find((s) => s.id === slot)?.label ?? slot;
  }

  itemStats(item: ItemCatalogEntry): string {
    return [
      item.attack ? `ATK ${item.attack}` : '',
      item.armor ? `ARM ${item.armor}` : '',
      item.defense ? `DEF ${item.defense}` : '',
      item.mountSpeed ? `VEL ${item.mountSpeed}` : '',
      item.critChance ? `CRIT +${Math.round(item.critChance * 100)}%` : '',
    ].filter(Boolean).join(' · ') || 'equipável';
  }

  equipmentTotals(waifuId: string): { label: string; value: string }[] {
    let atk = 0, arm = 0, def = 0, crit = 0;
    for (const slot of EQUIPMENT_SLOTS) {
      const item = this.equippedItem(waifuId, slot.id);
      if (!item) continue;
      atk += item.attack ?? 0; arm += item.armor ?? 0; def += item.defense ?? 0; crit += item.critChance ?? 0;
    }
    const out: { label: string; value: string }[] = [];
    if (atk) out.push({ label: 'ATK', value: `+${atk}` });
    if (arm) out.push({ label: 'ARM', value: `+${arm}` });
    if (def) out.push({ label: 'DEF', value: `+${def}` });
    if (crit) out.push({ label: 'CRIT', value: `+${Math.round(crit * 100)}%` });
    return out;
  }

  /** Bônus agregado do set (ATK/HP aprox.) para o resumo da coluna esquerda. */
  bonus(kind: 'atk' | 'hp'): string {
    const w = this.selected();
    if (!w) return '';
    let atk = 0, hp = 0;
    for (const slot of EQUIPMENT_SLOTS) {
      const item = this.equippedItem(w.id, slot.id);
      if (!item) continue;
      atk += item.attack ?? 0;
      hp += (item.armor ?? 0) * 4 + (item.defense ?? 0) * 6;
    }
    if (kind === 'atk') return atk ? `+${atk}` : '';
    return hp ? `+${hp}` : '';
  }

  async equip(waifuId: string, slot: EquipmentSlot, itemId: number): Promise<void> {
    this.busy.set(true);
    try { await this.api.equipItem(waifuId, slot, itemId, this.tierNum()); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async unequip(waifuId: string, slot: EquipmentSlot): Promise<void> {
    this.busy.set(true);
    try { await this.api.unequipItem(waifuId, slot, this.tierNum()); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  enter(): void {
    const w = this.selected();
    if (!w) return;
    void this.router.navigate(['/game', this.tierNum()], { queryParams: { waifu: w.id } });
  }

  back(): void { void this.router.navigate(['/hunt', 'dungeon']); }
}
