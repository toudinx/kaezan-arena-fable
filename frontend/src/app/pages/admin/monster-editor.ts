import { Component, OnInit, computed, output, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import {
  DungeonTier,
  MonsterAppearance,
  MonsterAuthoringMetadata,
  MonsterCatalogEntry,
  MonsterDefinition,
} from '../../core/types';
import { CreaturePreview } from './creature-preview';

type AppearanceKindFilter = 'all' | 'normal' | 'boss';
type AppearanceScopeFilter = 'all' | 'legacy';
type AuthoredRankFilter = 'all' | 'common' | 'elite' | 'boss';
type ResistanceKind = 'neutral' | 'weak' | 'resist';
type ResistanceGrade = 'low' | 'moderate' | 'high';

const APPEARANCE_PAGE_SIZE = 60;
const AUTHORED_PAGE_SIZE = 8;

@Component({
  selector: 'app-monster-editor',
  standalone: true,
  imports: [CreaturePreview],
  template: `
    <div class="monster-workspace">
      <aside class="source-library panel">
        <div class="panel-head">
          <div>
            <span class="eyebrow">Visual Archive</span>
            <h2>Canary Library</h2>
          </div>
          <b class="count">{{ filteredAppearances().length }}</b>
        </div>

        <input
          class="search"
          type="search"
          placeholder="Name, type, or lookType"
          [value]="appearanceSearch()"
          (input)="setAppearanceSearch($any($event.target).value)"
        />

        <div class="filter-grid">
          <label>Class
            <select (change)="setFamilyFilter($any($event.target).value)">
              <option value="">All</option>
              @for (family of appearanceFamilies(); track family) {
                <option [value]="family" [selected]="family === familyFilter()">{{ family }}</option>
              }
            </select>
          </label>
          <label>Category
            <select (change)="setKindFilter($any($event.target).value)">
              <option value="all" [selected]="kindFilter() === 'all'">Normal and bosses</option>
              <option value="normal" [selected]="kindFilter() === 'normal'">Normal</option>
              <option value="boss" [selected]="kindFilter() === 'boss'">Bosses</option>
            </select>
          </label>
        </div>

        <div class="scope-tabs">
          <button type="button" [class.active]="scopeFilter() === 'all'" (click)="setScopeFilter('all')">
            All Canary
          </button>
          <button type="button" [class.active]="scopeFilter() === 'legacy'" (click)="setScopeFilter('legacy')">
            Imported legacy
          </button>
        </div>

        <div class="appearance-list">
          @for (card of appearanceCards(); track card.appearance.id) {
            <button
              type="button"
              class="appearance-row"
              [class.active]="selectedAppearance()?.id === card.appearance.id"
              (click)="chooseAppearance(card.appearance)"
            >
              <app-creature-preview [creature]="card.creature" [size]="52" />
              <span class="row-copy">
                <strong [title]="card.appearance.name">{{ card.appearance.name }}</strong>
                <small>{{ card.appearance.bestiaryClass }} - LookType {{ card.appearance.outfit.lookType }}</small>
                <span class="badges">
                  <i [class.boss]="card.appearance.kind === 'boss'">
                    {{ card.appearance.kind === 'boss' ? 'BOSS' : 'NORMAL' }}
                  </i>
                  @if (card.appearance.legacyImported) { <i class="legacy">LEGACY</i> }
                  @if (card.appearance.bestiaryClass === 'Unclassified') { <i class="review">REVIEW</i> }
                  @if (!hasOutfit(card.appearance.outfit.lookType)) { <i class="missing">NO SPRITE</i> }
                </span>
              </span>
            </button>
          } @empty {
            <div class="empty">No appearance found.</div>
          }
        </div>

        <footer class="pagination">
          <button type="button" [disabled]="appearancePage() <= 1" (click)="changeAppearancePage(-1)">Previous</button>
          <span>{{ appearancePage() }} / {{ appearancePageCount() }}</span>
          <button type="button" [disabled]="appearancePage() >= appearancePageCount()" (click)="changeAppearancePage(1)">Next</button>
        </footer>
      </aside>

      <main class="editor panel">
        @if (metadata(); as meta) {
          <div class="editor-head">
            <div>
              <span class="eyebrow">Kaezan Configuration</span>
              <h2>{{ draft().id ? 'Edit monster' : 'New monster' }}</h2>
              @if (draft().id) {
                <code>{{ draft().id }}</code>
              } @else if (selectedAppearance(); as appearance) {
                <small class="base-label">Visual base: {{ appearance.name }}</small>
              }
            </div>
            <div class="actions">
              <button type="button" class="secondary" (click)="duplicateCurrent()">Duplicate</button>
              <button type="button" class="primary" [disabled]="saving()" (click)="save()">
                {{ saving() ? 'Saving...' : 'Save monster' }}
              </button>
            </div>
          </div>

          @if (status(); as state) {
            <div class="status" [class.ok]="state.kind === 'ok'" [class.err]="state.kind === 'err'">{{ state.msg }}</div>
          }

          <div class="editor-grid">
            <section class="form-section identity">
              <h3>Visual identity</h3>
              <div class="identity-grid">
                <div class="preview">
                  <app-creature-preview [creature]="previewCreature()" [size]="112" />
                  <b>{{ selectedAppearance()?.name || 'LookType ' + draft().outfit.lookType }}</b>
                  <small>LookType {{ draft().outfit.lookType }}</small>
                  @if (selectedAppearance(); as appearance) {
                    <span>{{ appearance.kind === 'boss' ? 'Canary boss' : 'Canary monster' }}</span>
                  }
                </div>
                <div class="fields">
                  <label>Name
                    <input [value]="draft().name" (input)="patch({ name: $any($event.target).value })" />
                  </label>
                  <label>Description
                    <textarea rows="2" [value]="draft().description" (input)="patch({ description: $any($event.target).value })"></textarea>
                  </label>
                  <label>Bestiary type
                    <input [value]="draft().bestiaryClass" (input)="patch({ bestiaryClass: $any($event.target).value })" />
                  </label>
                  <p class="hint appearance-hint">
                    Choose or swap the visual from the library on the left. Only outfit, corpse, and visual type are copied.
                  </p>
                </div>
              </div>
            </section>

            <section class="form-section build">
              <h3>Build</h3>
              <div class="three">
                <label>Power tier
                  <select (change)="patchNum('powerTier', $any($event.target).value)">
                    @for (tier of [1, 2, 3, 4, 5]; track tier) {
                      <option [value]="tier" [selected]="tier === draft().powerTier">Tier {{ tier }}</option>
                    }
                  </select>
                </label>
                <label>Role
                  <select (change)="patch({ rank: $any($event.target).value })">
                    @for (rank of ranks; track rank.id) {
                      <option [value]="rank.id" [selected]="rank.id === draft().rank">{{ rank.name }}</option>
                    }
                  </select>
                </label>
                <label>Offensive element
                  <select (change)="patch({ elementId: $any($event.target).value })">
                    @for (element of meta.elements; track element.id) {
                      <option [value]="element.id" [selected]="element.id === draft().elementId">{{ element.name }}</option>
                    }
                  </select>
                </label>
              </div>

              <label>Behavior
                <select (change)="patch({ behaviorId: $any($event.target).value })">
                  @for (behavior of meta.behaviors; track behavior.id) {
                    <option [value]="behavior.id" [selected]="behavior.id === draft().behaviorId">{{ behavior.name }}</option>
                  }
                </select>
              </label>
              <p class="behavior">{{ behaviorDescription() }}</p>

              <label>Stat preset
                <select (change)="applyPreset($any($event.target).value)">
                  @for (preset of meta.presets; track preset.id) {
                    <option [value]="preset.id" [selected]="preset.id === draft().statPresetId">{{ preset.name }}</option>
                  }
                </select>
              </label>
              <div class="four">
                @for (modifier of modifiers; track modifier.key) {
                  <label>{{ modifier.label }}
                    <input
                      type="number"
                      [min]="meta.modifierMin"
                      [max]="meta.modifierMax"
                      step="0.05"
                      [value]="modifierValue(modifier.key)"
                      (input)="setModifier(modifier.key, $any($event.target).value)"
                    />
                  </label>
                }
              </div>
              <p class="hint">
                Presets are starting points. Each multiplier can be adjusted between
                {{ meta.modifierMin }}x e {{ meta.modifierMax }}x.
              </p>
            </section>

            <section class="form-section result">
              <h3>Resulting stats</h3>
              <div class="stat-cards">
                <div><span>HP</span><b>{{ resolvedStats().health }}</b></div>
                <div><span>Base damage</span><b>{{ resolvedStats().damage }}</b><small>attacks vary by kit</small></div>
                <div><span>Armor</span><b>{{ resolvedStats().armor }}</b></div>
                <div><span>Speed</span><b>{{ resolvedStats().speed }}</b></div>
                <div><span>XP</span><b>{{ resolvedStats().experience }}</b></div>
              </div>
              @if (draft().rank === 'boss') {
                <div class="boss-note">Authored boss uses this HP directly and does not receive a legacy multiplier.</div>
              }
            </section>

            <section class="form-section resistances">
              <h3>Weaknesses and resistances</h3>
              <p class="hint">Choose weakness or resistance by element. The default value is 5%, but it can be edited.</p>
              <div class="resistance-toolbar">
                <button type="button" class="secondary compact" (click)="clearResistances()">Clear resistances</button>
              </div>
              <div class="resistance-grid">
                @for (element of meta.elements; track element.id) {
                  <label
                    class="resistance-card"
                    [class.weak]="resistance(element.id) < 0"
                    [class.resist]="resistance(element.id) > 0"
                  >
                    <span>{{ element.name }}</span>
                    <select
                      [value]="resistanceKind(element.id)"
                      (change)="setResistanceKind(element.id, $any($event.target).value)"
                    >
                      <option value="neutral">Neutral</option>
                      <option value="weak">Weakness</option>
                      <option value="resist">Resistance</option>
                    </select>
                    <div class="suffix">
                      <input
                        type="number"
                        min="0"
                        [max]="maxResistanceMagnitude()"
                        step="1"
                        [disabled]="resistanceKind(element.id) === 'neutral'"
                        [value]="resistanceMagnitude(element.id)"
                        (input)="setResistanceAmount(element.id, $any($event.target).value)"
                      />
                      <span>%</span>
                    </div>
                  </label>
                }
              </div>
            </section>

            <section class="form-section keywords">
              <h3>Keyword resistance (cards)</h3>
              <p class="hint">
                Percentage resisted by the monster for each card keyword (G-04): 0 = normal, 100 = immune,
                negative values amplify. Use it to force build variety (for example, Curse 80 against Velvet).
              </p>
              <div class="resistance-toolbar">
                <button type="button" class="secondary compact" (click)="clearKeywordResistances()">Clear keywords</button>
              </div>
              <div class="resistance-grid keyword-grid">
                @for (tag of meta.keywordTags; track tag) {
                  <label
                    class="resistance-card"
                    [class.weak]="keywordResist(tag) < 0"
                    [class.resist]="keywordResist(tag) > 0"
                  >
                    <span>{{ keywordLabel(tag) }}</span>
                    <div class="suffix">
                      <input
                        type="number"
                        [min]="meta.keywordResistMin"
                        [max]="meta.keywordResistMax"
                        step="5"
                        [value]="keywordResist(tag)"
                        (input)="setKeywordResist(tag, $any($event.target).value)"
                      />
                      <span>%</span>
                    </div>
                  </label>
                }
              </div>
            </section>
          </div>
        } @else {
          <div class="loading">Loading monster authoring...</div>
        }
      </main>

      <aside class="authored-library panel">
        <div class="panel-head">
          <div>
            <span class="eyebrow">Authored Content</span>
            <h2>Kaezan Monsters</h2>
          </div>
          <button class="primary compact" type="button" (click)="newMonster()">New</button>
        </div>

        <input
          class="search"
          type="search"
          placeholder="Search Kaezan monster"
          [value]="authoredSearch()"
          (input)="setAuthoredSearch($any($event.target).value)"
        />
        <label>Role
          <select (change)="setAuthoredRankFilter($any($event.target).value)">
            <option value="all" [selected]="authoredRankFilter() === 'all'">All</option>
            @for (rank of ranks; track rank.id) {
              <option [value]="rank.id" [selected]="authoredRankFilter() === rank.id">{{ rank.name }}</option>
            }
          </select>
        </label>

        <div class="authored-list">
          @for (card of authoredCards(); track card.monster.id) {
            <article class="authored-card" [class.active]="selectedId() === card.monster.id">
              <button type="button" class="authored-main" (click)="editMonster(card.monster)">
                <app-creature-preview [creature]="card.creature" [size]="54" />
                <span>
                  <strong [title]="card.monster.name">{{ card.monster.name }}</strong>
                  <small>{{ card.monster.bestiaryClass }} - Tier {{ card.monster.powerTier }}</small>
                  <i [class]="card.monster.rank">{{ label(card.monster.rank) }}</i>
                </span>
              </button>
              @if (card.usages.length) {
                <div class="usage" [title]="card.usages.join(', ')">In use: {{ card.usages.join(', ') }}</div>
              }
              <footer>
                <button type="button" (click)="editMonster(card.monster)">Edit</button>
                <button type="button" (click)="duplicateMonster(card.monster)">Duplicate</button>
                <button type="button" class="danger" (click)="requestDelete(card.monster)">Delete</button>
              </footer>
            </article>
          } @empty {
            <div class="empty authored-empty">
              No Kaezan monster in this filter. Choose a Canary visual and save the first creature.
            </div>
          }
        </div>

        <footer class="pagination">
          <button type="button" [disabled]="authoredPage() <= 1" (click)="changeAuthoredPage(-1)">Previous</button>
          <span>{{ authoredPage() }} / {{ authoredPageCount() }} - {{ filteredAuthored().length }}</span>
          <button type="button" [disabled]="authoredPage() >= authoredPageCount()" (click)="changeAuthoredPage(1)">Next</button>
        </footer>
      </aside>

      @if (pendingDelete(); as monster) {
        <div class="modal-backdrop" (click)="cancelDelete()">
          <section class="confirm-modal" (click)="$event.stopPropagation()">
            <span class="eyebrow">Delete monster</span>
            <h2>{{ monster.name }}</h2>
            <p>This action permanently removes <code>{{ monster.id }}</code>.</p>
            @if (usageFor(monster).length) {
              <div class="delete-warning">
                It is still in {{ usageFor(monster).join(', ') }}, and the backend will block deletion.
              </div>
            }
            <div class="actions">
              <button type="button" class="secondary" (click)="cancelDelete()">Cancel</button>
              <button type="button" class="danger solid" [disabled]="deleting()" (click)="confirmDelete()">
                {{ deleting() ? 'Deleting...' : 'Delete permanently' }}
              </button>
            </div>
          </section>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .monster-workspace { display: grid; grid-template-columns: 310px minmax(560px, 1fr) 310px; gap: 14px; align-items: start; }
    .panel { border-radius: 8px; min-width: 0; padding: 14px; }
    .source-library, .authored-library {
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      height: calc(100vh - 82px);
      overflow: hidden;
      position: sticky;
      top: 68px;
    }
    .panel-head, .editor-head { align-items: center; display: flex; gap: 10px; justify-content: space-between; }
    h2, h3, p { margin: 0; }
    h2 { font-size: 20px; }
    h3 { color: #cfccd9; font-size: 11px; letter-spacing: .7px; margin-bottom: 11px; text-transform: uppercase; }
    .eyebrow { color: #2dd4bf; font-size: 9px; font-weight: 900; letter-spacing: 1.2px; text-transform: uppercase; }
    .count { background: #192d2a; border: 1px solid #2d6b63; border-radius: 4px; color: #58daca; font-size: 10px; padding: 5px 7px; }
    button { border: 1px solid transparent; border-radius: 5px; color: #dddbe8; font: inherit; }
    button:disabled { cursor: default; opacity: .45; }
    .primary, .secondary { min-height: 34px; padding: 0 13px; font-size: 10px; font-weight: 900; }
    .primary { background: #1db9aa; color: #061d1a; }
    .secondary { background: #1b1b28; border-color: #343448; }
    .compact { min-height: 31px; padding: 0 10px; }
    .actions { display: flex; gap: 7px; }
    input, select, textarea { background: #0d0d15; border: 1px solid #303043; border-radius: 5px; color: #eceaf3; font: inherit; outline: none; }
    input, select { box-sizing: border-box; min-height: 36px; padding: 0 9px; width: 100%; }
    textarea { box-sizing: border-box; padding: 8px 9px; resize: vertical; width: 100%; }
    input:focus, select:focus, textarea:focus { border-color: #2ab5a5; }
    label { color: #8e8ca0; display: flex; flex-direction: column; gap: 5px; font-size: 9px; font-weight: 800; }
    .search { margin: 12px 0 8px; }
    .filter-grid { display: grid; gap: 7px; grid-template-columns: 1fr 1fr; }
    .scope-tabs { background: #0d0d15; border: 1px solid #303043; border-radius: 5px; display: grid; grid-template-columns: 1fr 1fr; margin: 8px 0; overflow: hidden; }
    .scope-tabs button { background: transparent; border: 0; border-radius: 0; color: #858296; font-size: 8px; font-weight: 900; min-height: 30px; }
    .scope-tabs button + button { border-left: 1px solid #303043; }
    .scope-tabs button.active { background: #1b433d; color: #64ead6; }
    .appearance-list, .authored-list {
      align-content: start;
      display: grid;
      flex: 1 1 auto;
      gap: 5px;
      min-height: 0;
      overflow-y: auto;
      padding-right: 3px;
    }
    .appearance-row { align-items: center; background: #11111a; border-color: #29293a; display: grid; gap: 6px; grid-template-columns: 54px minmax(0, 1fr); min-width: 0; padding: 2px 5px 2px 0; text-align: left; }
    .appearance-row:hover, .appearance-row.active { background: #17201f; border-color: #2f8e82; }
    .row-copy, .row-copy strong, .row-copy small { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .row-copy strong { font-size: 10px; }
    .row-copy small { color: #858297; font-size: 8px; margin-top: 3px; }
    .badges { display: flex; gap: 3px; margin-top: 4px; }
    .badges i, .authored-main i { background: #173b35; border-radius: 3px; color: #58dbc9; font-size: 7px; font-style: normal; font-weight: 900; padding: 2px 4px; }
    .badges i.boss, .authored-main i.boss { background: #3b2818; color: #efad69; }
    .badges i.legacy { background: #252536; color: #aaa7bd; }
    .badges i.review, .badges i.missing { background: #3b2025; color: #f09aa5; }
    .pagination { align-items: center; border-top: 1px solid #29293a; display: flex; justify-content: space-between; margin-top: 8px; padding-top: 8px; }
    .pagination button { background: #171722; border-color: #303043; font-size: 8px; min-height: 27px; padding: 0 8px; }
    .pagination span { color: #77758b; font-size: 8px; }
    .editor { min-width: 0; }
    .editor-head { border-bottom: 1px solid #29293a; padding-bottom: 12px; }
    code { color: #858297; font-size: 9px; }
    .editor-head code { display: block; margin-top: 3px; }
    .base-label { color: #77758c; display: block; font-size: 9px; margin-top: 3px; }
    .status { border: 1px solid; border-radius: 5px; font-size: 10px; margin-top: 10px; padding: 8px 10px; }
    .status.ok { background: #102a25; border-color: #22675d; color: #55e5cf; }
    .status.err { background: #32191e; border-color: #6d303b; color: #ff9aa5; }
    .editor-grid { display: grid; gap: 12px; grid-template-columns: 1.08fr .92fr; margin-top: 12px; }
    .form-section { background: #11111a; border: 1px solid #29293a; border-radius: 6px; padding: 13px; }
    .identity { grid-column: 1; }
    .resistances { grid-column: 1 / -1; }
    .keywords { grid-column: 1 / -1; }
    .keyword-grid { grid-template-columns: repeat(auto-fit, minmax(92px, 1fr)); }
    .build { grid-column: 2; grid-row: 1 / span 2; }
    .result { grid-column: 1; }
    .identity-grid { display: grid; gap: 12px; grid-template-columns: 140px minmax(0, 1fr); }
    .preview { align-items: center; background: radial-gradient(circle, #2b2b3c, #12121a 68%); border: 1px solid #343448; border-radius: 6px; display: flex; flex-direction: column; justify-content: center; min-height: 190px; }
    .preview b { font-size: 10px; margin-top: 3px; max-width: 120px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .preview small { color: #77758c; font-size: 8px; margin-top: 2px; }
    .preview span { background: #192d2a; border-radius: 3px; color: #62dacb; font-size: 7px; margin-top: 5px; padding: 3px 5px; text-transform: uppercase; }
    .fields { display: grid; gap: 8px; }
    .appearance-hint { background: #171721; border-left: 2px solid #2d8c81; padding: 7px 8px; }
    .three, .four, .resistance-grid { display: grid; gap: 8px; }
    .three { grid-template-columns: repeat(3, 1fr); }
    .four { grid-template-columns: repeat(2, 1fr); margin-top: 8px; }
    .build > label { margin-top: 10px; }
    .behavior { background: #171721; border-left: 2px solid #7650a3; color: #9794a8; font-size: 9px; line-height: 1.45; margin-top: 7px; padding: 7px 8px; }
    .hint { color: #77758b; font-size: 8px; line-height: 1.45; margin-top: 7px; }
    .stat-cards { display: grid; gap: 6px; grid-template-columns: repeat(5, 1fr); }
    .stat-cards div { background: #171721; border: 1px solid #303043; border-radius: 4px; min-height: 58px; padding: 7px; }
    .stat-cards span, .stat-cards b, .stat-cards small { display: block; }
    .stat-cards span { color: #77758b; font-size: 7px; text-transform: uppercase; }
    .stat-cards b { color: #55decc; font-size: 16px; margin-top: 4px; }
    .stat-cards small { color: #6f6d80; font-size: 7px; margin-top: 2px; }
    .boss-note { background: #2e2016; border: 1px solid #684526; border-radius: 4px; color: #e3ae69; font-size: 8px; margin-top: 8px; padding: 7px; }
    .resistance-grid { grid-template-columns: repeat(7, minmax(108px, 1fr)); margin-top: 10px; }
    .resistance-toolbar { display: flex; justify-content: flex-end; margin-top: 8px; }
    .resistance-card { border: 1px solid #29293a; border-radius: 6px; padding: 8px; }
    .resistance-card.weak { background: #25141a; border-color: #70303d; }
    .resistance-card.resist { background: #10251f; border-color: #286f62; }
    .resistance-card > span { color: #cfccd9; font-size: 9px; }
    .suffix { display: grid; grid-template-columns: minmax(0, 1fr) 26px; }
    .suffix input { border-radius: 5px 0 0 5px; min-width: 0; }
    .suffix span { align-items: center; background: #1d1d29; border: 1px solid #303043; border-left: 0; border-radius: 0 5px 5px 0; color: #77758b; display: flex; justify-content: center; }
    .authored-library > label { margin-bottom: 8px; }
    .authored-card { background: #11111a; border: 1px solid #29293a; border-radius: 6px; overflow: hidden; }
    .authored-card.active { background: #17201f; border-color: #2f8e82; }
    .authored-main { align-items: center; background: transparent; border: 0; border-radius: 0; display: grid; gap: 6px; grid-template-columns: 56px minmax(0, 1fr); padding: 3px 6px 3px 0; text-align: left; width: 100%; }
    .authored-main span, .authored-main strong, .authored-main small { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .authored-main strong { font-size: 10px; }
    .authored-main small { color: #858297; font-size: 8px; margin: 3px 0 4px; }
    .authored-main i.elite { background: #2e2040; color: #c9a0ef; }
    .usage { background: #191724; border-top: 1px solid #29293a; color: #a995c4; font-size: 7px; overflow: hidden; padding: 5px 7px; text-overflow: ellipsis; white-space: nowrap; }
    .authored-card footer { border-top: 1px solid #29293a; display: grid; grid-template-columns: repeat(3, 1fr); }
    .authored-card footer button { background: #171722; border: 0; border-radius: 0; color: #9a97aa; font-size: 8px; min-height: 27px; }
    .authored-card footer button + button { border-left: 1px solid #29293a; }
    .authored-card footer button:hover { color: #eceaf3; }
    .danger { color: #ef8b98 !important; }
    .danger.solid { background: #7b303b; border-color: #a84957; color: #fff0f2 !important; min-height: 34px; padding: 0 12px; }
    .empty { color: #77758c; font-size: 9px; padding: 30px 12px; text-align: center; }
    .authored-empty { line-height: 1.5; }
    .loading { color: #77758b; padding: 70px; text-align: center; }
    .modal-backdrop { align-items: center; background: rgb(4 4 8 / 78%); display: flex; inset: 0; justify-content: center; position: fixed; z-index: 1000; }
    .confirm-modal { background: #12121b; border: 1px solid #3b3b50; border-radius: 8px; box-shadow: 0 20px 70px #000; max-width: 430px; padding: 20px; width: calc(100% - 32px); }
    .confirm-modal h2 { margin-top: 4px; }
    .confirm-modal p { color: #9996a8; font-size: 10px; line-height: 1.5; margin: 10px 0; }
    .confirm-modal .actions { justify-content: flex-end; margin-top: 16px; }
    .delete-warning { background: #32191e; border: 1px solid #6d303b; border-radius: 5px; color: #ff9aa5; font-size: 9px; line-height: 1.45; padding: 8px; }
    @media (max-width: 1450px) {
      .monster-workspace { grid-template-columns: 280px minmax(520px, 1fr) 280px; }
    }
    @media (max-width: 1180px) {
      .monster-workspace { grid-template-columns: 1fr 1fr; }
      .editor { grid-column: 1 / -1; grid-row: 1; }
      .source-library, .authored-library { height: 500px; position: static; }
      .source-library { grid-column: 1; grid-row: 2; }
      .authored-library { grid-column: 2; grid-row: 2; }
    }
    @media (max-width: 760px) {
      .monster-workspace, .editor-grid, .identity-grid, .three, .stat-cards, .resistance-grid { grid-template-columns: 1fr; }
      .editor, .source-library, .authored-library, .identity, .build, .result, .resistances { grid-column: 1; grid-row: auto; }
      .four, .filter-grid { grid-template-columns: repeat(2, 1fr); }
      .editor-head { align-items: stretch; flex-direction: column; }
    }
  `],
})
export class MonsterEditor implements OnInit {
  readonly catalogChanged = output<void>();
  readonly metadata = signal<MonsterAuthoringMetadata | null>(null);
  readonly authored = signal<MonsterDefinition[]>([]);
  readonly tiers = signal<DungeonTier[]>([]);
  readonly draft = signal<MonsterDefinition>(this.emptyDefinition());
  readonly selectedId = signal('');
  readonly selectedAppearance = signal<MonsterAppearance | null>(null);
  readonly availableOutfits = signal<Set<number>>(new Set());

