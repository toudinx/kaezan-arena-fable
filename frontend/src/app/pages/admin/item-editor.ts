import { Component, OnInit, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { ItemIcon } from '../../core/item-icon';
import { AdminItem, EquipmentSlot, ItemBalanceMetadata } from '../../core/types';

type PercentField = keyof Pick<AdminItem,
  'critChance' | 'critDamage' | 'lifeStealChance' | 'lifeStealAmount'
  | 'cooldownReduction' | 'moveSpeedPercent' | 'physicalResistance'
  | 'fireResistance' | 'iceResistance' | 'earthResistance'
  | 'energyResistance' | 'deathResistance' | 'holyResistance'>;
type ItemBonusId = 'critDamage' | 'critChance' | 'vampiric' | 'cooldownReduction'
  | 'moveSpeedPercent' | 'elementAffinity' | 'physicalResistance' | 'elementResistance';

interface BonusControl {
  id: ItemBonusId;
  label: string;
  hint: string;
}

const SLOT_OPTIONS: { id: EquipmentSlot; label: string }[] = [
  { id: 'weapon', label: 'Weapon' },
  { id: 'armor', label: 'Armor' },
  { id: 'helmet', label: 'Helmet' },
  { id: 'ring', label: 'Ring' },
  { id: 'necklace', label: 'Amulet' },
  { id: 'mount', label: 'Mount' },
];

const WEAPON_TYPES = ['sword', 'axe', 'club', 'distance', 'wand', 'rod', 'fist', 'shield'];
const ELEMENT_RESISTANCE_FIELDS: { key: PercentField; element: string; label: string }[] = [
  { key: 'fireResistance', element: 'fire', label: 'Fire' },
  { key: 'iceResistance', element: 'ice', label: 'Ice' },
  { key: 'earthResistance', element: 'earth', label: 'Earth' },
  { key: 'energyResistance', element: 'energy', label: 'Energy' },
  { key: 'deathResistance', element: 'death', label: 'Death' },
  { key: 'holyResistance', element: 'holy', label: 'Holy' },
];

@Component({
  selector: 'app-item-editor',
  standalone: true,
  imports: [ItemIcon],
  template: `
    <div class="studio">
      <aside class="panel library">
        <header>
          <div><span class="eyebrow">Visual Archive</span><h2>Canary Library</h2></div>
          <b>{{ filteredLibrary().length }}</b>
        </header>
        <input type="search" placeholder="Name or item id" [value]="search()"
          (input)="setSearch($any($event.target).value)" />
        <div class="filters">
          <select [value]="category()" (change)="setCategory($any($event.target).value)">
            <option value="">All categories</option>
            @for (value of categories(); track value) { <option [value]="value">{{ value }}</option> }
          </select>
          <select [value]="subcategory()" (change)="setSubcategory($any($event.target).value)">
            <option value="">All types</option>
            @for (value of subcategories(); track value) { <option [value]="value">{{ value }}</option> }
          </select>
        </div>
        <div class="scroll">
          @for (item of visibleLibrary(); track item.itemId) {
            <button class="source" type="button"
              [class.active]="draft()?.sourceItemId === item.itemId" (click)="createFrom(item)">
              <app-item-icon [itemId]="item.itemId" [size]="38" />
              <span><strong>{{ item.name }}</strong><small>#{{ item.itemId }} - {{ item.subcategory }}</small></span>
            </button>
          } @empty { <p class="empty">No item found.</p> }
          @if (visibleLibrary().length < filteredLibrary().length) {
            <button type="button" class="more" (click)="showMore()">
              Show more ({{ filteredLibrary().length - visibleLibrary().length }})
            </button>
          }
        </div>
      </aside>

      <main class="panel editor">
        <header>
          <div>
            <span class="eyebrow">Item Studio</span>
            <h2>{{ draft()?.itemId ? 'Edit item' : 'New item' }}</h2>
          </div>
          <div class="head-actions">
            @if (draft()?.itemId) { <button type="button" (click)="duplicate()">Duplicate</button> }
            <button type="button" class="primary" [disabled]="busy() || !draft()" (click)="save()">
              {{ saving() ? 'Saving...' : 'Save item' }}
            </button>
          </div>
        </header>

        @if (status(); as value) {
          <div class="status" [class.error]="value.kind === 'error'">{{ value.message }}</div>
        }

        @if (draft(); as item) {
          <section class="identity-card">
            <app-item-icon [itemId]="item.sourceItemId" [size]="76" />
            <div class="identity-fields">
              <div class="source-line">
                <span class="eyebrow">Visual Canary</span>
                <small>#{{ item.sourceItemId }} - {{ sourceName() }}</small>
              </div>
              <div class="grid two">
                <label>Name
                  <input [value]="item.name" (input)="patchText('name', $any($event.target).value)" />
                </label>
                <label>Sell value
                  <output class="stat-readout">{{ item.salePrice }}</output>
                </label>
              </div>
              <div class="grid four">
                <label>Type
                  <select [value]="item.slot || 'weapon'" (change)="setSlot($any($event.target).value)">
                    @for (slot of slotOptions; track slot.id) { <option [value]="slot.id">{{ slot.label }}</option> }
                  </select>
                </label>
                <label>Weapon type
                  <select [disabled]="item.slot !== 'weapon'" [value]="item.weaponType || 'sword'"
                    (change)="patchText('weaponType', $any($event.target).value)">
                    @for (type of weaponTypes; track type) { <option [value]="type">{{ type }}</option> }
                  </select>
                </label>
                <label>Element
                  <select [value]="item.element" (change)="patchText('element', $any($event.target).value)">
                    @for (element of elements(); track element) { <option [value]="element">{{ element }}</option> }
                  </select>
                </label>
                <label>Tier
                  <select [value]="item.tier" (change)="setTier($any($event.target).value)">
                    @for (tier of balanceTiers(); track tier) {
                      <option [value]="tier">{{ tier === 0 ? 'Tier 0' : 'Tier ' + tier }}</option>
                    }
                  </select>
                </label>
              </div>
              <div class="grid two">
                <label>Category
                  <select [value]="item.tag || 'normal'" (change)="setTag($any($event.target).value)">
                    @for (tag of itemTags(); track tag.id) { <option [value]="tag.id">{{ tag.name }}</option> }
                  </select>
                </label>
                <label>Relic multiplier
                  <input type="number" step="0.05"
                    [disabled]="item.tag !== 'relic'"
                    [min]="balance()?.relicMultiplierMin ?? 1.05"
                    [max]="balance()?.relicMultiplierMax ?? 1.6"
                    [value]="item.statMultiplier"
                    (input)="setRelicMultiplier($any($event.target).value)" />
                </label>
              </div>
              <label>Description
                <textarea rows="2" [value]="item.description"
                  (input)="patchText('description', $any($event.target).value)"></textarea>
              </label>
            </div>
          </section>

          <section>
            <div class="section-head">
              <div>
                <h3>Tier values</h3>
                <p class="hint left">Base values and bonus magnitude are calculated by the tier curve.</p>
              </div>
              <span class="tier-pill">{{ tierSummary() }}</span>
            </div>

            <div class="base-grid">
              @if (item.slot === 'weapon') {
                <label>Base attack
                  <output class="stat-readout">{{ item.attack }}</output>
                </label>
              }
              @if (item.slot === 'armor' || item.slot === 'helmet') {
                <label>Base armor
                  <output class="stat-readout">{{ item.armor }}</output>
                </label>
              }
              @if (item.slot === 'ring' || item.slot === 'necklace') {
                <label>Base defense
                  <output class="stat-readout">{{ item.defense }}</output>
                </label>
              }
              @if (item.slot === 'mount') {
                <label>Base speed
                  <output class="stat-readout">{{ item.mountSpeed }}</output>
                </label>
              }
            </div>

            <div class="bonus-grid">
              @for (bonus of bonusControls(item); track bonus.id) {
                <article class="bonus" [class.enabled]="bonusEnabled(bonus.id)">
                  <label class="check">
                    <input type="checkbox" [checked]="bonusEnabled(bonus.id)" (change)="toggleBonus(bonus.id)" />
                    <span><strong>{{ bonus.label }}</strong><small>{{ bonus.hint }}</small></span>
                  </label>
                  @switch (bonus.id) {
                    @case ('critDamage') {
                      <label>Extra critical damage
                        <output class="stat-readout">{{ pct(item.critDamage) }}%</output>
                      </label>
                    }
                    @case ('critChance') {
                      <label>Critical chance
                        <output class="stat-readout">{{ pct(item.critChance) }}%</output>
                      </label>
                    }
                    @case ('vampiric') {
                      <div class="grid two">
                        <label>Chance
                          <output class="stat-readout">{{ pct(item.lifeStealChance) }}%</output>
                        </label>
                        <label>Life stolen
                          <output class="stat-readout">{{ pct(item.lifeStealAmount) }}%</output>
                        </label>
                      </div>
                    }
                    @case ('cooldownReduction') {
                      <label>Cooldown reduction
                        <output class="stat-readout">{{ pct(item.cooldownReduction) }}%</output>
                      </label>
                    }
                    @case ('moveSpeedPercent') {
                      <label>Movement
                        <output class="stat-readout">{{ pct(item.moveSpeedPercent) }}%</output>
                      </label>
                    }
                    @case ('elementAffinity') {
                      <label>Elemental bonus
                        <output class="stat-readout">{{ item.elementDamage }}%</output>
                      </label>
                    }
                    @case ('physicalResistance') {
                      <label>Physical resistance
                        <output class="stat-readout">{{ pct(item.physicalResistance) }}%</output>
                      </label>
                    }
                    @case ('elementResistance') {
                      <div class="grid two">
                        <label>Element
                          <select [disabled]="!bonusEnabled(bonus.id)" [value]="selectedResistanceElement(item)"
                            (change)="setElementResistance($any($event.target).value)">
                            @for (res of elementResistanceFields; track res.element) {
                              <option [value]="res.element">{{ res.label }}</option>
                            }
                          </select>
                        </label>
                        <label>Resistance
                          <output class="stat-readout">{{ pct(selectedResistanceValue(item)) }}%</output>
                        </label>
                      </div>
                    }
                  }
                </article>
              }
            </div>
          </section>

          @if (item.slot) {
            <section>
              <h3>Allowed classes</h3>
              <div class="checks">
                @for (klass of classes(); track klass.id) {
                  <label class="check"><input type="checkbox"
                    [checked]="item.allowedClassIds.includes(klass.id)"
                    (change)="toggleClass(klass.id)" />{{ klass.name }}</label>
                }
              </div>
              <p class="hint left">No checked classes means no restriction.</p>
            </section>
          }
        } @else {
          <p class="empty large">Choose a Canary item to create a Kaezan version.</p>
        }
      </main>

      <aside class="panel authored">
        <header>
          <div><span class="eyebrow">Authored Content</span><h2>Kaezan Items</h2></div>
          <button type="button" class="primary" (click)="newItem()">New</button>
        </header>
        <input type="search" placeholder="Search created item" [value]="authoredSearch()"
          (input)="authoredSearch.set($any($event.target).value)" />
        <div class="scroll">
          @for (item of filteredAuthored(); track item.itemId) {
            <button class="source authored-card" type="button"
              [class.active]="draft()?.itemId === item.itemId" (click)="edit(item)">
              <app-item-icon [itemId]="item.itemId" [size]="42" />
              <span><strong>{{ item.name }}</strong><small>#{{ item.itemId }} - {{ summary(item) }}</small></span>
            </button>
          } @empty { <p class="empty">No authored item yet. Choose a base and save the first one.</p> }
        </div>
        @if (draft()?.itemId) {
          <button type="button" class="grant" [disabled]="busy()" (click)="grant()">
            Add 1 to Backpack
          </button>
          <button type="button" class="danger" [disabled]="busy()" (click)="remove()">Delete item</button>
        }
      </aside>
    </div>
  `,
  styles: [`
    :host { display:block } .studio{display:grid;grid-template-columns:280px minmax(560px,1fr)280px;gap:12px;align-items:start}
    .panel{background:#0c0c14;border:1px solid #222232;border-radius:8px;padding:13px;min-width:0}
    .library,.authored{height:calc(100vh - 84px);position:sticky;top:68px;display:flex;flex-direction:column;box-sizing:border-box}
    header{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-bottom:10px}h2{font-size:17px;margin:1px 0 0}
    h3{font-size:10px;text-transform:uppercase;letter-spacing:.8px;color:#aaa7b8;margin:0 0 10px}
    .eyebrow{font-size:8px;text-transform:uppercase;letter-spacing:1.1px;color:#35d3bf;font-weight:900}
    header b{font-size:9px;color:#61dfcf;background:#17342f;border:1px solid #2c756a;border-radius:4px;padding:4px 6px}
    input,select,textarea{box-sizing:border-box;width:100%;background:#0c0c15;border:1px solid #303043;border-radius:5px;color:#eeeaf5;font:inherit;padding:8px;outline:none}
    .stat-readout{box-sizing:border-box;width:100%;background:#15151f;border:1px solid #2b2b3d;border-radius:5px;color:#eeeaf5;font-size:10px;font-weight:900;min-height:34px;padding:8px}
    input:focus,select:focus,textarea:focus{border-color:#31b9aa}textarea{resize:vertical}
    button{background:#171722;border:1px solid #303043;border-radius:5px;color:#e6e3ef;cursor:pointer;font:inherit}button:disabled{opacity:.5;cursor:default}
    .primary{background:#22b9aa;border-color:#22b9aa;color:#061b18;font-size:9px;font-weight:900;padding:8px 12px}
    .filters{display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-top:7px}.scroll{overflow:auto;min-height:0;flex:1;margin-top:8px}
    .source{display:grid;grid-template-columns:42px minmax(0,1fr);gap:7px;align-items:center;text-align:left;width:100%;padding:5px 7px;margin-bottom:4px}
    .source:hover{border-color:#55546a;background:#14141f}.source.active{border-color:#30b8a8;background:#16332e}
    .more{width:100%;padding:8px;font-size:9px;color:#62dfcf}
    .source span,.source strong,.source small{display:block;min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
    .source strong{font-size:10px}.source small{font-size:8px;color:#858296;margin-top:2px}
    section{border:1px solid #242434;background:#0f0f18;border-radius:7px;padding:12px;margin-top:10px}
    .identity-card{display:grid;grid-template-columns:88px minmax(0,1fr);gap:14px;align-items:start;margin-top:0}
    .identity-fields{display:grid;gap:8px}.source-line{display:flex;justify-content:space-between;gap:10px}.source-line small{color:#858296;font-size:9px}
    .section-head{align-items:flex-start;display:flex;gap:10px;justify-content:space-between}.tier-pill{background:#181827;border:1px solid #303043;border-radius:4px;color:#b9b5c8;font-size:8px;font-weight:900;padding:7px 9px;white-space:nowrap}
    .bonus-grid{display:grid;gap:8px;grid-template-columns:repeat(2,1fr);margin-top:10px}.base-grid{display:grid;gap:8px;grid-template-columns:repeat(3,1fr);margin-top:10px}
    .bonus{background:#11111b;border:1px solid #2a2a3c;border-radius:6px;display:grid;gap:8px;padding:9px}.bonus.enabled{background:#12251f;border-color:#2b7569}
    .bonus .check{background:transparent;border:0;padding:0}.bonus .check span{display:block}.bonus .check strong,.bonus .check small{display:block}.bonus .check small{color:#777487;font-size:8px;margin-top:2px}
    .warn{background:#342018;border:1px solid #7a4b25;border-radius:4px;color:#efb36d;font-size:8px;font-weight:800;line-height:1.35;padding:5px}
    label{display:flex;flex-direction:column;gap:5px;color:#9996aa;font-size:8px;font-weight:800}.grid{display:grid;gap:8px}.two{grid-template-columns:1fr 1fr}.four{grid-template-columns:repeat(4,1fr)}
    .head-actions{display:flex;gap:6px}.head-actions button:not(.primary){padding:7px 10px;font-size:9px}
    .checks{display:flex;flex-wrap:wrap;gap:6px}.check{display:flex;flex-direction:row;align-items:center;background:#151520;border:1px solid #2b2b3b;border-radius:4px;padding:7px 9px}.check input{width:auto}
    .hint,.empty{color:#777487;font-size:9px;line-height:1.5;text-align:center}.hint.left{text-align:left}.large{padding:80px 20px}.status{padding:8px 10px;border:1px solid #28766a;background:#12302b;color:#65e6d4;border-radius:5px;font-size:9px}.status.error{border-color:#773641;background:#32181d;color:#ff9ca6}
    .grant,.danger{margin-top:8px;padding:9px;font-size:9px;font-weight:900}.grant{background:#16322d;border-color:#2b796d;color:#63e3d1}.danger{background:#2d171d;border-color:#6e303d;color:#ff9aa6}.authored-card{grid-template-columns:46px minmax(0,1fr)}
    @media(max-width:1200px){.studio{grid-template-columns:230px minmax(500px,1fr)240px}.four,.bonus-grid,.base-grid{grid-template-columns:repeat(2,1fr)}}
    @media(max-width:900px){.studio{grid-template-columns:1fr}.library,.authored{height:420px;position:static}.editor{grid-row:2}.two,.four,.bonus-grid,.base-grid,.identity-card{grid-template-columns:1fr}}
  `],
})
export class ItemEditor implements OnInit {
  readonly library = signal<AdminItem[]>([]);
  readonly authored = signal<AdminItem[]>([]);
  readonly classes = signal<{ id: string; name: string }[]>([]);
  readonly elements = signal<string[]>([]);
  readonly draft = signal<AdminItem | null>(null);
  readonly search = signal('');
  readonly category = signal('');
  readonly subcategory = signal('');
  readonly authoredSearch = signal('');
  readonly libraryLimit = signal(160);
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly granting = signal(false);
  readonly status = signal<{ kind: 'ok' | 'error'; message: string } | null>(null);
  readonly balance = signal<ItemBalanceMetadata | null>(null);
  readonly enabledBonuses = signal<Record<string, boolean>>({});
  readonly selectedElementResistance = signal('fire');

  readonly slotOptions = SLOT_OPTIONS;
  readonly weaponTypes = WEAPON_TYPES;
  readonly elementResistanceFields = ELEMENT_RESISTANCE_FIELDS;
  readonly busy = computed(() => this.saving() || this.deleting() || this.granting());
  readonly balanceTiers = computed(() => this.balance()?.tiers ?? [0, 1, 2, 3, 4, 5]);
  readonly itemTags = computed(() => this.balance()?.tags ?? [
    { id: 'normal' as const, name: 'Normal' },
    { id: 'relic' as const, name: 'Relic' },
  ]);
  readonly categories = computed(() => [...new Set(this.library().map((item) => item.category))].sort());
  readonly subcategories = computed(() => [...new Set(this.library()
    .filter((item) => !this.category() || item.category === this.category())
    .map((item) => item.subcategory))].sort());
  readonly filteredLibrary = computed(() => {
    const query = this.search().trim().toLowerCase();
    return this.library().filter((item) =>
      (!this.category() || item.category === this.category())
      && (!this.subcategory() || item.subcategory === this.subcategory())
      && (!query || item.name.toLowerCase().includes(query) || String(item.itemId).includes(query)));
  });
  readonly visibleLibrary = computed(() => this.filteredLibrary().slice(0, this.libraryLimit()));
  readonly filteredAuthored = computed(() => {
    const query = this.authoredSearch().trim().toLowerCase();
    return this.authored().filter((item) =>
      !query || item.name.toLowerCase().includes(query) || String(item.itemId).includes(query));
  });
  readonly sourceName = computed(() =>
    this.library().find((item) => item.itemId === this.draft()?.sourceItemId)?.name ?? 'Base removida');

  constructor(private readonly api: ApiService, private readonly assets: AssetsService) {}

  async ngOnInit(): Promise<void> {
    try {
      const [payload] = await Promise.all([
        this.api.getAdminItems(),
        this.api.loadCatalog(),
        this.assets.load(),
      ]);
      this.library.set(payload.library);
      this.authored.set(payload.authored);
      this.classes.set(payload.classes);
      this.elements.set(payload.elements);
      this.balance.set(payload.balance);
    } catch (error) {
      this.fail(error);
    }
  }

  setCategory(value: string): void {
    this.category.set(value);
    this.subcategory.set('');
    this.libraryLimit.set(160);
  }

  setSubcategory(value: string): void {
    this.subcategory.set(value);
    this.libraryLimit.set(160);
  }

  setSearch(value: string): void {
    this.search.set(value);
    this.libraryLimit.set(160);
  }

  showMore(): void {
    this.libraryLimit.update((value) => value + 160);
  }

  createFrom(source: AdminItem): void {
    const slot = source.slot ?? 'weapon';
    const draft = this.withSanitizedType({
      ...source,
      itemId: 0,
      sourceItemId: source.itemId,
      appearanceItemId: source.itemId,
      isAuthored: true,
      name: source.name,
      description: '',
      slot,
      weaponType: slot === 'weapon' ? (source.weaponType || 'sword') : null,
      tag: 'normal',
      statMultiplier: 1,
      skillPower: 0,
      requiredMasteryPoints: 0,
      allowedClassIds: [],
    });
    this.enabledBonuses.set({});
    this.draft.set(this.applyRecommended(draft));
    this.seedBonusState(this.draft()!);
    this.status.set(null);
  }

  newItem(): void {
    const source = this.filteredLibrary()[0] ?? this.library()[0];
    if (source) this.createFrom(source);
  }

  edit(item: AdminItem): void {
    const draft = this.withSanitizedType({ ...item, allowedClassIds: [...item.allowedClassIds] });
    this.seedBonusState(draft);
    this.selectedElementResistance.set(this.selectedResistanceElement(draft));
    this.draft.set(this.applyRecommended(draft));
    this.status.set(null);
  }

  duplicate(): void {
    const item = this.draft();
    if (!item) return;
    const draft = {
      ...item,
      itemId: 0,
      name: `${item.name} (copy)`,
      allowedClassIds: [...item.allowedClassIds],
    };
    this.draft.set(draft);
    this.seedBonusState(draft);
    this.status.set(null);
  }

  setSlot(value: string): void {
    const item = this.draft();
    if (!item) return;
    const slot = SLOT_OPTIONS.some((entry) => entry.id === value) ? value as EquipmentSlot : 'weapon';
    const next = this.withSanitizedType({
      ...item,
      slot,
      weaponType: slot === 'weapon' ? (item.weaponType || 'sword') : null,
    });
    this.enabledBonuses.set({});
    this.draft.set(this.applyRecommended(next));
    this.seedBonusState(this.draft()!);
    this.status.set(null);
  }

  setTier(value: string): void {
    const item = this.draft();
    if (!item) return;
    const tier = Math.min(5, Math.max(0, Math.floor(Number(value) || 0)));
    this.draft.set(this.applyRecommended({ ...item, tier }));
    this.status.set(null);
  }

  patchText(field: 'name' | 'description' | 'element' | 'weaponType', value: string): void {
    this.patch(field, value);
  }

  setTag(value: string): void {
    const item = this.draft();
    if (!item) return;
    const tag = value === 'relic' ? 'relic' : 'normal';
    this.draft.set(this.applyRecommended({
      ...item,
      tag,
      statMultiplier: tag === 'relic'
        ? (this.balance()?.relicMultiplierDefault ?? 1.25)
        : 1,
    }));
    this.status.set(null);
  }

  setRelicMultiplier(value: string): void {
    const item = this.draft();
    if (!item || item.tag !== 'relic') return;
    const config = this.balance();
    const min = config?.relicMultiplierMin ?? 1.05;
    const max = config?.relicMultiplierMax ?? 1.6;
    const statMultiplier = Math.min(max, Math.max(min, Number(value) || config?.relicMultiplierDefault || 1.25));
    this.draft.set(this.applyRecommended({ ...item, statMultiplier }));
    this.status.set(null);
  }

  toggleClass(classId: string): void {
    const item = this.draft();
    if (!item) return;
    const allowed = item.allowedClassIds.includes(classId)
      ? item.allowedClassIds.filter((id) => id !== classId)
      : [...item.allowedClassIds, classId];
    this.draft.set({ ...item, allowedClassIds: allowed });
  }

  bonusControls(item: AdminItem): BonusControl[] {
    switch (item.slot) {
      case 'weapon':
        return [{ id: 'critDamage', label: 'Critical damage', hint: 'Weapons scale critical hit damage.' }];
      case 'armor':
        return [
          { id: 'physicalResistance', label: 'Physical res.', hint: 'Armor can mitigate physical damage.' },
          { id: 'elementResistance', label: 'Elemental res.', hint: 'Choose one additional element.' },
        ];
      case 'helmet':
        return [
          { id: 'cooldownReduction', label: 'Cooldown', hint: 'Helmets can speed up the kit.' },
          { id: 'vampiric', label: 'Vampiric', hint: 'Helmets can sustain through life steal.' },
        ];
      case 'mount':
        return [{ id: 'moveSpeedPercent', label: 'Movement', hint: 'Mounts can grant a percentage bonus.' }];
      case 'ring':
        return [{ id: 'critChance', label: 'Critical chance', hint: 'Rings can increase critical frequency.' }];
      case 'necklace':
        return [{ id: 'elementAffinity', label: 'Elemental affinity', hint: 'Bonus when stance and item element match.' }];
      default:
        return [];
    }
  }

  bonusEnabled(id: ItemBonusId): boolean {
    return this.enabledBonuses()[id] ?? false;
  }

  toggleBonus(id: ItemBonusId): void {
    const item = this.draft();
    if (!item) return;
    const enabled = !this.bonusEnabled(id);
    this.enabledBonuses.update((state) => ({ ...state, [id]: enabled }));
    const next = enabled ? this.applyBonusDefault(item, id) : this.clearBonus(item, id);
    this.draft.set(next);
    this.status.set(null);
  }

  selectedResistanceElement(item: AdminItem): string {
    return ELEMENT_RESISTANCE_FIELDS.find((field) => Number(item[field.key]) > 0)?.element
      ?? this.selectedElementResistance();
  }

  selectedResistanceValue(item: AdminItem): number {
    const field = this.fieldForElement(this.selectedResistanceElement(item));
    return field ? Number(item[field.key]) || 0 : 0;
  }

  setElementResistance(element: string): void {
    const item = this.draft();
    if (!item) return;
    this.selectedElementResistance.set(element);
    const cleared = this.clearElementResistances(item);
    this.draft.set(this.bonusEnabled('elementResistance')
      ? this.applyBonusDefault(cleared, 'elementResistance')
      : cleared);
  }

  pct(value: number): number {
    return Math.round(value * 10000) / 100;
  }

  tierSummary(): string {
    const tier = this.draft()?.tier ?? 0;
    return tier === 0 ? 'T0 legacy/unlocked' : `Set T${tier}`;
  }

  summary(item: AdminItem): string {
    const label = SLOT_OPTIONS.find((slot) => slot.id === item.slot)?.label ?? item.subcategory;
    const values = [`T${item.tier}`, label];
    if (item.tag === 'relic') values.push(`Relic x${item.statMultiplier}`);
    if (item.attack) values.push(`Atk ${item.attack}`);
    if (item.armor) values.push(`Arm ${item.armor}`);
    if (item.defense) values.push(`Def ${item.defense}`);
    if (item.mountSpeed) values.push(`Mov ${item.mountSpeed}`);
    return values.join(' - ');
  }

  async save(): Promise<void> {
    const item = this.draft();
    if (!item) return;
    this.saving.set(true);
    this.status.set(null);
    try {
      const payload = this.applyRecommended(this.withSanitizedType(item));
      const saved = payload.itemId
        ? await this.api.updateAdminItem(payload)
        : await this.api.createAdminItem(payload);
      this.authored.update((items) => {
        const exists = items.some((entry) => entry.itemId === saved.itemId);
        return (exists
          ? items.map((entry) => entry.itemId === saved.itemId ? saved : entry)
          : [...items, saved]).sort((a, b) => a.name.localeCompare(b.name));
      });
      this.edit(saved);
      this.status.set({ kind: 'ok', message: 'Item saved and available in the catalog.' });
    } catch (error) {
      this.fail(error);
    } finally {
      this.saving.set(false);
    }
  }

  async remove(): Promise<void> {
    const item = this.draft();
    if (!item?.itemId) return;
    this.deleting.set(true);
    this.status.set(null);
    try {
      await this.api.deleteAdminItem(item.itemId);
      this.authored.update((items) => items.filter((entry) => entry.itemId !== item.itemId));
      this.draft.set(null);
      this.status.set({ kind: 'ok', message: 'Item deleted.' });
    } catch (error) {
      this.fail(error);
    } finally {
      this.deleting.set(false);
    }
  }

  async grant(): Promise<void> {
    const item = this.draft();
    if (!item?.itemId) return;
    this.granting.set(true);
    this.status.set(null);
    try {
      await this.api.grantAdminItem(item.itemId);
      const count = this.api.account()?.inventory
        .find((stack) => stack.itemId === item.itemId)?.count ?? 1;
      this.status.set({
        kind: 'ok',
        message: `${item.name} added to Backpack. Current quantity: ${count}.`,
      });
    } catch (error) {
      this.fail(error);
    } finally {
      this.granting.set(false);
    }
  }

  private seedBonusState(item: AdminItem): void {
    const enabled: Record<string, boolean> = {};
    enabled['critDamage'] = item.critDamage > 0;
    enabled['critChance'] = item.critChance > 0;
    enabled['vampiric'] = item.lifeStealChance > 0 || item.lifeStealAmount > 0;
    enabled['cooldownReduction'] = item.cooldownReduction > 0;
    enabled['moveSpeedPercent'] = item.moveSpeedPercent > 0;
    enabled['elementAffinity'] = item.elementDamage > 0;
    enabled['physicalResistance'] = item.physicalResistance > 0;
    enabled['elementResistance'] = ELEMENT_RESISTANCE_FIELDS.some((field) => Number(item[field.key]) > 0);
    this.enabledBonuses.set(enabled);
  }

  private applyRecommended(item: AdminItem): AdminItem {
    let next = this.withSanitizedType(item);
    next = {
      ...next,
      statMultiplier: this.normalizedMultiplier(next),
      salePrice: this.salePriceFor(next),
    };
    next = this.applyBaseDefault(next);
    for (const bonus of this.bonusControls(next)) {
      if (this.bonusEnabled(bonus.id))
        next = this.applyBonusDefault(next, bonus.id);
    }
    return next;
  }

  private applyBaseDefault(item: AdminItem): AdminItem {
    switch (item.slot) {
      case 'weapon':
        return { ...item, attack: this.recommendedValue(item, 'attack', true), armor: 0, defense: 0, mountSpeed: 0 };
      case 'armor':
      case 'helmet':
        return { ...item, attack: 0, armor: this.recommendedValue(item, 'armor', true), defense: 0, mountSpeed: 0 };
      case 'ring':
      case 'necklace':
        return { ...item, attack: 0, armor: 0, defense: this.recommendedValue(item, 'defense', true), mountSpeed: 0 };
      case 'mount':
        return { ...item, attack: 0, armor: 0, defense: 0, mountSpeed: this.recommendedValue(item, 'mountSpeed', true) };
      default:
        return item;
    }
  }

  private applyBonusDefault(item: AdminItem, id: ItemBonusId): AdminItem {
    switch (id) {
      case 'critDamage':
        return { ...item, critDamage: this.recommendedValue(item, 'critDamage', false) };
      case 'critChance':
        return { ...item, critChance: this.recommendedValue(item, 'critChance', false) };
      case 'vampiric':
        return {
          ...item,
          lifeStealChance: this.recommendedValue(item, 'lifeStealChance', false),
          lifeStealAmount: this.recommendedValue(item, 'lifeStealAmount', false),
        };
      case 'cooldownReduction':
        return { ...item, cooldownReduction: this.recommendedValue(item, 'cooldownReduction', false) };
      case 'moveSpeedPercent':
        return { ...item, moveSpeedPercent: this.recommendedValue(item, 'moveSpeedPercent', false) };
      case 'elementAffinity':
        return { ...item, elementDamage: this.recommendedValue(item, 'elementDamage', true) };
      case 'physicalResistance':
        return { ...item, physicalResistance: this.recommendedValue(item, 'resistance', false) };
      case 'elementResistance': {
        const field = this.fieldForElement(this.selectedElementResistance()) ?? ELEMENT_RESISTANCE_FIELDS[0];
        return { ...this.clearElementResistances(item), [field.key]: this.recommendedValue(item, 'resistance', false) };
      }
    }
  }

  private clearBonus(item: AdminItem, id: ItemBonusId): AdminItem {
    switch (id) {
      case 'critDamage': return { ...item, critDamage: 0 };
      case 'critChance': return { ...item, critChance: 0 };
      case 'vampiric': return { ...item, lifeStealChance: 0, lifeStealAmount: 0 };
      case 'cooldownReduction': return { ...item, cooldownReduction: 0 };
      case 'moveSpeedPercent': return { ...item, moveSpeedPercent: 0 };
      case 'elementAffinity': return { ...item, elementDamage: 0 };
      case 'physicalResistance': return { ...item, physicalResistance: 0 };
      case 'elementResistance': return this.clearElementResistances(item);
    }
  }

  private withSanitizedType(item: AdminItem): AdminItem {
    const slot = item.slot ?? 'weapon';
    const next: AdminItem = {
      ...item,
      slot,
      weaponType: slot === 'weapon' ? (item.weaponType || 'sword') : null,
      tag: item.tag === 'relic' ? 'relic' : 'normal',
      statMultiplier: item.tag === 'relic' ? item.statMultiplier : 1,
      skillPower: 0,
      requiredMasteryPoints: 0,
    };
    if (slot !== 'weapon') {
      next.attack = 0;
      next.critDamage = 0;
    }
    if (slot !== 'armor') {
      next.physicalResistance = 0;
      next.fireResistance = 0;
      next.iceResistance = 0;
      next.earthResistance = 0;
      next.energyResistance = 0;
      next.deathResistance = 0;
      next.holyResistance = 0;
    }
    if (slot !== 'helmet') {
      next.cooldownReduction = 0;
      next.lifeStealChance = 0;
      next.lifeStealAmount = 0;
    }
    if (slot !== 'ring') next.critChance = 0;
    if (slot !== 'necklace') next.elementDamage = 0;
    if (slot !== 'mount') {
      next.mountSpeed = 0;
      next.moveSpeedPercent = 0;
    }
    if (slot !== 'armor' && slot !== 'helmet') next.armor = 0;
    if (slot !== 'ring' && slot !== 'necklace') next.defense = 0;
    return next;
  }

  private clearElementResistances(item: AdminItem): AdminItem {
    return {
      ...item,
      fireResistance: 0,
      iceResistance: 0,
      earthResistance: 0,
      energyResistance: 0,
      deathResistance: 0,
      holyResistance: 0,
    };
  }

  private fieldForElement(element: string): { key: PercentField; element: string; label: string } | undefined {
    return ELEMENT_RESISTANCE_FIELDS.find((field) => field.element === element);
  }

  private recommendedValue(item: AdminItem, stat: string, integer: boolean): number {
    const range = this.balance()?.ranges.find((entry) =>
      entry.stat === stat && entry.tier === item.tier);
    if (!range) return 0;
    const value = ((range.moderateMin + range.moderateMax) / 2) * this.normalizedMultiplier(item);
    return integer ? Math.round(value) : Math.round(value * 10000) / 10000;
  }

  private normalizedMultiplier(item: AdminItem): number {
    if (item.tag !== 'relic') return 1;
    const config = this.balance();
    const min = config?.relicMultiplierMin ?? 1.05;
    const max = config?.relicMultiplierMax ?? 1.6;
    return Math.min(max, Math.max(min, item.statMultiplier || config?.relicMultiplierDefault || 1.25));
  }

  private salePriceFor(item: AdminItem): number {
    const tier = Math.min(5, Math.max(0, Math.floor(item.tier || 0)));
    const base = tier <= 0 ? 80 : 80 * tier * tier;
    return Math.round(base * this.normalizedMultiplier(item));
  }

  private patch(field: keyof AdminItem, value: AdminItem[keyof AdminItem]): void {
    const item = this.draft();
    if (item) this.draft.set({ ...item, [field]: value });
    this.status.set(null);
  }

  private fail(error: unknown): void {
    this.status.set({ kind: 'error', message: (error as Error).message });
  }
}
