import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { BiomeRow, MapDto, TilesetSummaryDto } from '../../core/types';

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
      draw(map.ground[i], x, y);
      draw(map.borderA[i], x, y);
      draw(map.borderB[i], x, y);
      draw(map.decor[i], x, y);
    }
  }

  for (let y = 0; y < map.h; y++) {
    for (let x = 0; x < map.w; x++) {
      draw(map.wall[y * map.w + x], x, y);
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

@Component({
  selector: 'app-map-lab',
  standalone: true,
  imports: [],
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
    .lab-grid { align-items: start; display: grid; gap: 14px; grid-template-columns: 280px minmax(0, 1fr); }
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
    @media (max-width: 980px) {
      .lab-grid { grid-template-columns: 1fr; }
      .controls { position: static; }
      .canvas-shell { min-height: 420px; }
    }
  `],
})
export class MapLab implements OnInit, AfterViewInit {
  @ViewChild('canvas') private readonly canvas?: ElementRef<HTMLCanvasElement>;

  readonly biomes = signal<BiomeRow[]>([]);
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

  readonly selectedBiome = computed(() => this.biomes().find((row) => row.tier === this.selectedTier()) ?? null);
  readonly busy = computed(() => this.loading() || this.generating());

  constructor(private readonly api: ApiService, private readonly assets: AssetsService) {}

  async ngOnInit(): Promise<void> {
    try {
      const [biomes, tilesets] = await Promise.all([
        this.api.adminBiomes(),
        this.api.adminTilesets(),
        this.assets.preload(['objects']),
      ]);
      this.biomes.set(biomes.map((row) => ({ ...row, def: { ...row.def } })));
      this.tilesets.set(tilesets);
      if (!this.selectedBiome() && biomes.length > 0) this.selectedTier.set(biomes[0].tier);
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
}