  readonly appearanceSearch = signal('');
  readonly familyFilter = signal('');
  readonly kindFilter = signal<AppearanceKindFilter>('all');
  readonly scopeFilter = signal<AppearanceScopeFilter>('all');
  readonly appearancePage = signal(1);
  readonly authoredSearch = signal('');
  readonly authoredRankFilter = signal<AuthoredRankFilter>('all');
  readonly authoredPage = signal(1);

  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly pendingDelete = signal<MonsterDefinition | null>(null);
  readonly status = signal<{ kind: 'ok' | 'err'; msg: string } | null>(null);

  readonly ranks = [
    { id: 'common', name: 'Common' },
    { id: 'elite', name: 'Elite' },
    { id: 'boss', name: 'Boss' },
  ] as const;
  readonly modifiers = [
    { key: 'hpMultiplier', label: 'HP' },
    { key: 'damageMultiplier', label: 'Damage' },
    { key: 'speedMultiplier', label: 'Speed' },
    { key: 'cadenceMultiplier', label: 'Cadence' },
  ] as const;
  // G-08B: card keyword labels (same taxonomy as G-04).
  readonly keywordLabels: Record<string, string> = {
    sin: 'Sin', combo: 'Discipline', curse: 'Curse', burn: 'Burn',
    charge: 'Charge', frost: 'Frost', prey: 'Prey', posture: 'Posture',
  };

