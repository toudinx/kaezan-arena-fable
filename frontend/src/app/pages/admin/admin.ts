import { Component, OnInit, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { DungeonTier, MonsterCatalogEntry } from '../../core/types';
import { CreaturePreview } from './creature-preview';
import { MonsterEditor } from './monster-editor';
import { KaeliStudio } from './kaeli-studio';

type AdminMode = 'dungeons' | 'monsters' | 'kaelis';
type CatalogMode = 'monsters' | 'bosses';
type MobKind = 'commonMobs' | 'eliteMobs';
type DropZone = MobKind | 'boss';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CreaturePreview, MonsterEditor, KaeliStudio],
  template: `
    <div class="page">
      <header class="titlebar">
        <div>
          <span class="eyebrow">Editor de conteudo</span>
          <h1>Bestiario & Dungeons</h1>
          <p>{{ monsters().length }} criaturas disponiveis. Alteracoes afetam apenas as proximas runs.</p>
        </div>
        <div class="header-actions">
          <div class="tabs page-tabs">
            <button type="button" [class.active]="pageMode() === 'dungeons'" (click)="pageMode.set('dungeons')">Dungeons</button>
            <button type="button" [class.active]="pageMode() === 'monsters'" (click)="pageMode.set('monsters')">Monstros</button>
            <button type="button" [class.active]="pageMode() === 'kaelis'" (click)="pageMode.set('kaelis')">Kaelis</button>
          </div>
          @if (pageMode() === 'dungeons') {
            <button class="secondary" type="button" [disabled]="busy()" (click)="reset()">Recarregar</button>
            <button class="primary" type="button" [disabled]="busy()" (click)="save()">
              {{ saving() ? 'Salvando...' : 'Salvar dungeons' }}
            </button>
          }
        </div>
      </header>

      @if (pageMode() === 'monsters') {
        <app-monster-editor />
      } @else if (pageMode() === 'kaelis') {
        <app-kaeli-studio />
      } @else {
        @if (status(); as state) {
          <div class="status" [class.ok]="state.kind === 'ok'" [class.err]="state.kind === 'err'">{{ state.msg }}</div>
        }

        <div class="workspace">
          <section class="catalog panel">
            <div class="catalog-head">
              <div class="tabs">
                <button type="button" [class.active]="mode() === 'monsters'" (click)="setMode('monsters')">
                  Monstros <span>{{ monsterCount() }}</span>
                </button>
                <button type="button" [class.active]="mode() === 'bosses'" (click)="setMode('bosses')">
                  Bosses <span>{{ bossCount() }}</span>
                </button>
              </div>
              <span class="result-count">{{ filtered().length }} resultado(s)</span>
            </div>

            <div class="filters">
              <label>Buscar
                <input type="search" placeholder="Nome, classe ou origem" [value]="search()"
                  (input)="search.set($any($event.target).value)" />
              </label>
              <label>Tipo
                <select (change)="typeFilter.set($any($event.target).value)">
                  <option value="">Todos</option>
                  @for (type of classes(); track type) {
                    <option [value]="type" [selected]="type === typeFilter()">{{ type }}</option>
                  }
                </select>
              </label>
              <label>Origem
                <select (change)="originFilter.set($any($event.target).value)">
                  <option value="">Todas</option>
                  @for (origin of origins(); track origin) {
                    <option [value]="origin" [selected]="origin === originFilter()">{{ origin }}</option>
                  }
                </select>
              </label>
            </div>

            @if (loading()) {
              <div class="empty">Carregando bestiario...</div>
            } @else {
              <div class="creature-grid">
                @for (monster of filtered(); track monster.id) {
                  <article class="creature-card"
                    [class.common-selected]="inCommon(monster.id)"
                    [class.elite-selected]="inElite(monster.id)"
                    [class.boss-selected]="isBoss(monster.id)"
                    [draggable]="true"
                    (dragstart)="startDrag($event, monster)"
                    (dragend)="endDrag()">
                    <div class="sprite"><app-creature-preview [creature]="monster" [size]="76" /></div>
                    <div class="creature-info">
                      <div class="creature-name">
                        <strong [title]="monster.name">{{ monster.name }}</strong>
                        @if (roleLabel(monster.id); as role) {
                          <i [class]="role.kind">{{ role.label }}</i>
                        }
                      </div>
                      <small>{{ monster.bestiaryClass || 'Sem classe' }}</small>
                      <div class="badges">
                        <span>{{ monster.source === 'authored' ? 'KAEZAN' : (monster.origin || 'LEGADO') }}</span>
                        @if (monster.source === 'authored') { <span>T{{ monster.powerTier }}</span> }
                      </div>
                      <div class="stats"><span>HP {{ monster.health }}</span><span>XP {{ monster.experience }}</span></div>
                    </div>
                    <div class="card-actions">
                      @if (mode() === 'monsters') {
                        <button type="button" [class.active-common]="inCommon(monster.id)"
                          [disabled]="!canPlace(monster, 'commonMobs')"
                          (click)="toggle(monster.id, 'commonMobs')">Comum</button>
                        <button type="button" [class.active-elite]="inElite(monster.id)"
                          [disabled]="!canPlace(monster, 'eliteMobs')"
                          (click)="toggle(monster.id, 'eliteMobs')">Elite</button>
                      } @else {
                        <button type="button" class="boss-action" [class.active-boss]="isBoss(monster.id)"
                          (click)="setBoss(monster.id)">
                          {{ isBoss(monster.id) ? 'Boss selecionado' : 'Definir como boss' }}
                        </button>
                      }
                    </div>
                  </article>
                } @empty {
                  <div class="empty">Nenhuma criatura encontrada.</div>
                }
              </div>
            }
          </section>

          <aside class="dungeon panel">
            <div class="dungeon-head">
              <div><span class="eyebrow">Composicao</span><h2>Dungeon</h2></div>
              @if (current(); as tier) { <b class="tier-badge">Tier {{ tier.tier }}</b> }
            </div>

            <div class="tier-tabs">
              @for (tier of draft(); track tier.tier; let index = $index) {
                <button type="button" [class.active]="index === sel()" (click)="selectTier(index)">T{{ tier.tier }}</button>
              }
            </div>

            @if (current(); as tier) {
              <label>Nome
                <input class="dungeon-name" [value]="tier.name" (input)="setField('name', $any($event.target).value)" />
              </label>
              <label>Descricao
                <textarea rows="2" [value]="tier.description" (input)="setField('description', $any($event.target).value)"></textarea>
              </label>
              <div class="numbers">
                <label>Nivel minimo
                  <input type="number" min="1" [value]="tier.requiredAccountLevel"
                    (input)="setNum('requiredAccountLevel', $any($event.target).value, 1)" />
                </label>
                <label>Multiplicador legado
                  <input type="number" min=".1" step=".05" [value]="tier.statMultiplier"
                    (input)="setNum('statMultiplier', $any($event.target).value, .1)" />
                </label>
              </div>
              <p class="legacy-note">O multiplicador acima afeta apenas placeholders legados. Monstros autorais usam tier e funcao.</p>

              <section class="group common">
                <div class="group-head"><h3>Comuns <span>{{ tier.commonMobs.length }}</span></h3><small>Maior presenca no mapa.</small></div>
                <div class="drop-zone" [class.drop-active]="dropTarget() === 'commonMobs'"
                  (dragover)="allowDrop($event, 'commonMobs')" (dragleave)="leaveDrop('commonMobs')" (drop)="drop($event, 'commonMobs')">
                  @for (ref of tier.commonMobs; track ref) {
                    @if (findMonster(ref); as monster) {
                      <div class="chip"><app-creature-preview [creature]="monster" [size]="38" />
                        <span>{{ monster.name }}</span><button type="button" (click)="remove(ref, 'commonMobs')">&times;</button>
                      </div>
                    }
                  } @empty { <div class="zone-empty">Arraste monstros ou use Comum.</div> }
                </div>
              </section>

              <section class="group elite">
                <div class="group-head"><h3>Elites <span>{{ tier.eliteMobs.length }}</span></h3><small>Ameacas intermediarias e mais raras.</small></div>
                <div class="drop-zone" [class.drop-active]="dropTarget() === 'eliteMobs'"
                  (dragover)="allowDrop($event, 'eliteMobs')" (dragleave)="leaveDrop('eliteMobs')" (drop)="drop($event, 'eliteMobs')">
                  @for (ref of tier.eliteMobs; track ref) {
                    @if (findMonster(ref); as monster) {
                      <div class="chip"><app-creature-preview [creature]="monster" [size]="38" />
                        <span>{{ monster.name }}</span><button type="button" (click)="remove(ref, 'eliteMobs')">&times;</button>
                      </div>
                    }
                  } @empty { <div class="zone-empty">Arraste monstros ou use Elite.</div> }
                </div>
              </section>

              <section class="group boss">
                <div class="group-head"><h3>Boss <span>1</span></h3><small>Confronto final.</small></div>
                <div class="drop-zone boss-zone" [class.drop-active]="dropTarget() === 'boss'"
                  (dragover)="allowDrop($event, 'boss')" (dragleave)="leaveDrop('boss')" (drop)="drop($event, 'boss')">
                  @if (findMonster(tier.boss); as monster) {
                    <div class="boss-card"><app-creature-preview [creature]="monster" [size]="58" />
                      <div><strong>{{ monster.name }}</strong><span>{{ monster.bestiaryClass || 'Boss' }}</span>
                        <small>HP {{ monster.health }} · XP {{ monster.experience }}</small></div>
                    </div>
                  } @else { <div class="zone-empty">Escolha um boss na aba Bosses.</div> }
                </div>
              </section>

              <footer class="summary">
                <span><b>{{ tier.commonMobs.length }}</b> comuns</span>
                <span><b>{{ tier.eliteMobs.length }}</b> elites</span>
                <span><b>1</b> boss</span>
              </footer>
            }
          </aside>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .page { max-width: 1680px; margin: 0 auto; padding: 24px 28px 40px; }
    .titlebar, .header-actions, .catalog-head, .dungeon-head, .group-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .titlebar { border-bottom: 1px solid #29293a; margin-bottom: 14px; padding-bottom: 15px; }
    h1, h2, h3, p { margin: 0; } h1 { font-size: 29px; } h2 { font-size: 21px; }
    .eyebrow { color: #2dd4bf; display: block; font-size: 9px; font-weight: 900; letter-spacing: 1.3px; text-transform: uppercase; }
    .titlebar p { color: #8c899d; font-size: 12px; margin-top: 4px; }
    button { border: 1px solid transparent; border-radius: 5px; color: #d9d7e5; font: inherit; }
    button:disabled { opacity: .55; }
    .primary, .secondary { min-height: 37px; padding: 0 14px; font-size: 11px; font-weight: 900; }
    .primary { background: #1db9aa; color: #061d1a; } .secondary { background: #1b1b28; border-color: #313145; }
    .tabs { background: #0f0f17; border: 1px solid #303043; border-radius: 5px; display: inline-flex; overflow: hidden; }
    .tabs button { background: transparent; border: 0; border-radius: 0; color: #9290a4; min-height: 34px; min-width: 110px; font-size: 11px; font-weight: 900; }
    .tabs button + button { border-left: 1px solid #303043; }
    .tabs button.active { background: #1b433d; color: #64ead6; }
    .page-tabs button { min-height: 37px; }
    .status { border: 1px solid; border-radius: 6px; font-size: 12px; margin-bottom: 12px; padding: 9px 11px; }
    .status.ok { background: #102a25; border-color: #22675d; color: #55e5cf; } .status.err { background: #32191e; border-color: #6d303b; color: #ff9aa5; }
    .workspace { display: grid; grid-template-columns: minmax(0, 1fr) 420px; gap: 16px; align-items: start; }
    .panel { border-radius: 8px; padding: 14px; min-width: 0; }
    .filters { display: grid; grid-template-columns: minmax(220px, 1fr) 160px 150px; gap: 9px; margin: 11px 0; }
    label { color: #89879b; display: flex; flex-direction: column; gap: 5px; font-size: 10px; font-weight: 800; margin-top: 8px; }
    input, select, textarea { background: #0e0e16; border: 1px solid #303043; border-radius: 5px; color: #e8e6f0; font: inherit; outline: none; }
    input, select { height: 36px; padding: 0 9px; } textarea { padding: 8px 9px; resize: vertical; }
    input:focus, select:focus, textarea:focus { border-color: #26aa9d; }
    .result-count { color: #77758c; font-size: 11px; }
    .creature-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(235px, 1fr)); gap: 7px; max-height: calc(100vh - 265px); overflow-y: auto; }
    .creature-card { background: #11111a; border: 1px solid #2b2b3c; border-radius: 6px; display: grid; grid-template-columns: 80px minmax(0, 1fr); min-height: 109px; overflow: hidden; position: relative; }
    .creature-card:hover { background: #151520; border-color: #505068; }
    .creature-card.common-selected { border-color: #27b9a7; } .creature-card.elite-selected { border-color: #9f67d8; } .creature-card.boss-selected { border-color: #d98b3f; }
    .sprite { background: radial-gradient(circle, #28293a, #12121b 70%); border-right: 1px solid #29293a; display: grid; place-items: center; }
    .creature-info { min-width: 0; padding: 9px 8px 34px; }
    .creature-name { display: flex; gap: 5px; align-items: center; }
    .creature-name strong { flex: 1; font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .creature-name i { border-radius: 3px; font-size: 7px; font-style: normal; font-weight: 900; padding: 3px 4px; }
    .creature-name i.common { background: #173b35; color: #59dac8; } .creature-name i.elite { background: #2e2040; color: #c9a0ef; } .creature-name i.boss { background: #3b2818; color: #efad69; }
    .creature-info small { color: #9a98aa; display: block; font-size: 9px; margin-top: 3px; }
    .badges, .stats { display: flex; gap: 4px; margin-top: 5px; }
    .badges span, .stats span { background: #181823; border: 1px solid #303043; border-radius: 3px; color: #9693a8; font-size: 8px; padding: 3px 4px; }
    .badges span { color: #70bbeb; }
    .card-actions { bottom: 6px; display: flex; gap: 4px; left: 87px; position: absolute; right: 7px; }
    .card-actions button { background: #1b1b28; border-color: #2b2b3d; flex: 1; font-size: 9px; font-weight: 900; min-height: 24px; }
    .card-actions .active-common { background: #1bad9d; color: #061d1a; } .card-actions .active-elite { background: #8650bd; } .card-actions .active-boss { background: #c97732; color: #251204; }
    .empty { color: #77758c; grid-column: 1 / -1; padding: 60px 20px; text-align: center; }
    .dungeon { max-height: calc(100vh - 80px); overflow-y: auto; position: sticky; top: 68px; }
    .tier-badge { background: #1a2c2a; border: 1px solid #2c756c; border-radius: 4px; color: #51d9c6; font-size: 9px; padding: 5px 7px; text-transform: uppercase; }
    .tier-tabs { display: grid; grid-template-columns: repeat(5, 1fr); gap: 5px; margin: 11px 0; }
    .tier-tabs button { background: #11111a; border-color: #2c2c3e; color: #8f8da3; font-size: 10px; height: 31px; }
    .tier-tabs button.active { background: #183933; border-color: #2db7a5; color: #58ddca; }
    .dungeon-name { font-size: 14px; font-weight: 900; } .numbers { display: grid; grid-template-columns: 1fr 1fr; gap: 9px; }
    .legacy-note { background: #171721; border-left: 2px solid #77604a; color: #77758b; font-size: 9px; line-height: 1.4; margin-top: 9px; padding: 6px 8px; }
    .group { margin-top: 13px; } .group-head h3 { color: #c7c5d2; font-size: 10px; letter-spacing: .5px; text-transform: uppercase; }
    .group-head h3 span { border-radius: 3px; margin-left: 3px; padding: 2px 5px; }
    .group-head small { color: #6f6d80; font-size: 9px; }
    .common h3 span { background: #173b35; color: #57dcca; } .elite h3 span { background: #2e2040; color: #c69bec; } .boss h3 span { background: #3b2818; color: #efad69; }
    .drop-zone { border: 1px dashed #353549; border-radius: 5px; display: grid; gap: 5px; grid-template-columns: repeat(2, minmax(0, 1fr)); margin-top: 6px; min-height: 58px; padding: 6px; }
    .common .drop-zone { border-color: #2a514c; } .elite .drop-zone { border-color: #4d3864; } .boss .drop-zone { border-color: #5d4027; }
    .drop-zone.drop-active { background: #182724; border-color: #48d7c2; }
    .chip { align-items: center; background: #15151f; border: 1px solid #343448; border-radius: 4px; display: grid; gap: 3px; grid-template-columns: 38px minmax(0, 1fr) 17px; min-width: 0; }
    .chip span { font-size: 9px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; } .chip button { background: transparent; border: 0; color: #d97884; font-size: 14px; padding: 0; }
    .zone-empty { color: #666477; font-size: 9px; grid-column: 1 / -1; padding: 11px; text-align: center; }
    .boss-zone { display: block; min-height: 74px; }
    .boss-card { align-items: center; background: linear-gradient(90deg, #211912, #15151e); border: 1px solid #684526; border-radius: 4px; display: grid; gap: 8px; grid-template-columns: 62px 1fr; padding: 4px; }
    .boss-card strong, .boss-card span, .boss-card small { display: block; } .boss-card strong { color: #f0c08f; font-size: 12px; }
    .boss-card span { color: #9d91a1; font-size: 9px; } .boss-card small { color: #756c79; font-size: 8px; margin-top: 4px; }
    .summary { border-top: 1px solid #29293a; display: grid; grid-template-columns: repeat(3, 1fr); gap: 5px; margin-top: 13px; padding-top: 9px; }
    .summary span { background: #11111a; border: 1px solid #29293a; border-radius: 4px; color: #77758b; font-size: 9px; padding: 6px; text-align: center; } .summary b { color: #d8d6e2; }
    @media (max-width: 1100px) { .workspace { grid-template-columns: 1fr; } .dungeon { max-height: none; position: static; } .creature-grid { max-height: none; } }
    @media (max-width: 720px) { .page { padding: 16px; } .titlebar, .header-actions { align-items: stretch; flex-direction: column; } .filters, .drop-zone { grid-template-columns: 1fr; } }
  `],
})
export class AdminPage implements OnInit {
  readonly pageMode = signal<AdminMode>('dungeons');
  readonly draft = signal<DungeonTier[]>([]);
  readonly sel = signal(0);
  readonly mode = signal<CatalogMode>('monsters');
  readonly status = signal<{ kind: 'ok' | 'err'; msg: string } | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly search = signal('');
  readonly typeFilter = signal('');
  readonly originFilter = signal('');
  readonly dragged = signal<MonsterCatalogEntry | null>(null);
  readonly dropTarget = signal<DropZone | null>(null);

