import { Component, EventEmitter, Input, OnInit, Output, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { OutfitThumb } from '../../core/outfit-thumb';
import { KaeliAuthoringMetadata, KaeliSkinDefinition, RARITY_COLORS, SkinDef, WaifuDef } from '../../core/types';
import { StudioSeed } from './kaeli-studio';

/**
 * A wardrobe skin with its resolved origin:
 * - pristine static skin (from code, no override) is read-only, but "Edit visual" creates an override;
 * - override (static id with authored record) is editable, and "Restore default" removes the override;
 * - pure authored skin (new id) is editable, rebindable, and reorderable.
 */
interface WardrobeSkin {
  skin: SkinDef;
  isDefault: boolean;
  isStatic: boolean;
  isOverride: boolean;
  isPureAuthored: boolean;
  editable: boolean;
  record?: KaeliSkinDefinition;
}

interface PendingAction { skin: KaeliSkinDefinition; restore: boolean; }

/**
 * Per-Kaeli wardrobe: the skin management face of the Kaelis tab. Lists the roster, shows ALL
 * skins for each Kaeli (default/static skins from code + authored skins from Outfit Studio), and
 * manages authored data without repainting: unlock rule, order, rebinding to another Kaeli, and deletion.
 * Visual design stays in Outfit Studio; "New skin" / "Edit visual" emit a StudioSeed.
 */
@Component({
  selector: 'app-kaeli-wardrobe',
  standalone: true,
  imports: [OutfitThumb],
  template: `
    <div class="wardrobe">
      <!-- LEFT: roster -->
      <aside class="roster panel">
        <div class="panel-head">
          <div><span class="eyebrow">Roster</span><h2>Kaelis</h2></div>
          <b class="count">{{ roster().length }}</b>
        </div>
        <div class="roster-list">
          @for (w of roster(); track w.id) {
            <button type="button" class="roster-row" [class.active]="selectedWaifuId() === w.id"
              (click)="select(w.id)">
              <app-outfit-thumb [lookType]="w.lookType" [head]="w.head" [body]="w.body"
                [legs]="w.legs" [feet]="w.feet" [size]="44" />
              <span class="row-copy">
                <strong [title]="w.name">{{ w.name }}</strong>
                <small>{{ w.title }}</small>
                <span class="row-tags">
                  <i class="star" [style.color]="rarityColor(w.rarity)">{{ w.rarity }}★</i>
                  <i class="skins">{{ w.skins.length }} skins</i>
                  @if (authoredCount(w.id); as n) { <i class="kaezan">{{ n }} Kaezan</i> }
                </span>
              </span>
            </button>
          }
        </div>
      </aside>

      <!-- MAIN: selected Kaeli wardrobe -->
      <main class="closet panel">
        @if (selected(); as w) {
          <div class="closet-head">
            <div>
              <span class="eyebrow">Wardrobe</span>
              <h2>{{ w.name }} <i class="star" [style.color]="rarityColor(w.rarity)">{{ w.rarity }}★</i></h2>
              <small>{{ skinsForSelected().length }} skins · {{ authoredCount(w.id) }} authored</small>
            </div>
            <button type="button" class="primary" (click)="newSkin()">+ New skin</button>
          </div>

          @if (status(); as state) {
            <div class="status" [class.ok]="state.kind === 'ok'" [class.err]="state.kind === 'err'">{{ state.msg }}</div>
          }

          <div class="grid">
            @for (item of skinsForSelected(); track item.skin.id) {
              <article class="skin-card" [class.authored]="item.editable">
                <div class="thumb">
                  <app-outfit-thumb [lookType]="item.skin.lookType" [head]="item.skin.head"
                    [body]="item.skin.body" [legs]="item.skin.legs" [feet]="item.skin.feet"
                    [addons]="item.skin.addons ?? 0" [mountLookType]="item.skin.mountLookType ?? 0" [size]="92" />
                </div>
                <div class="meta">
                  <div class="meta-head">
                    <strong [title]="item.skin.name">{{ item.skin.name }}</strong>
                    @if (item.isDefault) { <i class="origin default">Default</i> }
                    @if (item.isOverride) { <i class="origin edited">Edited</i> }
                    @else if (item.isPureAuthored) { <i class="origin kaezan">Kaezan</i> }
                    @else if (!item.isDefault) { <i class="origin static">Static</i> }
                  </div>
                  <code class="sid">{{ item.skin.id }}</code>

                  @if (item.editable && item.record && !item.isDefault) {
                    <div class="row">
                      <label>Unlock
                        <select [value]="item.record.unlock" (change)="setUnlock(item.record, $any($event.target).value)">
                          @for (k of unlockKinds(); track k) { <option [value]="k">{{ unlockLabel(k) }}</option> }
                        </select>
                      </label>
                      @if (item.record.unlock !== 'default') {
                        <label>{{ unlockValueLabel(item.record.unlock) }}
                          <input type="number" min="0" [value]="item.record.unlockValue"
                            (change)="setUnlockValue(item.record, $any($event.target).value)" />
                        </label>
                      }
                    </div>
                  } @else {
                    <span class="lock-badge">{{ unlockLabel(item.skin.unlock) }}{{ item.skin.unlock !== 'default' ? ' · ' + item.skin.unlockValue : '' }}</span>
                  }

                  @if (item.isPureAuthored && item.record) {
                    <label class="rebind">Bind to
                      <select [value]="item.record.waifuId" (change)="rebind(item.record, $any($event.target).value)">
                        @for (k of roster(); track k.id) { <option [value]="k.id">{{ k.name }}</option> }
                      </select>
                    </label>
                  }

                  <footer>
                    @if (item.isPureAuthored) {
                      <button type="button" [disabled]="!canMove(item.skin.id, -1)" (click)="move(item.record!, -1)" title="Move up">↑</button>
                      <button type="button" [disabled]="!canMove(item.skin.id, 1)" (click)="move(item.record!, 1)" title="Move down">↓</button>
                    }
                    <button type="button" (click)="editVisual(item)">Edit visual</button>
                    @if (item.isOverride) {
                      <button type="button" class="danger" (click)="requestRestore(item.record!)">Restore default</button>
                    } @else if (item.isPureAuthored) {
                      <button type="button" class="danger" (click)="requestDelete(item.record!)">Delete</button>
                    }
                  </footer>

                  @if (item.isStatic && !item.editable) {
                    <p class="static-note">Code skin. "Edit visual" creates an editable override; nothing is renamed.</p>
                  }
                </div>
              </article>
            }
          </div>

          <p class="hint">
            <b>"Edit visual"</b> works on any skin, including default and static skins:
            the edit becomes an override with the same id (nothing is renamed) and gains <i>Restore default</i>
            to return to code. <b>Kaezan</b> skins (new id) can also be rebound to another Kaeli
            and reordered (affecting the Hub skin selector). The default skin always stays free.
          </p>
        } @else {
          <div class="loading">Loading wardrobe...</div>
        }
      </main>

      @if (pendingAction(); as action) {
        <div class="modal-backdrop" (click)="cancelDelete()">
          <section class="confirm-modal" (click)="$event.stopPropagation()">
            <span class="eyebrow">{{ action.restore ? 'Restore default' : 'Delete skin' }}</span>
            <h2>{{ action.skin.name }}</h2>
            @if (action.restore) {
              <p>Removes override <code>{{ action.skin.id }}</code> and restores the visual defined in code.</p>
            } @else {
              <p>Permanently removes <code>{{ action.skin.id }}</code> from {{ waifuName(action.skin.waifuId) }}.</p>
            }
            <div class="actions">
              <button type="button" class="secondary" (click)="cancelDelete()">Cancel</button>
              <button type="button" class="danger solid" [disabled]="deleting()" (click)="confirmDelete()">
                {{ deleting() ? 'Applying...' : (action.restore ? 'Restore' : 'Delete') }}
              </button>
            </div>
          </section>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .wardrobe { display: grid; grid-template-columns: 300px minmax(0, 1fr); gap: 14px; align-items: start; }
    .panel { border-radius: 8px; min-width: 0; padding: 14px; background: #0c0c14; border: 1px solid #20202e; }
    .roster { box-sizing: border-box; display: flex; flex-direction: column; height: calc(100vh - 82px); overflow: hidden; position: sticky; top: 68px; }
    .panel-head, .closet-head { align-items: center; display: flex; gap: 10px; justify-content: space-between; }
    h2, h3, p { margin: 0; } h2 { font-size: 20px; }
    .eyebrow { color: #2dd4bf; font-size: 9px; font-weight: 900; letter-spacing: 1.2px; text-transform: uppercase; }
    .count { background: #192d2a; border: 1px solid #2d6b63; border-radius: 4px; color: #58daca; font-size: 10px; padding: 5px 7px; }
    button { border: 1px solid transparent; border-radius: 5px; color: #dddbe8; font: inherit; cursor: pointer; }
    button:disabled { cursor: default; opacity: .4; }
    .primary { background: #1db9aa; color: #061d1a; min-height: 34px; padding: 0 13px; font-size: 10px; font-weight: 900; }
    .secondary { background: #1b1b28; border-color: #343448; min-height: 34px; padding: 0 13px; font-size: 10px; font-weight: 900; }
    input, select { background: #0d0d15; border: 1px solid #303043; border-radius: 5px; color: #eceaf3; font: inherit; outline: none; box-sizing: border-box; width: 100%; min-height: 32px; padding: 0 8px; }
    input:focus, select:focus { border-color: #2ab5a5; }
    label { color: #8e8ca0; display: flex; flex-direction: column; gap: 4px; font-size: 9px; font-weight: 800; }
    .roster-list { align-content: start; display: grid; flex: 1 1 auto; gap: 5px; min-height: 0; overflow-y: auto; padding-right: 3px; margin-top: 12px; }
    .roster-row { align-items: center; background: #11111a; border-color: #29293a; display: grid; gap: 8px; grid-template-columns: 44px minmax(0, 1fr); min-width: 0; padding: 4px 6px 4px 0; text-align: left; }
    .roster-row:hover, .roster-row.active { background: #17201f; border-color: #2f8e82; }
    .row-copy, .row-copy strong, .row-copy small { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .row-copy strong { font-size: 11px; } .row-copy small { color: #858297; font-size: 8px; margin-top: 1px; }
    .row-tags { display: flex; gap: 3px; margin-top: 4px; }
    .row-tags i { border-radius: 3px; font-size: 7px; font-style: normal; font-weight: 900; padding: 2px 4px; background: #1c1c28; color: #a8a6b8; }
    .row-tags i.kaezan { background: #173b35; color: #58dbc9; }
    .closet { min-width: 0; }
    .closet-head { border-bottom: 1px solid #29293a; padding-bottom: 12px; }
    .closet-head small { color: #858297; display: block; font-size: 10px; margin-top: 4px; }
    .star { font-style: normal; font-weight: 900; }
    .status { border: 1px solid; border-radius: 5px; font-size: 10px; margin-top: 10px; padding: 8px 10px; }
    .status.ok { background: #102a25; border-color: #22675d; color: #55e5cf; }
    .status.err { background: #32191e; border-color: #6d303b; color: #ff9aa5; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 10px; margin-top: 14px; }
    .skin-card { background: #11111a; border: 1px solid #29293a; border-radius: 7px; display: grid; grid-template-columns: 104px minmax(0, 1fr); overflow: hidden; }
    .skin-card.authored { border-color: #2f6b63; }
    .thumb { align-items: center; background: radial-gradient(circle, #2b2b3c, #10101a 70%); border-right: 1px solid #29293a; display: grid; place-items: center; }
    .meta { display: grid; gap: 6px; min-width: 0; padding: 9px 10px; align-content: start; }
    .meta-head { align-items: center; display: flex; gap: 6px; min-width: 0; }
    .meta-head strong { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .origin { border-radius: 3px; font-size: 7px; font-style: normal; font-weight: 900; margin-left: auto; padding: 2px 5px; }
    .origin.default { background: #1b2a3b; color: #74c0e8; } .origin.kaezan { background: #173b35; color: #58dbc9; } .origin.static { background: #26262f; color: #a8a6b8; } .origin.edited { background: #3a2b16; color: #e8b86a; }
    .sid { color: #6f6d80; font-size: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .row { display: grid; gap: 6px; grid-template-columns: 1fr 1fr; }
    .rebind { margin-top: 2px; }
    .lock-badge { background: #181823; border: 1px solid #303043; border-radius: 4px; color: #b79cf0; font-size: 9px; font-weight: 800; padding: 4px 6px; width: fit-content; }
    .static-note { color: #6f6d80; font-size: 8px; line-height: 1.4; }
    .skin-card footer { display: flex; gap: 5px; margin-top: 2px; }
    .skin-card footer button { background: #171722; border: 1px solid #2b2b3d; font-size: 9px; font-weight: 800; min-height: 28px; padding: 0 8px; }
    .skin-card footer button:first-child, .skin-card footer button:nth-child(2) { padding: 0 9px; }
    .skin-card footer button:hover:not(:disabled) { border-color: #505068; }
    .danger { color: #ef8b98; }
    .danger.solid { background: #7b303b; border-color: #a84957; color: #fff0f2; min-height: 34px; padding: 0 12px; font-weight: 900; }
    .hint { color: #77758b; font-size: 9px; line-height: 1.6; margin-top: 14px; }
    .loading { color: #77758b; padding: 70px; text-align: center; }
    .modal-backdrop { align-items: center; background: rgb(4 4 8 / 78%); display: flex; inset: 0; justify-content: center; position: fixed; z-index: 1000; }
    .confirm-modal { background: #12121b; border: 1px solid #3b3b50; border-radius: 8px; box-shadow: 0 20px 70px #000; max-width: 410px; padding: 20px; width: calc(100% - 32px); }
    .confirm-modal h2 { margin-top: 4px; } .confirm-modal p { color: #9996a8; font-size: 10px; line-height: 1.5; margin: 10px 0; }
    .confirm-modal .actions { display: flex; gap: 7px; justify-content: flex-end; margin-top: 16px; }
    @media (max-width: 1100px) {
      .wardrobe { grid-template-columns: 1fr; }
      .roster { height: 360px; position: static; }
    }
  `],
})
export class KaeliWardrobe implements OnInit {
  /** Kaeli selected on mount (the host preserves selection between Outfit Studio trips). */
  @Input() initialWaifuId = '';
  /** Asks the host (KaeliManager) to open Outfit Studio to create/edit a skin. */
  @Output() readonly openStudio = new EventEmitter<StudioSeed>();
  /** Tells the host which Kaeli is selected so it can be preserved across mounts. */
  @Output() readonly waifuSelected = new EventEmitter<string>();