  readonly appearanceFamilies = computed(() => {
    const families = this.metadata()?.appearances.map((entry) => entry.bestiaryClass).filter(Boolean) ?? [];
    return [...new Set(families)].sort((a, b) =>
      a === 'Unclassified' ? 1 : b === 'Unclassified' ? -1 : a.localeCompare(b));
  });

  readonly filteredAppearances = computed(() => {
    const meta = this.metadata();
    if (!meta) return [];
    const query = this.appearanceSearch().trim().toLocaleLowerCase();
    return meta.appearances.filter((entry) => {
      const text = `${entry.name} ${entry.bestiaryClass} ${entry.outfit.lookType} ${entry.source}`.toLocaleLowerCase();
      return (!query || text.includes(query))
        && (!this.familyFilter() || entry.bestiaryClass === this.familyFilter())
        && (this.kindFilter() === 'all' || entry.kind === this.kindFilter())
        && (this.scopeFilter() === 'all' || entry.legacyImported);
    });
  });

  readonly appearancePageCount = computed(() =>
    Math.max(1, Math.ceil(this.filteredAppearances().length / APPEARANCE_PAGE_SIZE)));

  readonly pagedAppearances = computed(() => {
    const page = Math.min(this.appearancePage(), this.appearancePageCount());
    const start = (page - 1) * APPEARANCE_PAGE_SIZE;
    return this.filteredAppearances().slice(start, start + APPEARANCE_PAGE_SIZE);
  });

