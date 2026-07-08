import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { ItemIcon } from '../../core/item-icon';
import { BiomeDef, BiomeRow, MapDto, MapPreviewRequest, TilesetSummaryDto } from '../../core/types';

const TILE = 32;

export interface MapPreviewDrawOptions {
  zoom: 1 | 2;
  showBlocked: boolean;
  showRooms: boolean;
}

export function drawMapPreviewCanvas(
  canvas: HTMLCanvasElement,
  map: MapDto,
  assets: AssetsService,
  options: MapPreviewDrawOptions,
): void {
  const zoom = options.zoom;
  const tile = TILE * zoom;
  canvas.width = map.w * tile;
  canvas.height = map.h * tile;

  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  ctx.clearRect(0, 0, canvas.width, canvas.height);
  const draw = (id: number, x: number, y: number): void => {
    if (id) assets.drawObject(ctx, id, x * tile, y * tile, zoom, x, y, 0);
  };

  for (let y = 0; y < map.h; y++) {
    for (let x = 0; x < map.w; x++) {
      const i = y * map.w + x;
      for (const id of map.flat[i]) draw(id, x, y);
    }
  }

  for (let y = 0; y < map.h; y++) {
    for (let x = 0; x < map.w; x++) {
      for (const id of map.tall[y * map.w + x]) draw(id, x, y);
    }
  }

  if (options.showBlocked) {
    ctx.fillStyle = 'rgba(205, 58, 74, 0.22)';
    for (let y = 0; y < map.h; y++) {
      for (let x = 0; x < map.w; x++) {
        if (map.blocked[y * map.w + x]) ctx.fillRect(x * tile, y * tile, tile, tile);
      }
    }
  }

  if (options.showRooms) {
    ctx.font = `${Math.max(9, 10 * zoom)}px Sora, sans-serif`;
    ctx.lineWidth = Math.max(1, zoom);
    for (const room of map.rooms) {
      const prefab = room.role.toLowerCase().includes('prefab');
      ctx.strokeStyle = prefab ? 'rgba(232, 169, 60, 0.95)' : 'rgba(45, 212, 191, 0.9)';
      ctx.fillStyle = prefab ? 'rgba(232, 169, 60, 0.95)' : 'rgba(45, 212, 191, 0.95)';
      ctx.strokeRect(room.x * tile + 0.5, room.y * tile + 0.5, room.w * tile - 1, room.h * tile - 1);
      ctx.fillText(room.role, room.x * tile + 4, room.y * tile + Math.max(12, 13 * zoom));
    }
  }
}

export function cloneBiomeDef(def: BiomeDef): BiomeDef {
  return {
    ...def,
    ground: [...def.ground],
    bossGround: [...def.bossGround],
    decor: [...def.decor],
    accent: [...def.accent],
    atmosphere: { ...def.atmosphere },
    wallSet: def.wallSet ? { tiles: { ...def.wallSet.tiles } } : null,
    groundFamilies: def.groundFamilies ? [...def.groundFamilies] : null,
  };
}

export function cloneBiomeRow(row: BiomeRow): BiomeRow {
  return { ...row, def: cloneBiomeDef(row.def) };
}

export function replaceBiomeRow(rows: BiomeRow[], draft: BiomeRow): BiomeRow[] {
  return rows.map((row) => (row.tier === draft.tier ? cloneBiomeRow(draft) : row));
}

export function previewRequestFromDraft(
  tier: number,
  seed: number,
  bossFloor: boolean,
  draft: BiomeRow,
): MapPreviewRequest {
  return {
    tier,
    seed,
    floorIndex: bossFloor ? 1 : 0,
    bossFloor,
    biome: cloneBiomeDef(draft.def),
  };
}

function clampChance(value: number): number {
  if (Number.isNaN(value)) return 0;
  return Math.max(0, Math.min(0.2, value));
}