  readonly authored = signal<KaeliSkinDefinition[]>([]);
  readonly meta = signal<KaeliAuthoringMetadata | null>(null);
  readonly affinityMax = signal(10);
  readonly selectedWaifuId = signal('');

  readonly deleting = signal(false);
  readonly pendingAction = signal<PendingAction | null>(null);
  readonly status = signal<{ kind: 'ok' | 'err'; msg: string } | null>(null);

  readonly unlockKinds = computed(() => this.meta()?.unlockKinds ?? ['default', 'affinity', 'gold', 'kaeros']);
  readonly roster = computed<WaifuDef[]>(() => this.api.catalog()?.waifus ?? []);
  readonly selected = computed(() => this.roster().find((w) => w.id === this.selectedWaifuId()));
  readonly authoredById = computed(() => new Map(this.authored().map((s) => [s.id, s])));

  /** Static ids (from code) + selected Kaeli default id, used to classify each skin. */
  readonly staticInfo = computed(() => {
    const kaeli = this.meta()?.kaelis.find((k) => k.id === this.selectedWaifuId());
    return { staticIds: new Set(kaeli?.staticSkinIds ?? []), defaultId: kaeli?.defaultSkinId ?? '' };
  });

  readonly skinsForSelected = computed<WardrobeSkin[]>(() => {
    const waifu = this.selected();
    if (!waifu) return [];
    const byId = this.authoredById();
    const { staticIds, defaultId } = this.staticInfo();
    return waifu.skins.map((skin) => {
      const record = byId.get(skin.id);
      const isStatic = staticIds.has(skin.id);
      return {
        skin,
        isDefault: skin.id === defaultId,
        isStatic,
        isOverride: isStatic && !!record,
        isPureAuthored: !isStatic && !!record,
        editable: !!record,
        record,
      };
    });
  });