  readonly appearanceCards = computed(() => this.pagedAppearances().map((appearance) => ({
    appearance,
    creature: this.fromAppearance(appearance),
  })));

  readonly filteredAuthored = computed(() => {
    const query = this.authoredSearch().trim().toLocaleLowerCase();
    const rankOrder = { common: 0, elite: 1, boss: 2 };
    return this.authored()
      .filter((monster) => {
        const text = `${monster.name} ${monster.bestiaryClass} ${monster.id}`.toLocaleLowerCase();
        return (!query || text.includes(query))
          && (this.authoredRankFilter() === 'all' || monster.rank === this.authoredRankFilter());
      })
      .sort((a, b) => rankOrder[a.rank] - rankOrder[b.rank] || a.name.localeCompare(b.name));
  });

  readonly authoredPageCount = computed(() =>
    Math.max(1, Math.ceil(this.filteredAuthored().length / AUTHORED_PAGE_SIZE)));

  readonly pagedAuthored = computed(() => {
    const page = Math.min(this.authoredPage(), this.authoredPageCount());
    const start = (page - 1) * AUTHORED_PAGE_SIZE;
    return this.filteredAuthored().slice(start, start + AUTHORED_PAGE_SIZE);
  });

  readonly authoredCards = computed(() => this.pagedAuthored().map((monster) => ({
    monster,
    creature: this.fromDefinition(monster),
    usages: this.usageFor(monster),
  })));

