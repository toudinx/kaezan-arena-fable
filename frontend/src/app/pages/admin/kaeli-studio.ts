import { Component, OnInit, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { OutfitPreview } from '../../core/outfit-preview';
import { OutfitThumb } from '../../core/outfit-thumb';
import {
  ItemCatalogEntry,
  KaeliAuthoringMetadata,
  KaeliSkinDefinition,
  MonsterAppearance,
  WaifuDef,
} from '../../core/types';

type Region = 'head' | 'body' | 'legs' | 'feet';
type OutfitCategory = 'feminino' | 'masculino' | 'monster' | 'boss' | 'all';

/** Entrada de outfit-catalog.json (gerado de Canary outfits.xml: nome + gênero por lookType). */
interface OutfitCatalogEntry {
  lookType: number;
  name: string;
  gender: 'female' | 'male';
}

interface OutfitOption {
  lookType: number;
  name: string;
  recolorable: boolean;
  category: 'feminino' | 'masculino' | 'monster' | 'boss' | 'other';
  bestiaryClass: string;
}

const PAGE_SIZE = 48;

/** Outfit Studio: cria skins autorais (visual recolorível + addons + montaria) para as Kaelis. */
@Component({
  selector: 'app-kaeli-studio',
  standalone: true,
  imports: [OutfitPreview, OutfitThumb],
  template: `
    @if (metadata(); as meta) {
      <div class="studio">
        <!-- LEFT: biblioteca de outfits (só visuais — montaria fica no estúdio) -->
        <aside class="library panel">
          <div class="panel-head">
            <div>
              <span class="eyebrow">Acervo visual</span>
              <h2>Biblioteca</h2>
            </div>
            <b class="count">{{ filteredOutfits().length }}</b>
          </div>

          <input class="search" type="search" placeholder="Nome, classe ou lookType"
            [value]="outfitSearch()" (input)="setOutfitSearch($any($event.target).value)" />
          <select class="search cat-select" (change)="setOutfitCategory($any($event.target).value)">
            <option value="feminino" [selected]="outfitCategory() === 'feminino'">Feminino ({{ categoryCounts().feminino }})</option>
            <option value="masculino" [selected]="outfitCategory() === 'masculino'">Masculino ({{ categoryCounts().masculino }})</option>
            <option value="monster" [selected]="outfitCategory() === 'monster'">Monstros ({{ categoryCounts().monster }})</option>
            <option value="boss" [selected]="outfitCategory() === 'boss'">Bosses ({{ categoryCounts().boss }})</option>
            <option value="all" [selected]="outfitCategory() === 'all'">Todos ({{ categoryCounts().all }})</option>
          </select>

          <div class="lib-list">
            @for (o of pagedOutfits(); track o.lookType) {
              <button type="button" class="lib-row" [class.active]="draft().lookType === o.lookType"
                (click)="chooseOutfit(o.lookType)">
                <app-outfit-thumb [lookType]="o.lookType" [size]="46" />
                <span class="row-copy">
                  <strong [title]="o.name">{{ o.name }}</strong>
                  <small>LookType {{ o.lookType }}{{ o.bestiaryClass ? ' · ' + o.bestiaryClass : '' }}</small>
                  <span class="row-tags">
                    <i [class]="o.category">{{ catLabel(o.category) }}</i>
                  </span>
                </span>
              </button>
            } @empty {
              <div class="empty">Nenhum outfit encontrado.</div>
            }
          </div>
          <footer class="pagination">
            <button type="button" [disabled]="outfitPage() <= 1" (click)="changeOutfitPage(-1)">Anterior</button>
            <span>{{ outfitPage() }} / {{ outfitPageCount() }}</span>
            <button type="button" [disabled]="outfitPage() >= outfitPageCount()" (click)="changeOutfitPage(1)">Próxima</button>
          </footer>
        </aside>

        <!-- CENTER: estúdio -->
        <main class="editor panel">
          <div class="editor-head">
            <div>
              <span class="eyebrow">Outfit Studio</span>
              <h2>{{ draft().id ? 'Editar skin' : 'Nova skin' }}</h2>
              @if (draft().id) { <code>{{ draft().id }}</code> }
            </div>
            <div class="actions">
              <button type="button" class="secondary" (click)="duplicateCurrent()">Duplicar</button>
              <button type="button" class="primary" [disabled]="saving()" (click)="save()">
                {{ saving() ? 'Salvando...' : 'Salvar look' }}
              </button>
            </div>
          </div>

          @if (status(); as state) {
            <div class="status" [class.ok]="state.kind === 'ok'" [class.err]="state.kind === 'err'">{{ state.msg }}</div>
          }

          <div class="stage">
            <div class="preview-wrap">
              <div class="preview">
                @if (draft().lookType > 0) {
                  <app-outfit-preview
                    [lookType]="draft().lookType"
                    [head]="draft().head" [body]="draft().body" [legs]="draft().legs" [feet]="draft().feet"
                    [addons]="draft().addons" [mountLookType]="draft().mountLookType"
                    [animate]="walk()" [size]="168" />
                } @else {
                  <span class="no-outfit">Escolha um outfit na biblioteca</span>
                }
              </div>
              <label class="check center">
                <input type="checkbox" [checked]="walk()" (change)="walk.set($any($event.target).checked)" />
                Andar / girar
              </label>
            </div>

            <div class="controls">
              <section class="block addons-mount">
                <div>
                  <h3>Addons</h3>
                  <div class="addons">
                    <label class="check"><input type="checkbox" [checked]="hasAddon(1)" (change)="toggleAddon(1)" /> Addon 1</label>
                    <label class="check"><input type="checkbox" [checked]="hasAddon(2)" (change)="toggleAddon(2)" /> Addon 2</label>
                  </div>
                </div>
                <div>
                  <h3>Montaria</h3>
                  <select (change)="setMount(+$any($event.target).value)">
                    <option value="0" [selected]="draft().mountLookType === 0">Sem montaria</option>
                    @for (m of mounts(); track m.itemId) {
                      <option [value]="m.mountLookType" [selected]="draft().mountLookType === m.mountLookType">{{ m.name }}</option>
                    }
                  </select>
                </div>
              </section>

              <section class="block">
                <h3>Região</h3>
                <div class="regions">
                  @for (r of regions; track r.key) {
                    <button type="button" [class.active]="region() === r.key" (click)="region.set(r.key)">
                      <span class="dot" [style.background]="cssColor(draft()[r.key])"></span>{{ r.label }}
                    </button>
                  }
                </div>
                <div class="palette">
                  @for (c of colorSwatches(); track $index) {
                    <button type="button" class="swatch" [class.active]="draft()[region()] === $index"
                      [style.background]="c" [title]="'Cor ' + $index" (click)="pickColor($index)"></button>
                  }
                </div>
              </section>
            </div>
          </div>

          <section class="block identity">
            <h3>Identidade</h3>
            <div class="id-grid">
              <label>Kaeli
                <select (change)="setWaifu($any($event.target).value)">
                  @for (w of roster(); track w.id) {
                    <option [value]="w.id" [selected]="w.id === draft().waifuId">{{ w.name }} ({{ w.rarity }}★)</option>
                  }
                </select>
              </label>
              <label>Nome da skin
                <input [value]="draft().name" (input)="patch({ name: $any($event.target).value })" placeholder="Ex.: Aurora Boreal" />
              </label>
              <button type="button" class="secondary load-default" (click)="loadKaeliDefault()">
                Carregar visual padrão da Kaeli
              </button>
            </div>
            <label>Descrição
              <textarea rows="2" [value]="draft().description"
                (input)="patch({ description: $any($event.target).value })"></textarea>
            </label>
            <div class="unlock-grid">
              <label>Desbloqueio
                <select (change)="patch({ unlock: $any($event.target).value })">
                  @for (k of unlockKinds(); track k) {
                    <option [value]="k" [selected]="k === draft().unlock">{{ unlockLabel(k) }}</option>
                  }
                </select>
              </label>
              @if (draft().unlock !== 'default') {
                <label>{{ unlockValueLabel() }}
                  <input type="number" min="0" [value]="draft().unlockValue"
                    (input)="patch({ unlockValue: clampValue($any($event.target).value) })" />
                </label>
              }
            </div>
            <p class="hint">
              Skins com desbloqueio “Padrão” já podem ser equipadas; as demais exigem afinidade, ouro ou Kaeros.
              Addons e montaria definidos aqui sobrescrevem ascensão/equipamento dentro da run.
            </p>
          </section>
        </main>

        <!-- RIGHT: skins autorais -->
        <aside class="authored panel">
          <div class="panel-head">
            <div>
              <span class="eyebrow">Conteúdo autoral</span>
              <h2>Skins Kaezan</h2>
            </div>
            <button class="primary compact" type="button" (click)="newSkin()">Nova</button>
          </div>

          <input class="search" type="search" placeholder="Buscar skin"
            [value]="authoredSearch()" (input)="authoredSearch.set($any($event.target).value)" />
          <label class="filter-label">Kaeli
            <select (change)="authoredWaifuFilter.set($any($event.target).value)">
              <option value="all" [selected]="authoredWaifuFilter() === 'all'">Todas</option>
              @for (w of roster(); track w.id) {
                <option [value]="w.id" [selected]="authoredWaifuFilter() === w.id">{{ w.name }}</option>
              }
            </select>
          </label>

          <div class="authored-list">
            @for (s of filteredAuthored(); track s.id) {
              <article class="authored-card" [class.active]="selectedId() === s.id">
                <button type="button" class="authored-main" (click)="editSkin(s)">
                  <app-outfit-thumb [lookType]="s.lookType" [head]="s.head" [body]="s.body"
                    [legs]="s.legs" [feet]="s.feet" [addons]="s.addons" [mountLookType]="s.mountLookType" [size]="48" />
                  <span>
                    <strong [title]="s.name">{{ s.name }}</strong>
                    <small>{{ waifuName(s.waifuId) }}</small>
                    <i [class]="s.unlock">{{ unlockLabel(s.unlock) }}</i>
                  </span>
                </button>
                <footer>
                  <button type="button" (click)="editSkin(s)">Editar</button>
                  <button type="button" (click)="duplicateSkin(s)">Duplicar</button>
                  <button type="button" class="danger" (click)="requestDelete(s)">Excluir</button>
                </footer>
              </article>
            } @empty {
              <div class="empty authored-empty">
                Nenhuma skin autoral ainda. Escolha um outfit, ajuste as cores e salve a primeira.
              </div>
            }
          </div>
        </aside>

        @if (pendingDelete(); as skin) {
          <div class="modal-backdrop" (click)="cancelDelete()">
            <section class="confirm-modal" (click)="$event.stopPropagation()">
              <span class="eyebrow">Excluir skin</span>
              <h2>{{ skin.name }}</h2>
              <p>Remove definitivamente <code>{{ skin.id }}</code> de {{ waifuName(skin.waifuId) }}.</p>
              <div class="actions">
                <button type="button" class="secondary" (click)="cancelDelete()">Cancelar</button>
                <button type="button" class="danger solid" [disabled]="deleting()" (click)="confirmDelete()">
                  {{ deleting() ? 'Excluindo...' : 'Excluir' }}
                </button>
              </div>
            </section>
          </div>
        }
      </div>
    } @else {
      <div class="loading">Carregando Outfit Studio...</div>
    }
  `,
  styles: [`
    :host { display: block; }
    .studio { display: grid; grid-template-columns: 300px minmax(520px, 1fr) 300px; gap: 14px; align-items: start; }
    .panel { border-radius: 8px; min-width: 0; padding: 14px; background: #0c0c14; border: 1px solid #20202e; }
    .library, .authored { box-sizing: border-box; display: flex; flex-direction: column; height: calc(100vh - 82px); overflow: hidden; position: sticky; top: 68px; }
    .panel-head, .editor-head { align-items: center; display: flex; gap: 10px; justify-content: space-between; }
    h2, h3, p { margin: 0; } h2 { font-size: 20px; }
    h3 { color: #cfccd9; font-size: 11px; letter-spacing: .6px; margin-bottom: 9px; text-transform: uppercase; }
    .eyebrow { color: #2dd4bf; font-size: 9px; font-weight: 900; letter-spacing: 1.2px; text-transform: uppercase; }
    .count { background: #192d2a; border: 1px solid #2d6b63; border-radius: 4px; color: #58daca; font-size: 10px; padding: 5px 7px; }
    button { border: 1px solid transparent; border-radius: 5px; color: #dddbe8; font: inherit; cursor: pointer; }
    button:disabled { cursor: default; opacity: .45; }
    .primary, .secondary { min-height: 34px; padding: 0 13px; font-size: 10px; font-weight: 900; }
    .primary { background: #1db9aa; color: #061d1a; } .secondary { background: #1b1b28; border-color: #343448; }
    .compact { min-height: 31px; padding: 0 10px; }
    .actions { display: flex; gap: 7px; }
    input, select, textarea { background: #0d0d15; border: 1px solid #303043; border-radius: 5px; color: #eceaf3; font: inherit; outline: none; box-sizing: border-box; width: 100%; }
    input, select { min-height: 36px; padding: 0 9px; } textarea { padding: 8px 9px; resize: vertical; }
    input:focus, select:focus, textarea:focus { border-color: #2ab5a5; }
    label { color: #8e8ca0; display: flex; flex-direction: column; gap: 5px; font-size: 9px; font-weight: 800; }
    .search { margin: 12px 0 8px; }
    .lib-list, .authored-list { align-content: start; display: grid; flex: 1 1 auto; gap: 5px; min-height: 0; overflow-y: auto; padding-right: 3px; }
    .lib-row { align-items: center; background: #11111a; border-color: #29293a; display: grid; gap: 6px; grid-template-columns: 48px minmax(0, 1fr); min-width: 0; padding: 2px 5px 2px 0; text-align: left; }
    .lib-row:hover, .lib-row.active { background: #17201f; border-color: #2f8e82; }
    .row-copy, .row-copy strong, .row-copy small { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .row-copy strong { font-size: 10px; } .row-copy small { color: #858297; font-size: 8px; margin-top: 2px; }
    .cat-select { margin-top: 0; }
    .row-tags { display: flex; gap: 3px; margin-top: 4px; }
    .row-tags i { border-radius: 3px; font-size: 7px; font-style: normal; font-weight: 900; padding: 2px 4px; }
    .row-tags i.feminino { background: #3a1f33; color: #f0a4d4; }
    .row-tags i.masculino { background: #1b2a3b; color: #74c0e8; }
    .row-tags i.monster { background: #2a2438; color: #b79cf0; }
    .row-tags i.boss { background: #3b2818; color: #efad69; }
    .row-tags i.other { background: #26262f; color: #a8a6b8; }
    .row-tags i.warn { background: #3b2025; color: #f0a4ad; }
    .pagination { align-items: center; border-top: 1px solid #29293a; display: flex; justify-content: space-between; margin-top: 8px; padding-top: 8px; }
    .pagination button { background: #171722; border-color: #303043; font-size: 8px; min-height: 27px; padding: 0 8px; }
    .pagination span { color: #77758b; font-size: 8px; }
    .editor { min-width: 0; }
    .editor-head { border-bottom: 1px solid #29293a; padding-bottom: 12px; }
    code { color: #858297; font-size: 9px; } .editor-head code { display: block; margin-top: 3px; }
    .status { border: 1px solid; border-radius: 5px; font-size: 10px; margin-top: 10px; padding: 8px 10px; }
    .status.ok { background: #102a25; border-color: #22675d; color: #55e5cf; }
    .status.err { background: #32191e; border-color: #6d303b; color: #ff9aa5; }
    .stage { display: grid; gap: 14px; grid-template-columns: 200px minmax(0, 1fr); margin-top: 12px; }
    .preview-wrap { display: grid; gap: 8px; }
    .preview { align-items: center; background: radial-gradient(circle, #2b2b3c, #10101a 68%); border: 1px solid #343448; border-radius: 8px; display: grid; min-height: 200px; place-items: center; }
    .no-outfit { color: #77758c; font-size: 10px; padding: 0 16px; text-align: center; }
    .check { align-items: center; color: #b9b7c8; flex-direction: row; font-size: 10px; font-weight: 700; gap: 6px; }
    .check input { min-height: 0; width: auto; }
    .check.center { justify-content: center; }
    .block { background: #11111a; border: 1px solid #29293a; border-radius: 6px; padding: 12px; }
    .controls { display: grid; gap: 10px; }
    .addons-mount { display: grid; gap: 12px; grid-template-columns: 1fr 1fr; align-items: start; }
    .addons { display: flex; gap: 16px; }
    .regions { display: grid; gap: 5px; grid-template-columns: repeat(4, 1fr); margin-bottom: 10px; }
    .regions button { align-items: center; background: #161620; border: 1px solid #2c2c3e; color: #9b99ad; display: flex; flex-direction: column; font-size: 8px; font-weight: 900; gap: 4px; min-height: 40px; padding: 5px 2px; }
    .regions button.active { background: #183933; border-color: #2db7a5; color: #64ead6; }
    .dot { border: 1px solid #00000066; border-radius: 50%; height: 14px; width: 14px; }
    .palette { display: grid; gap: 2px; grid-template-columns: repeat(19, 1fr); }
    .swatch { border: 1px solid #00000044; border-radius: 2px; height: 16px; padding: 0; }
    .swatch:hover { outline: 1px solid #ffffff66; }
    .swatch.active { box-shadow: 0 0 0 2px #2dd4bf, 0 0 0 3px #000; position: relative; z-index: 1; }
    .identity { margin-top: 12px; }
    .id-grid { align-items: end; display: grid; gap: 8px; grid-template-columns: 1fr 1fr; }
    .id-grid .load-default { grid-column: 1 / -1; }
    .identity > label { margin-top: 8px; }
    .unlock-grid { display: grid; gap: 8px; grid-template-columns: 1fr 1fr; margin-top: 8px; }
    .hint { color: #77758b; font-size: 8px; line-height: 1.5; margin-top: 8px; }
    .authored > .filter-label { margin-bottom: 8px; }
    .authored-card { background: #11111a; border: 1px solid #29293a; border-radius: 6px; overflow: hidden; }
    .authored-card.active { background: #17201f; border-color: #2f8e82; }
    .authored-main { align-items: center; background: transparent; border: 0; border-radius: 0; display: grid; gap: 6px; grid-template-columns: 48px minmax(0, 1fr); padding: 3px 6px 3px 0; text-align: left; width: 100%; }
    .authored-main span, .authored-main strong, .authored-main small { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .authored-main strong { font-size: 10px; } .authored-main small { color: #858297; font-size: 8px; margin: 3px 0 4px; }
    .authored-main i { border-radius: 3px; font-size: 7px; font-style: normal; font-weight: 900; padding: 2px 4px; background: #173b35; color: #58dbc9; }
    .authored-main i.gold { background: #3b2818; color: #efad69; } .authored-main i.kaeros { background: #2a2040; color: #b79cf0; } .authored-main i.affinity { background: #20303b; color: #74c7e8; }
    .authored-card footer { border-top: 1px solid #29293a; display: grid; grid-template-columns: repeat(3, 1fr); }
    .authored-card footer button { background: #171722; border: 0; border-radius: 0; color: #9a97aa; font-size: 8px; min-height: 27px; }
    .authored-card footer button + button { border-left: 1px solid #29293a; }
    .authored-card footer button:hover { color: #eceaf3; }
    .danger { color: #ef8b98 !important; }
    .danger.solid { background: #7b303b; border-color: #a84957; color: #fff0f2 !important; min-height: 34px; padding: 0 12px; }
    .empty { color: #77758c; font-size: 9px; padding: 28px 12px; text-align: center; }
    .authored-empty { line-height: 1.5; }
    .loading { color: #77758b; padding: 70px; text-align: center; }
    .modal-backdrop { align-items: center; background: rgb(4 4 8 / 78%); display: flex; inset: 0; justify-content: center; position: fixed; z-index: 1000; }
    .confirm-modal { background: #12121b; border: 1px solid #3b3b50; border-radius: 8px; box-shadow: 0 20px 70px #000; max-width: 410px; padding: 20px; width: calc(100% - 32px); }
    .confirm-modal h2 { margin-top: 4px; } .confirm-modal p { color: #9996a8; font-size: 10px; line-height: 1.5; margin: 10px 0; }
    .confirm-modal .actions { justify-content: flex-end; margin-top: 16px; }
    @media (max-width: 1450px) { .studio { grid-template-columns: 270px minmax(480px, 1fr) 270px; } }
    @media (max-width: 1180px) {
      .studio { grid-template-columns: 1fr 1fr; }
      .editor { grid-column: 1 / -1; grid-row: 1; }
      .library, .authored { height: 480px; position: static; }
    }
    @media (max-width: 760px) {
      .studio, .stage, .id-grid, .unlock-grid { grid-template-columns: 1fr; }
      .editor, .library, .authored { grid-column: 1; }
    }
  `],
})
export class KaeliStudio implements OnInit {
  readonly metadata = signal<KaeliAuthoringMetadata | null>(null);
  readonly authored = signal<KaeliSkinDefinition[]>([]);
  readonly outfitList = signal<OutfitOption[]>([]);
  readonly draft = signal<KaeliSkinDefinition>(this.emptyDraft());
  readonly selectedId = signal('');

  readonly outfitCategory = signal<OutfitCategory>('feminino');
  readonly outfitSearch = signal('');
  readonly outfitPage = signal(1);
  readonly authoredSearch = signal('');
  readonly authoredWaifuFilter = signal<string>('all');
  readonly region = signal<Region>('head');
  readonly walk = signal(false);

  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly pendingDelete = signal<KaeliSkinDefinition | null>(null);
  readonly status = signal<{ kind: 'ok' | 'err'; msg: string } | null>(null);

  readonly regions: { key: Region; label: string }[] = [
    { key: 'head', label: 'Cabeça' },
    { key: 'body', label: 'Corpo' },
    { key: 'legs', label: 'Pernas' },
    { key: 'feet', label: 'Pés' },
  ];

  readonly roster = computed<WaifuDef[]>(() => this.api.catalog()?.waifus ?? []);
  readonly mounts = computed<ItemCatalogEntry[]>(() =>
    (this.api.catalog()?.items ?? []).filter((item) => item.mountLookType > 0));

  readonly colorSwatches = computed(() => {
    const count = this.metadata()?.outfitColorCount ?? 133;
    return Array.from({ length: count }, (_, i) => this.cssColor(i));
  });
  readonly unlockKinds = computed(() => this.metadata()?.unlockKinds ?? ['default', 'affinity', 'gold', 'kaeros']);

  readonly categoryCounts = computed(() => {
    const list = this.outfitList();
    return {
      feminino: list.filter((o) => o.category === 'feminino').length,
      masculino: list.filter((o) => o.category === 'masculino').length,
      monster: list.filter((o) => o.category === 'monster').length,
      boss: list.filter((o) => o.category === 'boss').length,
      all: list.length,
    };
  });

  readonly filteredOutfits = computed(() => {
    const query = this.outfitSearch().trim().toLocaleLowerCase();
    const category = this.outfitCategory();
    return this.outfitList().filter((o) =>
      (category === 'all' || o.category === category)
      && (!query
        || o.name.toLocaleLowerCase().includes(query)
        || String(o.lookType).includes(query)
        || o.bestiaryClass.toLocaleLowerCase().includes(query)));
  });
  readonly outfitPageCount = computed(() => Math.max(1, Math.ceil(this.filteredOutfits().length / PAGE_SIZE)));
  readonly pagedOutfits = computed(() => {
    const page = Math.min(this.outfitPage(), this.outfitPageCount());
    return this.filteredOutfits().slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  });

  readonly filteredAuthored = computed(() => {
    const query = this.authoredSearch().trim().toLocaleLowerCase();
    return this.authored()
      .filter((s) =>
        (this.authoredWaifuFilter() === 'all' || s.waifuId === this.authoredWaifuFilter())
        && (!query || s.name.toLocaleLowerCase().includes(query) || s.id.toLocaleLowerCase().includes(query)))
      .sort((a, b) => a.waifuId.localeCompare(b.waifuId) || a.name.localeCompare(b.name));
  });

  constructor(
    private readonly api: ApiService,
    private readonly assets: AssetsService,
  ) {}

  async ngOnInit(): Promise<void> {
    try {
      const [metadata, authored, , , monsterMeta, outfitCatalog] = await Promise.all([
        this.api.getKaeliAuthoringMetadata(),
        this.api.getAuthoredKaeliSkins(),
        this.api.loadCatalog(),
        this.assets.load(),
        this.api.getMonsterAuthoringMetadata(),
        fetch('/assets/tibia/outfit-catalog.json').then((r) => r.json() as Promise<OutfitCatalogEntry[]>).catch(() => []),
      ]);
      // catálogo autoritativo de outfits de jogador (Canary outfits.xml): nome + gênero
      const catByLook = new Map(outfitCatalog.map((o) => [o.lookType, o]));
      // lookType -> aparência nomeada do Canary (bosses têm prioridade na classificação)
      const byLook = new Map<number, MonsterAppearance>();
      for (const appearance of monsterMeta.appearances) {
        const current = byLook.get(appearance.outfit.lookType);
        if (!current || (appearance.kind === 'boss' && current.kind !== 'boss')) {
          byLook.set(appearance.outfit.lookType, appearance);
        }
      }
      // montarias têm slot próprio (controle no estúdio) — fora da biblioteca de outfits
      const mountLooks = new Set(this.mounts().map((m) => m.mountLookType));
      this.outfitList.set(this.assets.ids('outfits')
        .filter((lookType) => !mountLooks.has(lookType))
        .map((lookType) => {
          const entry = this.assets.entry('outfits', lookType);
          const player = catByLook.get(lookType);
          const appearance = byLook.get(lookType);
          let category: OutfitOption['category'];
          let name: string;
          let bestiaryClass = '';
          if (player) {
            category = player.gender === 'male' ? 'masculino' : 'feminino';
            name = player.name;
          } else if (appearance) {
            category = appearance.kind === 'boss' ? 'boss' : 'monster';
            name = appearance.name;
            bestiaryClass = appearance.bestiaryClass;
          } else {
            category = 'other';
            name = entry?.name || `LookType ${lookType}`;
          }
          return {
            lookType,
            name,
            recolorable: entry?.groups.some((g) => g.layers >= 2) ?? false,
            category,
            bestiaryClass,
          } as OutfitOption;
        })
        // skins são recoloríveis: outfits de jogador sempre entram; monstros/bosses só se tiverem
        // camada de máscara de cor (sem isso a paleta não faz nada)
        .filter((o) => o.category === 'feminino' || o.category === 'masculino' || o.recolorable)
        .sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true })));
      this.metadata.set(metadata);
      this.authored.set(authored);
      this.newSkin();
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    }
  }

  // ---- color helpers ----
  cssColor(index: number): string {
    const [r, g, b] = AssetsService.outfitColor(index);
    return `rgb(${r}, ${g}, ${b})`;
  }

  // ---- library ----
  setOutfitSearch(value: string): void { this.outfitSearch.set(value); this.outfitPage.set(1); }
  setOutfitCategory(value: OutfitCategory): void { this.outfitCategory.set(value); this.outfitPage.set(1); }
  changeOutfitPage(delta: number): void {
    this.outfitPage.set(Math.max(1, Math.min(this.outfitPageCount(), this.outfitPage() + delta)));
  }
  chooseOutfit(lookType: number): void { this.patch({ lookType }); }
  setMount(lookType: number): void { this.patch({ mountLookType: lookType }); }

  // ---- studio controls ----
  hasAddon(bit: number): boolean { return (this.draft().addons & bit) !== 0; }
  toggleAddon(bit: number): void { this.patch({ addons: this.draft().addons ^ bit }); }
  pickColor(index: number): void { this.patch({ [this.region()]: index }); }

  setWaifu(waifuId: string): void {
    const reseed = !this.draft().id; // só re-seeda o visual em skins novas
    this.patch({ waifuId });
    if (reseed) this.loadKaeliDefault();
  }

  loadKaeliDefault(): void {
    const waifu = this.roster().find((w) => w.id === this.draft().waifuId);
    if (!waifu) return;
    this.patch({
      lookType: waifu.lookType,
      head: waifu.head, body: waifu.body, legs: waifu.legs, feet: waifu.feet,
    });
  }

  clampValue(value: string): number { return Math.max(0, Math.floor(+value || 0)); }

  unlockLabel(kind: string): string {
    const labels: Record<string, string> = {
      default: 'Padrão (grátis)', affinity: 'Afinidade', gold: 'Ouro', kaeros: 'Kaeros',
    };
    return labels[kind] ?? kind;
  }
  unlockValueLabel(): string {
    const labels: Record<string, string> = {
      affinity: 'Nível de afinidade', gold: 'Preço (ouro)', kaeros: 'Preço (Kaeros)',
    };
    return labels[this.draft().unlock] ?? 'Valor';
  }
  waifuName(waifuId: string): string {
    return this.roster().find((w) => w.id === waifuId)?.name ?? waifuId;
  }
  catLabel(category: string): string {
    const labels: Record<string, string> = {
      feminino: 'Feminino', masculino: 'Masculino', monster: 'Monstro', boss: 'Boss', other: 'Outro',
    };
    return labels[category] ?? category;
  }

  // ---- draft lifecycle ----
  patch(patch: Partial<KaeliSkinDefinition>): void {
    this.draft.update((skin) => ({ ...skin, ...patch }));
    this.status.set(null);
  }

  newSkin(): void {
    const waifuId = this.draft().waifuId || this.roster()[0]?.id || '';
    this.draft.set(this.emptyDraft(waifuId));
    this.loadKaeliDefault();
    this.selectedId.set('');
    this.status.set(null);
  }

  editSkin(skin: KaeliSkinDefinition): void {
    this.draft.set({ ...skin });
    this.selectedId.set(skin.id);
    this.status.set(null);
  }

  duplicateSkin(skin: KaeliSkinDefinition): void {
    this.editSkin(skin);
    this.duplicateCurrent();
  }

  duplicateCurrent(): void {
    this.draft.update((skin) => ({
      ...skin,
      id: '',
      name: skin.name ? `${skin.name} Echo` : '',
    }));
    this.selectedId.set('');
    this.status.set({ kind: 'ok', msg: 'Cópia aberta como nova skin. Ajuste o nome e salve.' });
  }

  requestDelete(skin: KaeliSkinDefinition): void { this.pendingDelete.set(skin); }
  cancelDelete(): void { if (!this.deleting()) this.pendingDelete.set(null); }

  async confirmDelete(): Promise<void> {
    const skin = this.pendingDelete();
    if (!skin) return;
    this.deleting.set(true);
    try {
      await this.api.deleteKaeliSkin(skin.id);
      this.authored.update((list) => list.filter((s) => s.id !== skin.id));
      if (this.selectedId() === skin.id) this.newSkin();
      this.pendingDelete.set(null);
      this.status.set({ kind: 'ok', msg: `${skin.name} foi excluída.` });
    } catch (error) {
      this.pendingDelete.set(null);
      this.status.set({ kind: 'err', msg: (error as Error).message });
    } finally {
      this.deleting.set(false);
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.status.set(null);
    try {
      const saved = this.draft().id
        ? await this.api.updateKaeliSkin(this.draft())
        : await this.api.createKaeliSkin(this.draft());
      this.authored.update((list) => [...list.filter((s) => s.id !== saved.id), saved]);
      this.draft.set({ ...saved });
      this.selectedId.set(saved.id);
      this.status.set({ kind: 'ok', msg: `Skin salva para ${this.waifuName(saved.waifuId)}. Já pode ser equipada.` });
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    } finally {
      this.saving.set(false);
    }
  }

  private emptyDraft(waifuId = ''): KaeliSkinDefinition {
    return {
      waifuId,
      id: '',
      name: '',
      description: '',
      lookType: 0,
      head: 0, body: 0, legs: 0, feet: 0,
      addons: 0,
      mountLookType: 0,
      unlock: 'default',
      unlockValue: 0,
    };
  }
}