  /** Display order for pure authored skins only (the only reorderable skins; static ones stay fixed). */
  readonly authoredOrder = computed<string[]>(() =>
    this.skinsForSelected().filter((s) => s.isPureAuthored).map((s) => s.skin.id));

  constructor(private readonly api: ApiService) {}

  async ngOnInit(): Promise<void> {
    try {
      const [, metadata] = await Promise.all([
        this.api.loadCatalog(),
        this.api.getKaeliAuthoringMetadata(),
      ]);
      this.meta.set(metadata);
      this.affinityMax.set(metadata.affinityMaxLevel);
      await this.refreshAuthored();
      if (!this.selectedWaifuId()) {
        const exists = this.roster().some((w) => w.id === this.initialWaifuId);
        this.selectedWaifuId.set(exists ? this.initialWaifuId : (this.roster()[0]?.id ?? ''));
      }
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    }
  }

  /** Reloads the authored skin list after each mutation without relying only on the catalog. */
  async refreshAuthored(): Promise<void> {
    this.authored.set(await this.api.getAuthoredKaeliSkins());
  }

  select(waifuId: string): void {
    this.selectedWaifuId.set(waifuId);
    this.waifuSelected.emit(waifuId);
    this.status.set(null);
  }

  authoredCount(waifuId: string): number {
    return this.authored().filter((s) => s.waifuId === waifuId).length;
  }