  readonly previewCreature = computed<MonsterCatalogEntry>(() => this.fromDefinition(this.draft()));

  readonly resolvedStats = computed(() => {
    const line = this.metadata()?.statLines[`${this.draft().powerTier}:${this.draft().rank}`];
    if (!line) return { health: 0, damage: 0, armor: 0, speed: 0, experience: 0 };
    return {
      health: Math.round(line.health * this.draft().hpMultiplier),
      damage: Math.round(line.damage * this.draft().damageMultiplier),
      armor: line.armor,
      speed: Math.round(line.speed * this.draft().speedMultiplier),
      experience: line.experience,
    };
  });

  readonly behaviorDescription = computed(() =>
    this.metadata()?.behaviors.find((entry) => entry.id === this.draft().behaviorId)?.description ?? '');

  constructor(
    private readonly api: ApiService,
    private readonly assets: AssetsService,
  ) {}

  async ngOnInit(): Promise<void> {
    try {
      const [metadata, authored, tiers] = await Promise.all([
        this.api.getMonsterAuthoringMetadata(),
        this.api.getAuthoredMonsters(),
        this.api.getAdminTiers(),
        this.api.loadCatalog(),
        this.assets.load(),
      ]);
      this.metadata.set(metadata);
      this.authored.set(authored);
      this.tiers.set(tiers);
      this.availableOutfits.set(new Set(this.assets.ids('outfits')));
      this.newMonster();
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    }
  }