  readonly busy = computed(() => this.loading() || this.saving());
  readonly monsters = computed<MonsterCatalogEntry[]>(() => this.api.catalog()?.monsters ?? []);
  readonly current = computed(() => this.draft()[this.sel()]);
  readonly bossRefs = computed(() => new Set([
    ...this.monsters().filter((monster) => monster.isBoss || monster.rank === 'boss').flatMap((monster) => [monster.id, monster.name]),
    ...(this.api.catalog()?.tiers ?? []).map((tier) => tier.boss),
    ...this.draft().map((tier) => tier.boss),
  ]));
  readonly modeMonsters = computed(() => this.monsters().filter((monster) =>
    this.mode() === 'bosses' ? this.isBossCandidate(monster) : !this.isBossCandidate(monster)));
  readonly monsterCount = computed(() => this.monsters().filter((monster) => !this.isBossCandidate(monster)).length);
  readonly bossCount = computed(() => this.monsters().filter((monster) => this.isBossCandidate(monster)).length);
  readonly classes = computed(() => [...new Set(this.modeMonsters().map((monster) => monster.bestiaryClass).filter(Boolean))].sort());
  readonly origins = computed(() => [...new Set(this.modeMonsters().map((monster) => monster.origin).filter((value): value is string => !!value))].sort());
  readonly filtered = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return this.modeMonsters().filter((monster) => {
      const text = `${monster.name} ${monster.bestiaryClass} ${monster.origin ?? ''}`.toLocaleLowerCase();
      return (!query || text.includes(query))
        && (!this.typeFilter() || monster.bestiaryClass === this.typeFilter())
        && (!this.originFilter() || monster.origin === this.originFilter());
    }).sort((a, b) => a.name.localeCompare(b.name));
  });

  constructor(private readonly api: ApiService) {}

  async ngOnInit(): Promise<void> {
    try {
      await this.api.loadCatalog();
      await this.reset();
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    } finally {
      this.loading.set(false);
    }
  }

  async reset(): Promise<void> {
    this.draft.set(structuredClone(await this.api.getAdminTiers()));
    this.sel.set(Math.min(this.sel(), Math.max(0, this.draft().length - 1)));
    this.status.set(null);
  }

  setMode(mode: CatalogMode): void {
    this.mode.set(mode);
    this.search.set('');
    this.typeFilter.set('');
    this.originFilter.set('');
  }

  selectTier(index: number): void {
    this.sel.set(index);
    this.dropTarget.set(null);
  }

  findMonster(reference: string): MonsterCatalogEntry | undefined {
    return this.monsters().find((monster) => monster.id === reference || monster.name === reference);
  }

  inCommon(reference: string): boolean { return this.current()?.commonMobs.includes(reference) ?? false; }
  inElite(reference: string): boolean { return this.current()?.eliteMobs.includes(reference) ?? false; }
  isBoss(reference: string): boolean { return this.current()?.boss === reference; }
  isBossCandidate(monster: MonsterCatalogEntry): boolean {
    return monster.isBoss || monster.rank === 'boss' || this.bossRefs().has(monster.id) || this.bossRefs().has(monster.name);
  }

  canPlace(monster: MonsterCatalogEntry, kind: MobKind): boolean {
    if (monster.source === 'legacy') return !this.isBossCandidate(monster);
    return kind === 'commonMobs' ? monster.rank === 'common' : monster.rank === 'elite';
  }

  roleLabel(reference: string): { label: string; kind: string } | null {
    if (this.isBoss(reference)) return { label: 'BOSS', kind: 'boss' };
    if (this.inElite(reference)) return { label: 'ELITE', kind: 'elite' };
    if (this.inCommon(reference)) return { label: 'COMUM', kind: 'common' };
    return null;
  }

  private patch(change: Partial<DungeonTier>): void {
    const selected = this.sel();
    this.draft.update((tiers) => tiers.map((tier, index) => index === selected ? { ...tier, ...change } : tier));
    this.status.set(null);
  }

  setField(field: 'name' | 'description', value: string): void {
    this.patch({ [field]: value } as Partial<DungeonTier>);
  }

  setNum(field: 'requiredAccountLevel' | 'statMultiplier', value: string, min: number): void {
    const parsed = field === 'requiredAccountLevel' ? Math.floor(+value || min) : +value || min;
    this.patch({ [field]: Math.max(min, parsed) } as Partial<DungeonTier>);
  }

  setBoss(reference: string): void { this.patch({ boss: reference }); }

  toggle(reference: string, kind: MobKind): void {
    const tier = this.current();
    if (!tier) return;
    const monster = this.findMonster(reference);
    if (!monster || !this.canPlace(monster, kind)) return;
    const other: MobKind = kind === 'commonMobs' ? 'eliteMobs' : 'commonMobs';
    if (tier[kind].includes(reference)) {
      this.patch({ [kind]: tier[kind].filter((entry) => entry !== reference) } as Partial<DungeonTier>);
      return;
    }
    this.patch({
      [kind]: [...tier[kind], reference],
      [other]: tier[other].filter((entry) => entry !== reference),
    } as Partial<DungeonTier>);
  }

  remove(reference: string, kind: MobKind): void {
    const tier = this.current();
    if (tier) this.patch({ [kind]: tier[kind].filter((entry) => entry !== reference) } as Partial<DungeonTier>);
  }

  startDrag(event: DragEvent, monster: MonsterCatalogEntry): void {
    this.dragged.set(monster);
    event.dataTransfer?.setData('text/plain', monster.id);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy';
  }

  endDrag(): void {
    this.dragged.set(null);
    this.dropTarget.set(null);
  }

  allowDrop(event: DragEvent, zone: DropZone): void {
    const monster = this.dragged();
    const accepts = zone === 'boss' ? monster && this.isBossCandidate(monster) : monster && this.canPlace(monster, zone);
    if (!accepts) return;
    event.preventDefault();
    this.dropTarget.set(zone);
  }

  leaveDrop(zone: DropZone): void {
    if (this.dropTarget() === zone) this.dropTarget.set(null);
  }

  drop(event: DragEvent, zone: DropZone): void {
    event.preventDefault();
    const monster = this.dragged();
    this.dropTarget.set(null);
    if (!monster) return;
    if (zone === 'boss' && this.isBossCandidate(monster)) this.setBoss(monster.id);
    if (zone !== 'boss' && this.canPlace(monster, zone) && !this.current()?.[zone].includes(monster.id))
      this.toggle(monster.id, zone);
    this.dragged.set(null);
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.status.set(null);
    try {
      this.draft.set(structuredClone(await this.api.saveAdminTiers(this.draft())));
      this.status.set({ kind: 'ok', msg: 'Dungeons salvas. As proximas runs ja usam esta composicao.' });
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    } finally {
      this.saving.set(false);
    }
  }
}