  rarityColor(rarity: number): string { return RARITY_COLORS[rarity] ?? '#cfccd9'; }

  waifuName(waifuId: string): string {
    return this.roster().find((w) => w.id === waifuId)?.name ?? waifuId;
  }

  unlockLabel(kind: string): string {
    const labels: Record<string, string> = {
      default: 'Default (free)', affinity: 'Affinity', gold: 'Gold', kaeros: 'Kaeros',
    };
    return labels[kind] ?? kind;
  }
  unlockValueLabel(kind: string): string {
    const labels: Record<string, string> = {
      affinity: 'Affinity level', gold: 'Price (gold)', kaeros: 'Price (Kaeros)',
    };
    return labels[kind] ?? 'Valor';
  }

  canMove(skinId: string, dir: -1 | 1): boolean {
    const order = this.authoredOrder();
    const i = order.indexOf(skinId);
    return i >= 0 && i + dir >= 0 && i + dir < order.length;
  }

  // ---- mutations ----
  newSkin(): void {
    const waifuId = this.selectedWaifuId();
    if (waifuId) this.openStudio.emit({ mode: 'new', waifuId });
  }

  /** Opens the studio to edit the visual. For a pristine static skin, seeds an override (same id). */
  editVisual(item: WardrobeSkin): void {
    const waifuId = this.selectedWaifuId();
    const skin: KaeliSkinDefinition = item.record
      ? { ...item.record }
      : {
          waifuId,
          id: item.skin.id,
          name: item.skin.name,
          description: item.skin.description,
          lookType: item.skin.lookType,
          head: item.skin.head, body: item.skin.body, legs: item.skin.legs, feet: item.skin.feet,
          addons: item.skin.addons ?? 0,
          mountLookType: item.skin.mountLookType ?? 0,
          unlock: item.skin.unlock,
          unlockValue: item.skin.unlockValue,
        };
    this.openStudio.emit({ mode: 'edit', waifuId, skin });
  }