  label(rank: string): string {
    return this.ranks.find((entry) => entry.id === rank)?.name ?? rank;
  }

  hasOutfit(lookType: number): boolean {
    return this.availableOutfits().has(lookType);
  }

  setAppearanceSearch(value: string): void {
    this.appearanceSearch.set(value);
    this.appearancePage.set(1);
  }

  setFamilyFilter(value: string): void {
    this.familyFilter.set(value);
    this.appearancePage.set(1);
  }

  setKindFilter(value: AppearanceKindFilter): void {
    this.kindFilter.set(value);
    this.appearancePage.set(1);
  }

  setScopeFilter(value: AppearanceScopeFilter): void {
    this.scopeFilter.set(value);
    this.appearancePage.set(1);
  }

  changeAppearancePage(delta: number): void {
    this.appearancePage.set(Math.max(1, Math.min(
      this.appearancePageCount(),
      this.appearancePage() + delta,
    )));
  }

  setAuthoredSearch(value: string): void {
    this.authoredSearch.set(value);
    this.authoredPage.set(1);
  }

  setAuthoredRankFilter(value: AuthoredRankFilter): void {
    this.authoredRankFilter.set(value);
    this.authoredPage.set(1);
  }

  changeAuthoredPage(delta: number): void {
    this.authoredPage.set(Math.max(1, Math.min(
      this.authoredPageCount(),
      this.authoredPage() + delta,
    )));
  }

  newMonster(): void {
    const definition = this.emptyDefinition();
    const appearance = this.selectedAppearance() ?? this.metadata()?.appearances[0] ?? null;
    this.draft.set(appearance ? this.withAppearance(definition, appearance, true) : definition);
    this.selectedAppearance.set(appearance);
    this.selectedId.set('');
    this.status.set(null);
  }

