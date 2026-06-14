import { Component, OnInit, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { ItemIcon } from '../../core/item-icon';
import { AdminItem } from '../../core/types';

type NumericField = keyof Pick<AdminItem,
  'salePrice' | 'attack' | 'armor' | 'defense' | 'mountSpeed' | 'elementDamage'
  | 'skillPower' | 'requiredMasteryPoints'>;
type PercentField = keyof Pick<AdminItem,
  'critChance' | 'critDamage' | 'lifeStealChance' | 'lifeStealAmount'
  | 'cooldownReduction' | 'moveSpeedPercent' | 'physicalResistance'
  | 'fireResistance' | 'iceResistance' | 'earthResistance'
  | 'energyResistance' | 'deathResistance' | 'holyResistance'>;

@Component({
  selector: 'app-item-editor',
  standalone: true,
  imports: [ItemIcon],
  template: `
    <div class="studio">
      <aside class="panel library">
        <header>
          <div><span class="eyebrow">Acervo visual</span><h2>Biblioteca Canary</h2></div>
          <b>{{ filteredLibrary().length }}</b>
        </header>
        <input type="search" placeholder="Nome ou item id" [value]="search()"
          (input)="setSearch($any($event.target).value)" />
        <div class="filters">
          <select [value]="category()" (change)="setCategory($any($event.target).value)">
            <option value="">Todas as categorias</option>
            @for (value of categories(); track value) { <option [value]="value">{{ value }}</option> }
          </select>
          <select [value]="subcategory()" (change)="setSubcategory($any($event.target).value)">
            <option value="">Todos os tipos</option>
            @for (value of subcategories(); track value) { <option [value]="value">{{ value }}</option> }
          </select>
        </div>
        <div class="scroll">
          @for (item of visibleLibrary(); track item.itemId) {
            <button class="source" type="button"
              [class.active]="draft()?.sourceItemId === item.itemId" (click)="createFrom(item)">
              <app-item-icon [itemId]="item.itemId" [size]="38" />
              <span><strong>{{ item.name }}</strong><small>#{{ item.itemId }} · {{ item.subcategory }}</small></span>
            </button>
          } @empty { <p class="empty">Nenhum item encontrado.</p> }
          @if (visibleLibrary().length < filteredLibrary().length) {
            <button type="button" class="more" (click)="showMore()">
              Mostrar mais ({{ filteredLibrary().length - visibleLibrary().length }})
            </button>
          }
        </div>
      </aside>

      <main class="panel editor">
        <header>
          <div>
            <span class="eyebrow">Item Studio</span>
            <h2>{{ draft()?.itemId ? 'Editar item' : 'Novo item' }}</h2>
          </div>
          <div class="head-actions">
            @if (draft()?.itemId) { <button type="button" (click)="duplicate()">Duplicar</button> }
            <button type="button" class="primary" [disabled]="busy() || !draft()" (click)="save()">
              {{ saving() ? 'Salvando...' : 'Salvar item' }}
            </button>
          </div>
        </header>

        @if (status(); as value) {
          <div class="status" [class.error]="value.kind === 'error'">{{ value.message }}</div>
        }

        @if (draft(); as item) {
          <section class="source-preview">
            <app-item-icon [itemId]="item.sourceItemId" [size]="72" />
            <div>
              <span class="eyebrow">Base Canary</span>
              <strong>{{ sourceName() }}</strong>
              <small>#{{ item.sourceItemId }} · {{ item.slot || 'consumivel/material' }}
                @if (item.weaponType) { · {{ item.weaponType }} }</small>
            </div>
          </section>

          <section>
            <h3>Identidade</h3>
            <div class="grid two">
              <label>Nome
                <input [value]="item.name" (input)="patchText('name', $any($event.target).value)" />
              </label>
              <label>Preco de venda
                <input type="number" min="0" [value]="item.salePrice"
                  (input)="patchNumber('salePrice', $any($event.target).value)" />
              </label>
            </div>
            <label>Descricao
              <textarea rows="2" [value]="item.description"
                (input)="patchText('description', $any($event.target).value)"></textarea>
            </label>
          </section>

          @if (item.capabilities.attack || item.capabilities.armor
            || item.capabilities.defense || item.capabilities.mountSpeed) {
            <section>
              <h3>Atributos base</h3>
              <div class="grid four">
                @if (item.capabilities.attack) {
                  <label>Ataque<input type="number" min="0" [value]="item.attack"
                    (input)="patchNumber('attack', $any($event.target).value)" /></label>
                }
                @if (item.capabilities.armor) {
                  <label>Armadura<input type="number" min="0" [value]="item.armor"
                    (input)="patchNumber('armor', $any($event.target).value)" /></label>
                }
                @if (item.capabilities.defense) {
                  <label>Defesa<input type="number" min="0" [value]="item.defense"
                    (input)="patchNumber('defense', $any($event.target).value)" /></label>
                }
                @if (item.capabilities.mountSpeed) {
                  <label>Velocidade da montaria<input type="number" min="0" [value]="item.mountSpeed"
                    (input)="patchNumber('mountSpeed', $any($event.target).value)" /></label>
                }
              </div>
            </section>
          }

          @if (item.capabilities.offense) {
            <section>
              <h3>Ofensiva avancada</h3>
              <div class="grid four">
                <label>Elemento
                  <select [value]="item.element" (change)="patchText('element', $any($event.target).value)">
                    @for (element of elements(); track element) { <option [value]="element">{{ element }}</option> }
                  </select>
                </label>
                <label>Dano elemental<input type="number" min="0" [value]="item.elementDamage"
                  (input)="patchNumber('elementDamage', $any($event.target).value)" /></label>
                <label>Poder de skill<input type="number" min="0" [value]="item.skillPower"
                  (input)="patchNumber('skillPower', $any($event.target).value)" /></label>
                <label>Chance de critico (%)<input type="number" min="0" step="0.1" [value]="pct(item.critChance)"
                  (input)="patchPercent('critChance', $any($event.target).value)" /></label>
                <label>Dano critico extra (%)<input type="number" min="0" step="1" [value]="pct(item.critDamage)"
                  (input)="patchPercent('critDamage', $any($event.target).value)" /></label>
                <label>Chance de roubo de vida (%)<input type="number" min="0" step="0.1"
                  [value]="pct(item.lifeStealChance)"
                  (input)="patchPercent('lifeStealChance', $any($event.target).value)" /></label>
                <label>Vida roubada (%)<input type="number" min="0" step="0.1"
                  [value]="pct(item.lifeStealAmount)"
                  (input)="patchPercent('lifeStealAmount', $any($event.target).value)" /></label>
              </div>
            </section>
          }

          @if (item.capabilities.support) {
            <section>
              <h3>Suporte</h3>
              <div class="grid four">
                <label>Reducao de recarga (%)<input type="number" min="0" step="0.1"
                  [value]="pct(item.cooldownReduction)"
                  (input)="patchPercent('cooldownReduction', $any($event.target).value)" /></label>
                <label>Velocidade de movimento (%)<input type="number" min="0" step="0.1"
                  [value]="pct(item.moveSpeedPercent)"
                  (input)="patchPercent('moveSpeedPercent', $any($event.target).value)" /></label>
              </div>
            </section>
          }

          @if (item.capabilities.resistance) {
            <section>
              <h3>Resistencias (%)</h3>
              <div class="grid seven">
                @for (resistance of resistanceFields; track resistance.key) {
                  <label>{{ resistance.label }}<input type="number" min="0" step="1"
                    [value]="pct(percentValue(item, resistance.key))"
                    (input)="patchPercent(resistance.key, $any($event.target).value)" /></label>
                }
              </div>
            </section>
          }

          @if (item.slot) {
            <section>
              <h3>Restricoes de uso</h3>
              <div class="restrictions">
                <div>
                  <span class="field-title">Classes permitidas</span>
                  <div class="checks">
                    @for (klass of classes(); track klass.id) {
                      <label class="check"><input type="checkbox"
                        [checked]="item.allowedClassIds.includes(klass.id)"
                        (change)="toggleClass(klass.id)" />{{ klass.name }}</label>
                    }
                  </div>
                </div>
                <label>Pontos totais de maestria
                  <input type="number" min="0" [value]="item.requiredMasteryPoints"
                    (input)="patchNumber('requiredMasteryPoints', $any($event.target).value)" />
                </label>
              </div>
              <p class="hint">A validacao acontece no backend ao equipar. Respec nao remove acesso ja conquistado.</p>
            </section>
          } @else {
            <p class="hint">Itens sem slot permitem apenas identidade e preco de venda.</p>
          }
        } @else {
          <p class="empty large">Escolha um item do Canary para criar uma versao Kaezan.</p>
        }
      </main>

      <aside class="panel authored">
        <header>
          <div><span class="eyebrow">Conteudo autoral</span><h2>Itens Kaezan</h2></div>
          <button type="button" class="primary" (click)="newItem()">Novo</button>
        </header>
        <input type="search" placeholder="Buscar item criado" [value]="authoredSearch()"
          (input)="authoredSearch.set($any($event.target).value)" />
        <div class="scroll">
          @for (item of filteredAuthored(); track item.itemId) {
            <button class="source authored-card" type="button"
              [class.active]="draft()?.itemId === item.itemId" (click)="edit(item)">
              <app-item-icon [itemId]="item.itemId" [size]="42" />
              <span><strong>{{ item.name }}</strong><small>#{{ item.itemId }} · {{ summary(item) }}</small></span>
            </button>
          } @empty { <p class="empty">Nenhum item autoral. Escolha uma base e salve o primeiro.</p> }
        </div>
        @if (draft()?.itemId) {
          <button type="button" class="grant" [disabled]="busy()" (click)="grant()">
            Adicionar 1 a Mochila
          </button>
          <button type="button" class="danger" [disabled]="busy()" (click)="remove()">Excluir item</button>
        }
      </aside>
    </div>
  `,
  styles: [`
    :host { display:block } .studio{display:grid;grid-template-columns:280px minmax(500px,1fr)280px;gap:12px;align-items:start}
    .panel{background:#0c0c14;border:1px solid #222232;border-radius:8px;padding:13px;min-width:0}
    .library,.authored{height:calc(100vh - 84px);position:sticky;top:68px;display:flex;flex-direction:column;box-sizing:border-box}
    header{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-bottom:10px}h2{font-size:17px;margin:1px 0 0}
    h3{font-size:10px;text-transform:uppercase;letter-spacing:.8px;color:#aaa7b8;margin:0 0 10px}
    .eyebrow{font-size:8px;text-transform:uppercase;letter-spacing:1.1px;color:#35d3bf;font-weight:900}
    header b{font-size:9px;color:#61dfcf;background:#17342f;border:1px solid #2c756a;border-radius:4px;padding:4px 6px}
    input,select,textarea{box-sizing:border-box;width:100%;background:#0c0c15;border:1px solid #303043;border-radius:5px;color:#eeeaf5;font:inherit;padding:8px;outline:none}
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
    .source-preview{display:flex;align-items:center;gap:12px}.source-preview strong,.source-preview small{display:block}.source-preview strong{font-size:14px}.source-preview small{font-size:9px;color:#858296;margin-top:3px}
    label{display:flex;flex-direction:column;gap:5px;color:#9996aa;font-size:8px;font-weight:800}.grid{display:grid;gap:8px}.two{grid-template-columns:2fr 1fr}.four{grid-template-columns:repeat(4,1fr)}.seven{grid-template-columns:repeat(4,1fr)}
    section>label{margin-top:8px}.head-actions{display:flex;gap:6px}.head-actions button:not(.primary){padding:7px 10px;font-size:9px}
    .restrictions{display:grid;grid-template-columns:1fr 180px;gap:14px}.field-title{display:block;color:#9996aa;font-size:8px;font-weight:800;margin-bottom:8px}
    .checks{display:flex;flex-wrap:wrap;gap:6px}.check{display:flex;flex-direction:row;align-items:center;background:#151520;border:1px solid #2b2b3b;border-radius:4px;padding:7px 9px}.check input{width:auto}
    .hint,.empty{color:#777487;font-size:9px;line-height:1.5;text-align:center}.large{padding:80px 20px}.status{padding:8px 10px;border:1px solid #28766a;background:#12302b;color:#65e6d4;border-radius:5px;font-size:9px}.status.error{border-color:#773641;background:#32181d;color:#ff9ca6}
    .grant,.danger{margin-top:8px;padding:9px;font-size:9px;font-weight:900}.grant{background:#16322d;border-color:#2b796d;color:#63e3d1}.danger{background:#2d171d;border-color:#6e303d;color:#ff9aa6}.authored-card{grid-template-columns:46px minmax(0,1fr)}
    @media(max-width:1200px){.studio{grid-template-columns:230px minmax(460px,1fr)240px}.four,.seven{grid-template-columns:repeat(2,1fr)}}
    @media(max-width:900px){.studio{grid-template-columns:1fr}.library,.authored{height:420px;position:static}.editor{grid-row:2}.two,.restrictions{grid-template-columns:1fr}}
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

  readonly resistanceFields: { key: PercentField; label: string }[] = [
    { key: 'physicalResistance', label: 'Fisico' },
    { key: 'fireResistance', label: 'Fogo' },
    { key: 'iceResistance', label: 'Gelo' },
    { key: 'earthResistance', label: 'Terra' },
    { key: 'energyResistance', label: 'Energia' },
    { key: 'deathResistance', label: 'Morte' },
    { key: 'holyResistance', label: 'Sagrado' },
  ];

  readonly busy = computed(() => this.saving() || this.deleting() || this.granting());
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
    this.draft.set({
      ...source,
      itemId: 0,
      sourceItemId: source.itemId,
      appearanceItemId: source.itemId,
      isAuthored: true,
      name: source.name,
      description: '',
      allowedClassIds: [...source.allowedClassIds],
    });
    this.status.set(null);
  }

  newItem(): void {
    const source = this.filteredLibrary()[0] ?? this.library()[0];
    if (source) this.createFrom(source);
  }

  edit(item: AdminItem): void {
    this.draft.set({ ...item, allowedClassIds: [...item.allowedClassIds] });
    this.status.set(null);
  }

  duplicate(): void {
    const item = this.draft();
    if (!item) return;
    this.draft.set({
      ...item,
      itemId: 0,
      name: `${item.name} (copia)`,
      allowedClassIds: [...item.allowedClassIds],
    });
    this.status.set(null);
  }

  patchText(field: 'name' | 'description' | 'element', value: string): void {
    this.patch(field, value);
  }

  patchNumber(field: NumericField, value: string): void {
    this.patch(field, Math.max(0, Math.floor(Number(value) || 0)));
  }

  patchPercent(field: PercentField, value: string): void {
    this.patch(field, Math.max(0, Number(value) || 0) / 100);
  }

  percentValue(item: AdminItem, field: PercentField): number {
    return item[field];
  }

  pct(value: number): number {
    return Math.round(value * 10000) / 100;
  }

  toggleClass(classId: string): void {
    const item = this.draft();
    if (!item) return;
    const allowed = item.allowedClassIds.includes(classId)
      ? item.allowedClassIds.filter((id) => id !== classId)
      : [...item.allowedClassIds, classId];
    this.draft.set({ ...item, allowedClassIds: allowed });
  }

  summary(item: AdminItem): string {
    const values: string[] = [];
    if (item.attack) values.push(`Atk ${item.attack}`);
    if (item.armor) values.push(`Arm ${item.armor}`);
    if (item.defense) values.push(`Def ${item.defense}`);
    if (!values.length) values.push(item.subcategory);
    return values.join(' · ');
  }

  async save(): Promise<void> {
    const item = this.draft();
    if (!item) return;
    this.saving.set(true);
    this.status.set(null);
    try {
      const saved = item.itemId
        ? await this.api.updateAdminItem(item)
        : await this.api.createAdminItem(item);
      this.authored.update((items) => {
        const exists = items.some((entry) => entry.itemId === saved.itemId);
        return (exists
          ? items.map((entry) => entry.itemId === saved.itemId ? saved : entry)
          : [...items, saved]).sort((a, b) => a.name.localeCompare(b.name));
      });
      this.edit(saved);
      this.status.set({ kind: 'ok', message: 'Item salvo e disponivel no catalogo.' });
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
      this.status.set({ kind: 'ok', message: 'Item excluido.' });
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
        message: `${item.name} adicionado a Mochila. Quantidade atual: ${count}.`,
      });
    } catch (error) {
      this.fail(error);
    } finally {
      this.granting.set(false);
    }
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