  async setUnlock(record: KaeliSkinDefinition, unlock: string): Promise<void> {
    const kind = unlock as KaeliSkinDefinition['unlock'];
    const unlockValue = kind === 'default' ? 0 : Math.max(kind === 'affinity' ? 1 : 0, record.unlockValue);
    await this.persist({ ...record, unlock: kind, unlockValue }, `${record.name} unlock updated.`);
  }

  async setUnlockValue(record: KaeliSkinDefinition, value: string): Promise<void> {
    let unlockValue = Math.max(0, Math.floor(+value || 0));
    if (record.unlock === 'affinity') unlockValue = Math.min(Math.max(1, unlockValue), this.affinityMax());
    await this.persist({ ...record, unlockValue }, `${record.name} unlock updated.`);
  }

  async rebind(record: KaeliSkinDefinition, waifuId: string): Promise<void> {
    if (waifuId === record.waifuId) return;
    const target = this.waifuName(waifuId);
    await this.persist({ ...record, waifuId }, `${record.name} now belongs to ${target}.`);
    this.selectedWaifuId.set(waifuId); // follow the skin to its new owner
    this.waifuSelected.emit(waifuId);
  }

  async move(record: KaeliSkinDefinition, dir: -1 | 1): Promise<void> {
    const order = [...this.authoredOrder()];
    const i = order.indexOf(record.id);
    const j = i + dir;
    if (i < 0 || j < 0 || j >= order.length) return;
    [order[i], order[j]] = [order[j], order[i]];
    try {
      await this.api.reorderKaeliSkins(record.waifuId, order);
      await this.refreshAuthored();
      this.status.set({ kind: 'ok', msg: 'Skin order updated.' });
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    }
  }

  private async persist(record: KaeliSkinDefinition, okMsg: string): Promise<void> {
    this.status.set(null);
    try {
      await this.api.updateKaeliSkin(record);
      await this.refreshAuthored();
      this.status.set({ kind: 'ok', msg: okMsg });
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    }
  }

  requestDelete(skin: KaeliSkinDefinition): void { this.pendingAction.set({ skin, restore: false }); }
  requestRestore(skin: KaeliSkinDefinition): void { this.pendingAction.set({ skin, restore: true }); }
  cancelDelete(): void { if (!this.deleting()) this.pendingAction.set(null); }

  async confirmDelete(): Promise<void> {
    const action = this.pendingAction();
    if (!action) return;
    const skin = action.skin;
    this.deleting.set(true);
    try {
      await this.api.deleteKaeliSkin(skin.id);
      await this.refreshAuthored();
      this.pendingAction.set(null);
      this.status.set({
        kind: 'ok',
        msg: action.restore ? `${skin.name} returned to the default visual.` : `${skin.name} was deleted.`,
      });
    } catch (error) {
      this.pendingAction.set(null);
      this.status.set({ kind: 'err', msg: (error as Error).message });
    } finally {
      this.deleting.set(false);
    }
  }
}