  chooseAppearance(appearance: MonsterAppearance): void {
    const previousAppearance = this.selectedAppearance();
    const isNew = !this.draft().id;
    this.draft.update((monster) => {
      const next = this.withAppearance(monster, appearance, isNew);
      if (!isNew || !previousAppearance) return next;
      return {
        ...next,
        name: monster.name === `${previousAppearance.name} Echo`
          ? `${appearance.name} Echo`
          : next.name,
        description: monster.description === `Kaezan creature with a visual inspired by ${previousAppearance.name}.`
          ? `Kaezan creature with a visual inspired by ${appearance.name}.`
          : next.description,
      };
    });
    this.selectedAppearance.set(appearance);
    this.status.set({
      kind: 'ok',
      msg: this.draft().id
        ? `Visual updated to ${appearance.name}. Save to confirm.`
        : `${appearance.name} visual selected. Configure the Kaezan monster in the center.`,
    });
  }

  editMonster(monster: MonsterDefinition): void {
    const copy = structuredClone(monster);
    const appearance = this.findAppearance(copy);
    if (appearance && !copy.appearanceId) copy.appearanceId = appearance.id;
    this.draft.set(copy);
    this.selectedId.set(copy.id);
    this.selectedAppearance.set(appearance);
    this.status.set(null);
  }

  duplicateMonster(monster: MonsterDefinition): void {
    this.editMonster(monster);
    this.duplicateCurrent();
  }

  duplicateCurrent(): void {
    this.draft.update((monster) => ({
      ...structuredClone(monster),
      id: '',
      name: monster.name ? `${monster.name} Echo` : 'New monster',
    }));
    this.selectedId.set('');
    this.status.set({ kind: 'ok', msg: 'Copy opened as a new monster. Adjust the name and save.' });
  }

  patch(patch: Partial<MonsterDefinition>): void {
    this.draft.update((monster) => ({ ...monster, ...patch }));
    this.status.set(null);
  }

  patchNum(field: 'powerTier', value: string): void {
    this.patch({ [field]: Math.max(1, Math.min(5, Math.round(+value || 1))) });
  }

  modifierValue(key: typeof this.modifiers[number]['key']): number {
    return this.draft()[key];
  }

  setModifier(key: typeof this.modifiers[number]['key'], value: string): void {
    const meta = this.metadata();
    if (!meta) return;
    this.patch({ [key]: Math.max(meta.modifierMin, Math.min(meta.modifierMax, +value || 1)) });
  }

  applyPreset(id: string): void {
    const preset = this.metadata()?.presets.find((entry) => entry.id === id);
    if (!preset) return;
    this.patch({
      statPresetId: preset.id,
      hpMultiplier: preset.hpMultiplier,
      damageMultiplier: preset.damageMultiplier,
      speedMultiplier: preset.speedMultiplier,
      cadenceMultiplier: preset.cadenceMultiplier,
    });
  }

  resistance(element: string): number {
    return this.draft().resistances[element] ?? 0;
  }

  resistanceMagnitude(element: string): number {
    return Math.abs(this.resistance(element));
  }

  maxResistanceMagnitude(): number {
    const meta = this.metadata();
    return Math.max(Math.abs(meta?.resistanceMin ?? -100), Math.abs(meta?.resistanceMax ?? 100));
  }

  resistanceKind(element: string): ResistanceKind {
    const value = this.resistance(element);
    return value < 0 ? 'weak' : value > 0 ? 'resist' : 'neutral';
  }

  resistanceGrade(element: string): ResistanceGrade {
    const magnitude = Math.abs(this.resistance(element));
    if (magnitude >= 15) return 'high';
    if (magnitude >= 10) return 'moderate';
    return 'low';
  }

  setResistanceKind(element: string, kind: ResistanceKind): void {
    if (kind === 'neutral') {
      const next = { ...this.draft().resistances };
      delete next[element];
      this.patch({ resistances: next });
      return;
    }
    const magnitude = this.resistanceMagnitude(element) || 5;
    this.patch({
      resistances: {
        ...this.draft().resistances,
        [element]: kind === 'weak' ? -magnitude : magnitude,
      },
    });
  }

  setResistanceGrade(element: string, grade: ResistanceGrade): void {
    const kind = this.resistanceKind(element);
    if (kind === 'neutral') return;
    this.applyResistance(element, kind, grade);
  }

  setResistance(element: string, value: string): void {
    const meta = this.metadata();
    if (!meta) return;
    const resistance = Math.max(meta.resistanceMin, Math.min(meta.resistanceMax, +value || 0));
    this.patch({ resistances: { ...this.draft().resistances, [element]: resistance } });
  }

  setResistanceAmount(element: string, value: string): void {
    const meta = this.metadata();
    if (!meta) return;
    const kind = this.resistanceKind(element);
    if (kind === 'neutral') return;
    const max = this.maxResistanceMagnitude();
    const magnitude = Math.max(0, Math.min(max, +value || 0));
    this.patch({
      resistances: {
        ...this.draft().resistances,
        [element]: kind === 'weak' ? -magnitude : magnitude,
      },
    });
  }

  clearResistances(): void {
    this.patch({ resistances: {} });
  }

  // G-08B: keyword interaction — percent the mob resists from each card keyword (negative amplifies).
  keywordResist(tag: string): number {
    return this.draft().keywordResistances?.[tag] ?? 0;
  }

  keywordLabel(tag: string): string {
    return this.keywordLabels[tag] ?? tag;
  }

  setKeywordResist(tag: string, value: string): void {
    const meta = this.metadata();
    if (!meta) return;
    const clamped = Math.max(meta.keywordResistMin, Math.min(meta.keywordResistMax, Math.round(+value || 0)));
    const next = { ...(this.draft().keywordResistances ?? {}) };
    if (clamped === 0) delete next[tag];
    else next[tag] = clamped;
    this.patch({ keywordResistances: next });
  }

  clearKeywordResistances(): void {
    this.patch({ keywordResistances: {} });
  }

  private applyResistance(element: string, kind: Exclude<ResistanceKind, 'neutral'>, grade: ResistanceGrade): void {
    const value = grade === 'high' ? 15 : grade === 'moderate' ? 10 : 5;
    this.patch({
      resistances: {
        ...this.draft().resistances,
        [element]: kind === 'weak' ? -value : value,
      },
    });
  }