function parseItemId(value: string): number | null {
  const parsed = Math.trunc(+value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function toHex(value: number): string {
  return Math.max(0, Math.min(255, Math.trunc(value))).toString(16).padStart(2, '0');
}

function rgbToHex(r: number, g: number, b: number): string {
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

function hexToRgb(hex: string): { r: number; g: number; b: number } | null {
  const match = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!match) return null;
  const raw = match[1];
  return {
    r: Number.parseInt(raw.slice(0, 2), 16),
    g: Number.parseInt(raw.slice(2, 4), 16),
    b: Number.parseInt(raw.slice(4, 6), 16),
  };
}

@Component({
  selector: 'app-map-lab',
  standalone: true,
  imports: [ItemIcon],
  template: `
    <section class="map-lab">
      @if (status(); as st) {
        <div class="status" [class.ok]="st.kind === 'ok'" [class.err]="st.kind === 'err'">{{ st.msg }}</div>
      }

      <div class="lab-head">
        <div>
          <span class="eyebrow">Map Lab</span>
          <h2>Seeded floor preview</h2>
        </div>
        <button class="primary" type="button" [disabled]="busy()" (click)="generate()">
          {{ generating() ? 'Generating...' : 'Generate' }}
        </button>
      </div>

      <div class="lab-grid">
        <aside class="panel controls">
          <div class="control-row tiers">
            @for (row of biomes(); track row.tier) {
              <button type="button" [class.active]="selectedTier() === row.tier" (click)="selectTier(row.tier)">
                T{{ row.tier }}
              </button>
            }
          </div>

          <label>Seed
            <div class="seed-row">
              <input type="number" [value]="seed()" (input)="setSeed($any($event.target).value)" />
              <button class="secondary" type="button" [disabled]="busy()" (click)="rerollSeed()">Reroll</button>
            </div>
          </label>

          <label>Floor
            <select [value]="bossFloor() ? 'boss' : 'normal'" (change)="setFloor($any($event.target).value)">
              <option value="normal">Normal</option>
              <option value="boss">Boss</option>
            </select>
          </label>

          <label>Zoom
            <div class="control-row">
              <button type="button" [class.active]="zoom() === 1" (click)="setZoom(1)">1x</button>
              <button type="button" [class.active]="zoom() === 2" (click)="setZoom(2)">2x</button>
            </div>
          </label>

          <div class="toggles">
            <label class="check">
              <input type="checkbox" [checked]="showBlocked()" (change)="toggleBlocked($any($event.target).checked)" />
              <span>Blocked</span>
            </label>
            <label class="check">
              <input type="checkbox" [checked]="showRooms()" (change)="toggleRooms($any($event.target).checked)" />
              <span>Rooms</span>
            </label>
          </div>

          @if (selectedBiome(); as biome) {
            <section class="summary">
              <span class="eyebrow">Biome</span>
              <strong>{{ biome.name }}</strong>
              <span>{{ biome.def.atmosphere.name }}</span>
              <small>Wall {{ biome.def.wallFamily || 'legacy' }}</small>
              <small>Ground {{ (biome.def.groundFamilies ?? []).join(', ') || 'legacy' }}</small>
            </section>
          }

          @if (tilesets(); as ts) {
            <section class="summary">
              <span class="eyebrow">Tilesets</span>
              <strong>{{ ts.families.length }} families</strong>
              <span>{{ ts.borderSets.length }} border sets</span>
              <span>{{ wallCoverage(ts) }}</span>
            </section>
          }
        </aside>

        <section class="panel preview">
          <div class="preview-head">
            @if (map(); as m) {
              <span>{{ m.w }} x {{ m.h }} tiles</span>
              <span>{{ m.rooms.length }} rooms</span>
              <span>{{ m.biome.name }}</span>
            } @else {
              <span>No preview generated yet.</span>
            }
          </div>
          <div class="canvas-shell">
            @if (loading()) {
              <div class="empty">Loading Map Lab...</div>
            }
            <canvas #canvas aria-label="Generated map preview"></canvas>
          </div>
        </section>

        <aside class="panel editor">
          <div class="editor-head">
            <div>
              <span class="eyebrow">Biome preset</span>
              <h3>T{{ selectedTier() }} draft</h3>
            </div>
            <button class="secondary" type="button" [disabled]="busy()" (click)="resetRow()">Reset row</button>
          </div>

          @if (draftBiome(); as draft) {
            <label>Name
              <input type="text" [value]="draft.name" (input)="updateDraftName($any($event.target).value)" />
            </label>

            <label>Wall family
              <select [value]="draft.def.wallFamily" (change)="updateWallFamily($any($event.target).value)">
                @for (family of mountainFamilies(); track family) {
                  <option [value]="family">{{ family }}</option>
                }
              </select>
            </label>

            <section class="field-block">
              <div class="field-title">
                <span>Ground families</span>
                <small>{{ (draft.def.groundFamilies ?? []).length }}/3</small>
              </div>
              <div class="chip-list">
                @for (family of draft.def.groundFamilies ?? []; track family; let i = $index) {
                  <span class="chip family">
                    {{ family }}
                    <button type="button" title="Move up" [disabled]="i === 0" (click)="moveGroundFamily(i, -1)">^</button>
                    <button type="button" title="Move down" [disabled]="i === (draft.def.groundFamilies?.length ?? 0) - 1" (click)="moveGroundFamily(i, 1)">v</button>
                    <button type="button" title="Remove" [disabled]="(draft.def.groundFamilies?.length ?? 0) <= 1" (click)="removeGroundFamily(family)">x</button>
                  </span>
                }
              </div>
              <div class="add-row">
                <select [value]="groundFamilyInput()" (change)="groundFamilyInput.set($any($event.target).value)">
                  <option value="">Add ground...</option>
                  @for (family of availableGroundFamilies(); track family) {
                    <option [value]="family">{{ family }}</option>
                  }
                </select>
                <button class="secondary" type="button" [disabled]="!canAddGroundFamily()" (click)="addGroundFamily()">Add</button>
              </div>
            </section>

            <div class="slider-grid">
              <label>Decor density
                <input type="range" min="0" max="0.2" step="0.005" [value]="draft.def.decorChance" (input)="updateChance('decorChance', $any($event.target).value)" />
                <output>{{ formatChance(draft.def.decorChance) }}</output>
              </label>
              <label>Accent density
                <input type="range" min="0" max="0.2" step="0.005" [value]="draft.def.accentChance" (input)="updateChance('accentChance', $any($event.target).value)" />
                <output>{{ formatChance(draft.def.accentChance) }}</output>
              </label>
            </div>

            <section class="field-block">
              <div class="field-title">
                <span>Decor palette</span>
                <small>{{ draft.def.decor.length }} ids</small>
              </div>
              <div class="chip-list sprites">
                @for (id of draft.def.decor; track $index) {
                  <span class="chip sprite">
                    <app-item-icon [itemId]="id" [size]="32" />
                    <span>{{ id }}</span>
                    <button type="button" title="Remove" (click)="removePaletteItem('decor', id)">x</button>
                  </span>
                }
              </div>
              <div class="add-row">
                <input type="number" placeholder="Item id" [value]="decorInput()" (input)="decorInput.set($any($event.target).value)" />
                <button class="secondary" type="button" (click)="addPaletteItem('decor')">Add</button>
              </div>
            </section>

            <section class="field-block">
              <div class="field-title">
                <span>Accent palette</span>
                <small>{{ draft.def.accent.length }} ids</small>
              </div>
              <div class="chip-list sprites">
                @for (id of draft.def.accent; track $index) {
                  <span class="chip sprite">
                    <app-item-icon [itemId]="id" [size]="32" />
                    <span>{{ id }}</span>
                    <button type="button" title="Remove" (click)="removePaletteItem('accent', id)">x</button>
                  </span>
                }
              </div>
              <div class="add-row">
                <input type="number" placeholder="Item id" [value]="accentInput()" (input)="accentInput.set($any($event.target).value)" />
                <button class="secondary" type="button" (click)="addPaletteItem('accent')">Add</button>
              </div>
            </section>

            <section class="field-block">
              <div class="field-title">
                <span>Atmosphere</span>
              </div>
              <label>Atmosphere name
                <input type="text" [value]="draft.def.atmosphere.name" (input)="updateAtmosphereText('name', $any($event.target).value)" />
              </label>
              <div class="color-grid">
                <label>Tint
                  <input type="color" [value]="tintColor()" (input)="updateAtmosphereColor('tint', $any($event.target).value)" />
                </label>
                <label>Fog
                  <input type="color" [value]="fogColor()" (input)="updateAtmosphereColor('fog', $any($event.target).value)" />
                </label>
                <label>Particles
                  <input type="color" [value]="particleColor()" (input)="updateAtmosphereColor('particle', $any($event.target).value)" />
                </label>
              </div>
              <div class="numeric-grid">
                <label>Tint strength
                  <input type="number" min="0" max="1" step="0.01" [value]="draft.def.atmosphere.tintStrength" (input)="updateAtmosphereNumber('tintStrength', $any($event.target).value)" />
                </label>
                <label>Fog strength
                  <input type="number" min="0" max="1" step="0.01" [value]="draft.def.atmosphere.fogStrength" (input)="updateAtmosphereNumber('fogStrength', $any($event.target).value)" />
                </label>
                <label>Vignette
                  <input type="number" min="0" max="1" step="0.01" [value]="draft.def.atmosphere.vignette" (input)="updateAtmosphereNumber('vignette', $any($event.target).value)" />
                </label>
                <label>Particle density
                  <input type="number" min="0" max="1" step="0.01" [value]="draft.def.atmosphere.particleDensity" (input)="updateAtmosphereNumber('particleDensity', $any($event.target).value)" />
                </label>
                <label>Particle drift
                  <input type="number" min="0" max="1" step="0.01" [value]="draft.def.atmosphere.particleDrift" (input)="updateAtmosphereNumber('particleDrift', $any($event.target).value)" />
                </label>
              </div>
            </section>

            <div class="editor-actions">
              <button class="secondary" type="button" [disabled]="busy()" (click)="previewDraft()">Preview draft</button>
              <button class="primary" type="button" [disabled]="busy()" (click)="saveDraft()">Save</button>
            </div>
          } @else {
            <div class="empty editor-empty">Load biomes to edit a preset.</div>
          }
        </aside>
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .map-lab { max-width: 1480px; }
    .status { border: 1px solid; border-radius: 6px; font-size: 12px; margin-bottom: 12px; padding: 9px 11px; }
    .status.ok { background: #102a25; border-color: #22675d; color: #55e5cf; }
    .status.err { background: #32191e; border-color: #6d303b; color: #ff9aa5; }
    .lab-head { align-items: flex-start; border-bottom: 1px solid #29293a; display: flex; gap: 16px; justify-content: space-between; margin-bottom: 14px; padding-bottom: 14px; }
    .lab-head h2 { font-size: 21px; margin: 2px 0 0; }
    .eyebrow { color: #2dd4bf; display: block; font-size: 9px; font-weight: 900; letter-spacing: 1.3px; text-transform: uppercase; }
    .lab-grid { align-items: start; display: grid; gap: 14px; grid-template-columns: 280px minmax(0, 1fr) 360px; }
    .panel { background: rgba(17, 17, 26, .72); border: 1px solid #29293a; border-radius: 8px; min-width: 0; padding: 14px; }
    .controls { position: sticky; top: 70px; }
    button { border: 1px solid transparent; border-radius: 5px; color: #d9d7e5; font: inherit; font-size: 11px; font-weight: 900; min-height: 36px; padding: 0 12px; }
    button:disabled { opacity: .55; }
    .primary { background: #1db9aa; color: #061d1a; }
    .secondary { background: #1b1b28; border-color: #313145; }
    .control-row { display: grid; gap: 6px; grid-template-columns: repeat(2, 1fr); }
    .control-row.tiers { grid-template-columns: repeat(5, 1fr); margin-bottom: 12px; }
    .control-row button { background: #0f0f17; border-color: #303043; color: #9290a4; padding: 0; }
    .control-row button.active { background: #1b433d; color: #64ead6; }
    label { color: #89879b; display: flex; flex-direction: column; gap: 6px; font-size: 10px; font-weight: 800; margin-top: 10px; }
    input, select { background: #0e0e16; border: 1px solid #303043; border-radius: 5px; color: #e8e6f0; font: inherit; height: 36px; outline: none; padding: 0 9px; }
    input[type="range"] { padding: 0; }
    input[type="color"] { min-width: 0; padding: 3px; }
    input:focus, select:focus { border-color: #26aa9d; }
    .seed-row { display: grid; gap: 6px; grid-template-columns: minmax(0, 1fr) 82px; }
    .toggles { display: grid; gap: 8px; grid-template-columns: repeat(2, 1fr); margin-top: 12px; }
    .check { align-items: center; background: #0f0f17; border: 1px solid #303043; border-radius: 5px; flex-direction: row; gap: 7px; height: 36px; justify-content: center; margin: 0; }
    .check input { height: auto; padding: 0; }
    .summary { border-top: 1px solid #29293a; display: grid; gap: 4px; margin-top: 14px; padding-top: 12px; }
    .summary strong { color: #e8e6f0; font-size: 13px; }
    .summary span, .summary small { color: #8c899d; font-size: 11px; }
    .preview { overflow: hidden; }
    .preview-head { color: #8c899d; display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 10px; }
    .preview-head span { background: #0f0f17; border: 1px solid #303043; border-radius: 4px; font-size: 10px; padding: 5px 7px; }
    .canvas-shell { background: #06070b; border: 1px solid #303043; border-radius: 6px; min-height: 560px; overflow: auto; position: relative; }
    canvas { display: block; image-rendering: pixelated; }
    .empty { color: #77758c; left: 0; padding: 60px 20px; position: absolute; right: 0; text-align: center; top: 0; }
    .editor { position: sticky; top: 70px; }
    .editor-head { align-items: center; border-bottom: 1px solid #29293a; display: flex; gap: 12px; justify-content: space-between; margin-bottom: 10px; padding-bottom: 10px; }
    .editor-head h3 { color: #e8e6f0; font-size: 16px; margin: 2px 0 0; }
    .field-block { border-top: 1px solid #29293a; display: grid; gap: 9px; margin-top: 12px; padding-top: 12px; }
    .field-title { align-items: center; color: #d9d7e5; display: flex; font-size: 11px; font-weight: 900; justify-content: space-between; }
    .field-title small { color: #77758c; font-size: 10px; }
    .chip-list { display: flex; flex-wrap: wrap; gap: 6px; min-height: 30px; }
    .chip { align-items: center; background: #0f0f17; border: 1px solid #303043; border-radius: 5px; color: #d9d7e5; display: inline-flex; font-size: 11px; gap: 6px; min-width: 0; padding: 5px 6px; }
    .chip.family { max-width: 100%; }
    .chip.sprite { padding: 3px 6px 3px 3px; }
    .chip button { background: transparent; border: 0; color: #9d9aaf; height: 24px; min-height: 24px; min-width: 24px; padding: 0; }
    .chip button:not(:disabled):hover { color: #64ead6; }
    .add-row { display: grid; gap: 6px; grid-template-columns: minmax(0, 1fr) 72px; }
    .slider-grid { display: grid; gap: 8px; grid-template-columns: repeat(2, minmax(0, 1fr)); }
    output { color: #d9d7e5; font-size: 11px; }
    .color-grid { display: grid; gap: 7px; grid-template-columns: repeat(3, minmax(0, 1fr)); }
    .numeric-grid { display: grid; gap: 7px; grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .editor-actions { border-top: 1px solid #29293a; display: grid; gap: 8px; grid-template-columns: 1fr 1fr; margin-top: 14px; padding-top: 12px; }
    .editor-empty { position: static; }
    @media (max-width: 980px) {
      .lab-grid { grid-template-columns: 1fr; }
      .controls, .editor { position: static; }
      .canvas-shell { min-height: 420px; }
    }
  `],
})
export class MapLab implements OnInit, AfterViewInit {
  @ViewChild('canvas') private readonly canvas?: ElementRef<HTMLCanvasElement>;

  readonly biomes = signal<BiomeRow[]>([]);
  readonly draftBiome = signal<BiomeRow | null>(null);
  readonly tilesets = signal<TilesetSummaryDto | null>(null);
  readonly map = signal<MapDto | null>(null);
  readonly selectedTier = signal(2);
  readonly seed = signal(20260707);
  readonly bossFloor = signal(false);
  readonly zoom = signal<1 | 2>(1);
  readonly showBlocked = signal(false);
  readonly showRooms = signal(true);
  readonly loading = signal(true);
  readonly generating = signal(false);
  readonly status = signal<{ kind: 'ok' | 'err'; msg: string } | null>(null);
  readonly groundFamilyInput = signal('');
  readonly decorInput = signal('');
  readonly accentInput = signal('');

  readonly selectedBiome = computed(() => this.biomes().find((row) => row.tier === this.selectedTier()) ?? null);
  readonly busy = computed(() => this.loading() || this.generating());
  readonly mountainFamilies = computed(() => this.tilesets()?.families
    .filter((family) => family.kind === 'mountain')
    .map((family) => family.name) ?? []);
  readonly groundFamilies = computed(() => this.tilesets()?.families
    .filter((family) => family.kind === 'ground')
    .map((family) => family.name) ?? []);
  readonly availableGroundFamilies = computed(() => {
    const selected = new Set(this.draftBiome()?.def.groundFamilies ?? []);
    return this.groundFamilies().filter((family) => !selected.has(family));
  });
  readonly canAddGroundFamily = computed(() => {
    const draft = this.draftBiome();
    const selected = draft?.def.groundFamilies ?? [];
    const next = this.groundFamilyInput();
    return !!draft && !!next && selected.length < 3 && this.availableGroundFamilies().includes(next);
  });
  readonly tintColor = computed(() => {
    const atmo = this.draftBiome()?.def.atmosphere;
    return atmo ? rgbToHex(atmo.tintR, atmo.tintG, atmo.tintB) : '#000000';
  });
  readonly fogColor = computed(() => {
    const atmo = this.draftBiome()?.def.atmosphere;
    return atmo ? rgbToHex(atmo.fogR, atmo.fogG, atmo.fogB) : '#000000';
  });
  readonly particleColor = computed(() => {
    const atmo = this.draftBiome()?.def.atmosphere;
    return atmo ? rgbToHex(atmo.particleR, atmo.particleG, atmo.particleB) : '#000000';
  });

  constructor(private readonly api: ApiService, private readonly assets: AssetsService) {}

  async ngOnInit(): Promise<void> {
    try {
      const [biomes, tilesets] = await Promise.all([
        this.api.adminBiomes(),
        this.api.adminTilesets(),
        this.assets.preload(['objects']),
      ]);
      this.biomes.set(biomes.map(cloneBiomeRow));
      this.tilesets.set(tilesets);
      if (!this.selectedBiome() && biomes.length > 0) this.selectedTier.set(biomes[0].tier);
      this.syncDraftFromSelected();
      this.status.set(null);
      await this.generate();
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    } finally {
      this.loading.set(false);
    }
  }

  ngAfterViewInit(): void {
    this.render();
  }

  selectTier(tier: number): void {
    this.selectedTier.set(tier);
    this.syncDraftFromSelected();
    this.status.set(null);
  }

  setSeed(value: string): void {
    const parsed = Math.trunc(+value);
    if (!Number.isNaN(parsed)) this.seed.set(parsed);
  }

  rerollSeed(): void {
    this.seed.set(Math.floor(Math.random() * 2_000_000_000));
    void this.generate();
  }

  setFloor(value: string): void {
    this.bossFloor.set(value === 'boss');
  }

  setZoom(value: 1 | 2): void {
    this.zoom.set(value);
    this.render();
  }

  toggleBlocked(value: boolean): void {
    this.showBlocked.set(value);
    this.render();
  }

  toggleRooms(value: boolean): void {
    this.showRooms.set(value);
    this.render();
  }

  wallCoverage(tilesets: TilesetSummaryDto): string {
    if (tilesets.wallSets.length === 0) return 'No wall sets';
    const missing = tilesets.wallSets.reduce((sum, set) => sum + set.missingSlots, 0);
    return `${tilesets.wallSets.length} wall sets, ${missing} missing slots`;
  }

  updateDraftName(value: string): void {
    this.mutateDraft((draft) => {
      draft.name = value;
    });
  }

  updateWallFamily(value: string): void {
    this.mutateDraft((draft) => {
      draft.def.wallFamily = value;
    });
  }

  addGroundFamily(): void {
    const family = this.groundFamilyInput();
    if (!this.canAddGroundFamily()) return;
    this.mutateDraft((draft) => {
      draft.def.groundFamilies = [...(draft.def.groundFamilies ?? []), family];
    });
    this.groundFamilyInput.set('');
  }

  removeGroundFamily(family: string): void {
    this.mutateDraft((draft) => {
      const next = (draft.def.groundFamilies ?? []).filter((name) => name !== family);
      draft.def.groundFamilies = next.length > 0 ? next : draft.def.groundFamilies;
    });
  }

  moveGroundFamily(index: number, delta: -1 | 1): void {
    this.mutateDraft((draft) => {
      const next = [...(draft.def.groundFamilies ?? [])];
      const target = index + delta;
      if (index < 0 || target < 0 || index >= next.length || target >= next.length) return;
      const current = next[index];
      next[index] = next[target];
      next[target] = current;
      draft.def.groundFamilies = next;
    });
  }

  updateChance(key: 'decorChance' | 'accentChance', value: string): void {
    this.mutateDraft((draft) => {
      draft.def[key] = clampChance(+value);
    });
  }

  formatChance(value: number): string {
    return `${Math.round(value * 1000) / 10}%`;
  }

  addPaletteItem(kind: 'decor' | 'accent'): void {
    const input = kind === 'decor' ? this.decorInput() : this.accentInput();
    const itemId = parseItemId(input);
    if (!itemId) return;
    this.mutateDraft((draft) => {
      const palette = draft.def[kind];
      if (!palette.includes(itemId)) palette.push(itemId);
    });
    if (kind === 'decor') this.decorInput.set('');
    else this.accentInput.set('');
  }

  removePaletteItem(kind: 'decor' | 'accent', itemId: number): void {
    this.mutateDraft((draft) => {
      draft.def[kind] = draft.def[kind].filter((id) => id !== itemId);
    });
  }

  updateAtmosphereText(key: 'name', value: string): void {
    this.mutateDraft((draft) => {
      draft.def.atmosphere[key] = value;
    });
  }

  updateAtmosphereNumber(
    key: 'tintStrength' | 'fogStrength' | 'vignette' | 'particleDensity' | 'particleDrift',
    value: string,
  ): void {
    const parsed = +value;
    this.mutateDraft((draft) => {
      draft.def.atmosphere[key] = Number.isNaN(parsed) ? 0 : parsed;
    });
  }

  updateAtmosphereColor(kind: 'tint' | 'fog' | 'particle', value: string): void {
    const rgb = hexToRgb(value);
    if (!rgb) return;
    this.mutateDraft((draft) => {
      if (kind === 'tint') {
        draft.def.atmosphere.tintR = rgb.r;
        draft.def.atmosphere.tintG = rgb.g;
        draft.def.atmosphere.tintB = rgb.b;
      } else if (kind === 'fog') {
        draft.def.atmosphere.fogR = rgb.r;
        draft.def.atmosphere.fogG = rgb.g;
        draft.def.atmosphere.fogB = rgb.b;
      } else {
        draft.def.atmosphere.particleR = rgb.r;
        draft.def.atmosphere.particleG = rgb.g;
        draft.def.atmosphere.particleB = rgb.b;
      }
    });
  }

  async generate(): Promise<void> {
    this.generating.set(true);
    this.status.set(null);
    try {
      const preview = await this.api.adminMapPreview({
        tier: this.selectedTier(),
        seed: this.seed(),
        floorIndex: this.bossFloor() ? 1 : 0,
        bossFloor: this.bossFloor(),
        biome: null,
      });
      this.map.set(preview);
      this.render();
      this.status.set({ kind: 'ok', msg: 'Preview generated from backend mapgen.' });
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    } finally {
      this.generating.set(false);
    }
  }

  async previewDraft(): Promise<void> {
    const draft = this.draftBiome();
    if (!draft) return;
    this.generating.set(true);
    this.status.set(null);
    try {
      const preview = await this.api.adminMapPreview(
        previewRequestFromDraft(this.selectedTier(), this.seed(), this.bossFloor(), draft),
      );
      this.map.set(preview);
      this.render();
      this.status.set({ kind: 'ok', msg: 'Draft preview generated without saving.' });
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    } finally {
      this.generating.set(false);
    }
  }

  async saveDraft(): Promise<void> {
    const draft = this.draftBiome();
    if (!draft) return;
    this.generating.set(true);
    this.status.set(null);
    try {
      const saved = await this.api.adminSaveBiomes(replaceBiomeRow(this.biomes(), draft));
      this.biomes.set(saved.map(cloneBiomeRow));
      this.syncDraftFromSelected();
      this.status.set({ kind: 'ok', msg: 'Biome preset saved.' });
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    } finally {
      this.generating.set(false);
    }
  }

  async resetRow(): Promise<void> {
    this.generating.set(true);
    this.status.set(null);
    try {
      const rows = await this.api.adminBiomes();
      this.biomes.set(rows.map(cloneBiomeRow));
      this.syncDraftFromSelected();
      this.status.set({ kind: 'ok', msg: 'Draft reset from saved backend data.' });
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    } finally {
      this.generating.set(false);
    }
  }

  private render(): void {
    const canvas = this.canvas?.nativeElement;
    const map = this.map();
    if (!canvas || !map) return;
    drawMapPreviewCanvas(canvas, map, this.assets, {
      zoom: this.zoom(),
      showBlocked: this.showBlocked(),
      showRooms: this.showRooms(),
    });
  }

  private syncDraftFromSelected(): void {
    const row = this.selectedBiome();
    this.draftBiome.set(row ? cloneBiomeRow(row) : null);
    this.groundFamilyInput.set('');
    this.decorInput.set('');
    this.accentInput.set('');
  }

  private mutateDraft(mutator: (draft: BiomeRow) => void): void {
    const current = this.draftBiome();
    if (!current) return;
    const next = cloneBiomeRow(current);
    mutator(next);
    this.draftBiome.set(next);
  }
}