  usageFor(monster: MonsterDefinition): string[] {
    const usages: string[] = [];
    for (const tier of this.tiers()) {
      if (tier.commonMobs.some((reference) => this.matches(reference, monster)))
        usages.push(`T${tier.tier} common`);
      if (tier.eliteMobs.some((reference) => this.matches(reference, monster)))
        usages.push(`T${tier.tier} elites`);
      if (this.matches(tier.boss, monster))
        usages.push(`T${tier.tier} boss`);
    }
    return usages;
  }

  requestDelete(monster: MonsterDefinition): void {
    this.pendingDelete.set(monster);
  }

  cancelDelete(): void {
    if (!this.deleting()) this.pendingDelete.set(null);
  }

  async confirmDelete(): Promise<void> {
    const monster = this.pendingDelete();
    if (!monster) return;
    this.deleting.set(true);
    try {
      await this.api.deleteAuthoredMonster(monster.id);
      this.authored.update((entries) => entries.filter((entry) => entry.id !== monster.id));
      if (this.selectedId() === monster.id) this.newMonster();
      this.pendingDelete.set(null);
      this.catalogChanged.emit();
      this.status.set({ kind: 'ok', msg: `${monster.name} was deleted.` });
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
        ? await this.api.updateAuthoredMonster(this.draft())
        : await this.api.createAuthoredMonster(this.draft());
      this.authored.update((entries) => [
        ...entries.filter((entry) => entry.id !== saved.id),
        saved,
      ]);
      this.draft.set(structuredClone(saved));
      this.selectedId.set(saved.id);
      this.selectedAppearance.set(this.findAppearance(saved));
      this.catalogChanged.emit();
      this.status.set({ kind: 'ok', msg: 'Monster saved. It can already be used in upcoming runs.' });
    } catch (error) {
      this.status.set({ kind: 'err', msg: (error as Error).message });
    } finally {
      this.saving.set(false);
    }
  }

  private emptyDefinition(): MonsterDefinition {
    return {
      id: '',
      name: '',
      description: '',
      outfit: { lookType: 0, head: 0, body: 0, legs: 0, feet: 0, addons: 0 },
      corpse: 0,
      powerTier: 1,
      rank: 'common',
      behaviorId: 'bruiser',
      elementId: 'physical',
      statPresetId: 'balanced',
      hpMultiplier: 1,
      damageMultiplier: 1,
      speedMultiplier: 1,
      cadenceMultiplier: 1,
      bestiaryClass: 'Authored',
      resistances: {},
      keywordResistances: {},
      appearanceId: '',
      enabled: true,
    };
  }

  private withAppearance(
    monster: MonsterDefinition,
    appearance: MonsterAppearance,
    applyDefaults: boolean,
  ): MonsterDefinition {
    const replaceClass = applyDefaults
      || monster.bestiaryClass === 'Authored'
      || monster.bestiaryClass === 'Unclassified';
    return {
      ...monster,
      name: applyDefaults && !monster.name ? `${appearance.name} Echo` : monster.name,
      description: applyDefaults && !monster.description
        ? `Kaezan creature with a visual inspired by ${appearance.name}.`
        : monster.description,
      outfit: structuredClone(appearance.outfit),
      corpse: appearance.corpse,
      appearanceId: appearance.id,
      rank: applyDefaults && appearance.kind === 'boss' ? 'boss' : monster.rank,
      bestiaryClass: replaceClass ? appearance.bestiaryClass : monster.bestiaryClass,
    };
  }

  private findAppearance(monster: MonsterDefinition): MonsterAppearance | null {
    const appearances = this.metadata()?.appearances ?? [];
    return appearances.find((entry) => entry.id === monster.appearanceId)
      ?? appearances.find((entry) => entry.outfit.lookType === monster.outfit.lookType)
      ?? null;
  }

  private fromAppearance(appearance: MonsterAppearance): MonsterCatalogEntry {
    return {
      id: appearance.id,
      name: appearance.name,
      description: '',
      health: 0,
      experience: 0,
      isBoss: appearance.kind === 'boss',
      bestiaryClass: appearance.bestiaryClass,
      origin: 'CANARY',
      bossRace: null,
      corpse: appearance.corpse,
      outfit: appearance.outfit,
      loot: [],
      source: 'legacy',
      rank: 'legacy',
      element: 'physical',
      behaviorId: 'legacy',
      statPresetId: 'legacy',
      hpMultiplier: 1,
      damageMultiplier: 1,
      speedMultiplier: 1,
      cadenceMultiplier: 1,
      powerTier: 0,
      resistances: {},
    };
  }

  private fromDefinition(monster: MonsterDefinition): MonsterCatalogEntry {
    const line = this.metadata()?.statLines[`${monster.powerTier}:${monster.rank}`];
    return {
      id: monster.id || 'preview',
      name: monster.name || 'New monster',
      description: monster.description,
      health: line ? Math.round(line.health * monster.hpMultiplier) : 0,
      experience: line?.experience ?? 0,
      isBoss: monster.rank === 'boss',
      bestiaryClass: monster.bestiaryClass,
      origin: 'KAEZAN',
      bossRace: null,
      corpse: monster.corpse,
      outfit: monster.outfit,
      loot: [],
      source: 'authored',
      rank: monster.rank,
      element: monster.elementId,
      behaviorId: monster.behaviorId,
      statPresetId: monster.statPresetId,
      hpMultiplier: monster.hpMultiplier,
      damageMultiplier: monster.damageMultiplier,
      speedMultiplier: monster.speedMultiplier,
      cadenceMultiplier: monster.cadenceMultiplier,
      powerTier: monster.powerTier,
      resistances: monster.resistances,
    };
  }

  private matches(reference: string, monster: MonsterDefinition): boolean {
    return reference.toLocaleLowerCase() === monster.id.toLocaleLowerCase()
      || reference.toLocaleLowerCase() === monster.name.toLocaleLowerCase();
  }
}
