import { AssetsService } from './assets.service';
import { EventDto, MapDto, MonsterDto, PlayerDto, SnapshotDto, TICK_MS } from './types';

const TILE = 32;
const BASE_SCALE = 2;
const GAMEPLAY_ZOOM = 1.25;
const SCALE = BASE_SCALE * GAMEPLAY_ZOOM;
const TS = TILE * SCALE; // screen px per tile
const RENDER_DELAY_MS = TICK_MS;
const CLOCK_SMOOTHING = 0.2;
const MAX_CLOCK_CORRECTION_MS = 25;

/** Tibia-style damage number colors by damage/condition type (player hits only). */
const DAMAGE_TYPE_COLORS: Record<string, string> = {
  poison: '#6ee76e', earth: '#6ee76e', fire: '#ff8c3c', energy: '#c47dff',
  ice: '#7df0ff', freeze: '#7df0ff', holy: '#ffe87d', dazzle: '#ffe87d',
  death: '#9b7dff', curse: '#9b7dff', lifedrain: '#ff5d8c', drown: '#5d9bff',
  bleed: '#ff5d5d', physical: '#ff5d5d',
};

interface ActiveEffect { x: number; y: number; id: number; start: number; }
interface ActiveProjectile { fromX: number; fromY: number; toX: number; toY: number; id: number; start: number; dur: number; }
interface FloatText { x: number; y: number; text: string; color: string; start: number; }
interface Bubble { x: number; y: number; text: string; start: number; }
interface Corpse { x: number; y: number; itemId: number; start: number; }
interface MotionSample {
  fromX: number;
  fromY: number;
  x: number;
  y: number;
  stepDurMs: number;
  stepStartTick: number;
}

/** Canvas renderer for the live run. The game component feeds snapshots/events. */
export class GameRenderer {
  private effects: ActiveEffect[] = [];
  private projectiles: ActiveProjectile[] = [];
  private texts: FloatText[] = [];
  private bubbles: Bubble[] = [];
  private corpses: Corpse[] = [];

  private snapArrival = 0;
  private serverClockOffsetMs: number | null = null;
  private readonly motionHistory = new Map<number, MotionSample[]>();
  private snapshot: SnapshotDto | null = null;
  private map: MapDto | null = null;

  hoverTile: { x: number; y: number } | null = null;

  constructor(
    private readonly canvas: HTMLCanvasElement,
    private readonly assets: AssetsService,
  ) {}

  setMap(map: MapDto): void {
    this.map = map;
    this.effects = [];
    this.projectiles = [];
    this.corpses = [];
    this.motionHistory.clear();
  }

  setSnapshot(snap: SnapshotDto, nowPerf: number): void {
    const previous = this.snapshot;
    const isPaused = snap.run.offer !== null;
    if (!isPaused) {
      const resumed = previous?.run.offer !== null;
      const resetClock = !previous || snap.tick <= previous.tick || resumed;
      const measuredOffset = snap.simulationMs - nowPerf;
      if (resetClock || this.serverClockOffsetMs === null) {
        this.serverClockOffsetMs = measuredOffset;
      } else {
        const drift = measuredOffset - this.serverClockOffsetMs;
        const correction = Math.max(
          -MAX_CLOCK_CORRECTION_MS,
          Math.min(drift, MAX_CLOCK_CORRECTION_MS),
        );
        this.serverClockOffsetMs += correction * CLOCK_SMOOTHING;
      }
    }
    this.recordMotion(snap.player, snap.simulationMs);
    for (const monster of snap.monsters) this.recordMotion(monster, snap.simulationMs);
    this.snapshot = snap;
    this.snapArrival = nowPerf;
    for (const ev of snap.events) this.ingest(ev, nowPerf);
  }

  private recordMotion(actor: PlayerDto | MonsterDto, simulationMs: number): void {
    const sample: MotionSample = {
      fromX: actor.fromX,
      fromY: actor.fromY,
      x: actor.x,
      y: actor.y,
      stepDurMs: actor.stepDurMs,
      stepStartTick: actor.stepDurMs ? actor.stepStartTick : simulationMs,
    };
    const history = this.motionHistory.get(actor.id) ?? [];
    const last = history[history.length - 1];
    if (last
      && last.fromX === sample.fromX
      && last.fromY === sample.fromY
      && last.x === sample.x
      && last.y === sample.y
      && last.stepDurMs === sample.stepDurMs
      && (sample.stepDurMs > 0 || last.stepStartTick === sample.stepStartTick)) return;
    history.push(sample);
    if (history.length > 5) history.shift();
    this.motionHistory.set(actor.id, history);
  }

  private ingest(ev: EventDto, now: number): void {
    switch (ev.kind) {
      case 'effect':
        this.effects.push({ x: ev.x, y: ev.y, id: ev.value, start: now });
        break;
      case 'projectile': {
        const dist = Math.max(Math.abs(ev.toX - ev.x), Math.abs(ev.toY - ev.y), 1);
        this.projectiles.push({
          fromX: ev.x, fromY: ev.y, toX: ev.toX, toY: ev.toY,
          id: ev.value, start: now, dur: 80 + dist * 45,
        });
        break;
      }
      case 'damage':
        this.texts.push({
          x: ev.x, y: ev.y, text: String(ev.value),
          color: ev.actorId === this.snapshot?.player.id
            ? DAMAGE_TYPE_COLORS[ev.text] ?? '#ff5d5d'
            : ev.crit ? '#ffd35d' : '#ffffff',
          start: now,
        });
        break;
      case 'heal':
        this.texts.push({ x: ev.x, y: ev.y, text: `+${ev.value}`, color: '#6ee76e', start: now });
        break;
      case 'text':
        this.texts.push({ x: ev.x, y: ev.y, text: ev.text, color: '#7df0ff', start: now });
        break;
      case 'gold':
        this.texts.push({ x: ev.x, y: ev.y, text: `+${ev.value} gold`, color: '#ffd35d', start: now });
        break;
      case 'pickup':
        this.texts.push({ x: ev.x, y: ev.y, text: ev.text, color: '#9dff9d', start: now });
        break;
      case 'levelup':
        this.texts.push({ x: ev.x, y: ev.y, text: `LEVEL ${ev.value}!`, color: '#7dff7d', start: now });
        break;
      case 'voice':
        this.bubbles.push({ x: ev.x, y: ev.y, text: ev.text, start: now });
        break;
      case 'death':
        this.corpses.push({ x: ev.x, y: ev.y, itemId: ev.value, start: now });
        break;
    }
  }

  // ---- coordinate helpers ----

  private serverNow(nowPerf: number): number {
    if (!this.snapshot) return 0;
    if (this.snapshot.run.offer) return this.snapshot.simulationMs;
    if (this.serverClockOffsetMs === null)
      return this.snapshot.simulationMs + (nowPerf - this.snapArrival) - RENDER_DELAY_MS;
    return nowPerf + this.serverClockOffsetMs - RENDER_DELAY_MS;
  }

  private actorMotionAt(actor: PlayerDto | MonsterDto, serverNow: number): MotionSample {
    const history = this.motionHistory.get(actor.id);
    if (!history?.length) return actor;
    for (let i = history.length - 1; i >= 0; i--) {
      if (history[i].stepStartTick <= serverNow) return history[i];
    }
    return history[0];
  }

  private actorRenderState(
    actor: PlayerDto | MonsterDto,
    serverNow: number,
  ): { x: number; y: number; moving: boolean } {
    const motion = this.actorMotionAt(actor, serverNow);
    if (!motion.stepDurMs) return { x: motion.x, y: motion.y, moving: false };
    const progress = Math.min(
      Math.max((serverNow - motion.stepStartTick) / motion.stepDurMs, 0),
      1,
    );
    return {
      x: motion.fromX + (motion.x - motion.fromX) * progress,
      y: motion.fromY + (motion.y - motion.fromY) * progress,
      moving: progress < 1,
    };
  }

  screenToTile(px: number, py: number, nowPerf: number): { x: number; y: number } | null {
    if (!this.snapshot || !this.map) return null;
    const cam = this.camera(nowPerf);
    return {
      x: Math.floor((px + cam.x) / TS),
      y: Math.floor((py + cam.y) / TS),
    };
  }

  monsterAtTile(tx: number, ty: number): MonsterDto | null {
    if (!this.snapshot) return null;
    return this.snapshot.monsters.find((m) => m.x === tx && m.y === ty)
      ?? this.snapshot.monsters.find((m) => Math.abs(m.x - tx) <= 1 && Math.abs(m.y - ty) <= 1) ?? null;
  }

  private camera(nowPerf: number): { x: number; y: number } {
    const serverNow = this.serverNow(nowPerf);
    const pos = this.actorRenderState(this.snapshot!.player, serverNow);
    const camX = pos.x * TS + TS / 2 - this.canvas.width / 2;
    const camY = pos.y * TS + TS / 2 - this.canvas.height / 2;
    const maxX = this.map!.w * TS - this.canvas.width;
    const maxY = this.map!.h * TS - this.canvas.height;
    return {
      x: Math.max(0, Math.min(camX, Math.max(maxX, 0))),
      y: Math.max(0, Math.min(camY, Math.max(maxY, 0))),
    };
  }

  // ---- main draw ----

  draw(nowPerf: number): void {
    const ctx = this.canvas.getContext('2d')!;
    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = '#0a0a0f';
    ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);

    if (!this.snapshot || !this.map || !this.assets.ready) return;
    const map = this.map;
    const snap = this.snapshot;
    const serverNow = this.serverNow(nowPerf);
    const cam = this.camera(nowPerf);

    const x0 = Math.max(Math.floor(cam.x / TS) - 1, 0);
    const y0 = Math.max(Math.floor(cam.y / TS) - 1, 0);
    const x1 = Math.min(Math.ceil((cam.x + this.canvas.width) / TS) + 1, map.w - 1);
    const y1 = Math.min(Math.ceil((cam.y + this.canvas.height) / TS) + 2, map.h - 1);

    const sx = (tx: number) => Math.round(tx * TS - cam.x);
    const sy = (ty: number) => Math.round(ty * TS - cam.y);

    // 1. ground + decor
    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) {
        const i = y * map.w + x;
        const ground = map.ground[i];
        if (ground) this.assets.drawObject(ctx, ground, sx(x), sy(y), SCALE, x, y, nowPerf);
        const decor = map.decor[i];
        if (decor) this.assets.drawObject(ctx, decor, sx(x), sy(y), SCALE, x, y, nowPerf);
      }
    }

    // 2. corpses (fade after a while)
    const corpseAlive = 28000;
    this.corpses = this.corpses.filter((c) => nowPerf - c.start < corpseAlive);
    for (const c of this.corpses) {
      if (c.x < x0 || c.x > x1 || c.y < y0 || c.y > y1) continue;
      const age = nowPerf - c.start;
      ctx.globalAlpha = age > corpseAlive - 3000 ? (corpseAlive - age) / 3000 : 1;
      this.assets.drawObject(ctx, c.itemId, sx(c.x), sy(c.y), SCALE, c.x, c.y, nowPerf - c.start);
      ctx.globalAlpha = 1;
    }

    // 3. ground items + POIs
    for (const item of snap.items) {
      if (item.x < x0 || item.x > x1 || item.y < y0 || item.y > y1) continue;
      this.assets.drawObject(ctx, item.itemId, sx(item.x), sy(item.y), SCALE, item.x, item.y, nowPerf);
    }
    for (const poi of map.pois) {
      if (poi.kind === 'chest' && poi.used) continue;
      this.assets.drawObject(ctx, poi.itemId, sx(poi.x), sy(poi.y), SCALE, poi.x, poi.y, nowPerf);
      // gentle highlight pulse on interactables
      const pulse = 0.35 + 0.25 * Math.sin(nowPerf / 350);
      ctx.strokeStyle = poi.kind === 'chest' ? `rgba(255, 211, 93, ${pulse})` : `rgba(125, 240, 255, ${pulse})`;
      ctx.lineWidth = 2;
      ctx.strokeRect(sx(poi.x) + 4, sy(poi.y) + 4, TS - 8, TS - 8);
    }

    // 4. walls and creatures, row by row (y-sorted)
    const creatures: { ry: number; draw: () => void; ref: PlayerDto | MonsterDto; pos: { x: number; y: number } }[] = [];
    for (const m of snap.monsters) {
      const pos = this.actorRenderState(m, serverNow);
      creatures.push({
        ry: pos.y, pos, ref: m,
        draw: () => {
          this.assets.drawOutfit(
            ctx, m.outfit.lookType, Math.round(pos.x * TS - cam.x), Math.round(pos.y * TS - cam.y), SCALE,
            m.dir, pos.moving, pos.moving ? serverNow : nowPerf,
            m.outfit.head, m.outfit.body, m.outfit.legs, m.outfit.feet, m.outfit.addons,
          );
        },
      });
    }
    {
      const p = snap.player;
      const pos = this.actorRenderState(p, serverNow);
      creatures.push({
        ry: pos.y, pos, ref: p,
        draw: () => {
          this.assets.drawOutfit(
            ctx, p.outfit.lookType, Math.round(pos.x * TS - cam.x), Math.round(pos.y * TS - cam.y), SCALE,
            p.dir, pos.moving, pos.moving ? serverNow : nowPerf,
            p.outfit.head, p.outfit.body, p.outfit.legs, p.outfit.feet, p.outfit.addons,
            p.outfit.mountLookType,
          );
        },
      });
    }
    creatures.sort((a, b) => a.ry - b.ry);

    let ci = 0;
    for (let y = y0; y <= y1; y++) {
      while (ci < creatures.length && creatures[ci].ry <= y + 0.5) {
        creatures[ci].draw();
        ci++;
      }
      for (let x = x0; x <= x1; x++) {
        const wall = map.wall[y * map.w + x];
        if (wall) this.assets.drawObject(ctx, wall, sx(x), sy(y), SCALE, x, y, nowPerf);
      }
    }
    while (ci < creatures.length) creatures[ci++].draw();

    // 5. target highlight
    if (snap.player.targetId) {
      const target = snap.monsters.find((m) => m.id === snap.player.targetId);
      if (target) {
        const pos = this.actorRenderState(target, serverNow);
        ctx.strokeStyle = '#ff4d4d';
        ctx.lineWidth = 2;
        ctx.strokeRect(pos.x * TS - cam.x + 2, pos.y * TS - cam.y + 2, TS - 4, TS - 4);
      }
    }
    if (this.hoverTile) {
      ctx.strokeStyle = 'rgba(255,255,255,0.35)';
      ctx.lineWidth = 1;
      ctx.strokeRect(sx(this.hoverTile.x) + 1, sy(this.hoverTile.y) + 1, TS - 2, TS - 2);
    }

    // 6. effects
    this.effects = this.effects.filter((e) => {
      const alive = this.assets.drawEffect(ctx, e.id, sx(e.x), sy(e.y), SCALE, nowPerf - e.start);
      return alive;
    });

    // 7. projectiles
    this.projectiles = this.projectiles.filter((p) => {
      const t = (nowPerf - p.start) / p.dur;
      if (t >= 1) return false;
      const x = p.fromX + (p.toX - p.fromX) * t;
      const y = p.fromY + (p.toY - p.fromY) * t;
      this.assets.drawMissile(ctx, p.id, Math.round(x * TS - cam.x), Math.round(y * TS - cam.y), SCALE, p.toX - p.fromX, p.toY - p.fromY);
      return true;
    });

    // 8. health bars + names (tibia style)
    for (const c of creatures) {
      const m = c.ref as MonsterDto;
      const isPlayer = (c.ref as PlayerDto).skills !== undefined;
      const px = c.pos.x * TS - cam.x;
      const py = c.pos.y * TS - cam.y;
      const frac = Math.max(c.ref.hp / c.ref.maxHp, 0);
      const color = frac > 0.6 ? '#00c000' : frac > 0.3 ? '#c0c000' : '#c00000';
      ctx.fillStyle = '#000';
      ctx.fillRect(px + TS / 2 - 14, py - 8, 28, 4);
      ctx.fillStyle = color;
      ctx.fillRect(px + TS / 2 - 13, py - 7, 26 * frac, 2);
      if (!isPlayer) {
        ctx.font = 'bold 11px Verdana, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillStyle = m.isBoss ? '#ff8c4d' : color;
        ctx.fillText(m.species, px + TS / 2, py - 12);
        if (m.stunned) {
          ctx.fillStyle = '#ffd35d';
          ctx.fillText('✶', px + TS / 2, py - 24);
        }
        if (m.elementMark) {
          // small pulsing elemental "mark" dot waiting for a reaction
          const mc = DAMAGE_TYPE_COLORS[m.elementMark] ?? '#ffffff';
          const r = 4 + Math.sin(nowPerf / 200) * 0.6;
          ctx.beginPath();
          ctx.arc(px + TS / 2 + 16, py - 6, r, 0, Math.PI * 2);
          ctx.fillStyle = mc;
          ctx.fill();
          ctx.strokeStyle = 'rgba(0,0,0,0.6)';
          ctx.lineWidth = 1;
          ctx.stroke();
        }
      }
    }

    // 9. floating texts
    const textLife = 1100;
    this.texts = this.texts.filter((t) => nowPerf - t.start < textLife);
    ctx.font = 'bold 13px Verdana, sans-serif';
    ctx.textAlign = 'center';
    for (const t of this.texts) {
      const age = (nowPerf - t.start) / textLife;
      ctx.globalAlpha = 1 - age * age;
      ctx.fillStyle = t.color;
      ctx.fillText(t.text, t.x * TS - cam.x + TS / 2, t.y * TS - cam.y - 14 - age * 26);
    }
    ctx.globalAlpha = 1;

    // 10. speech bubbles
    const bubbleLife = 2600;
    this.bubbles = this.bubbles.filter((b) => nowPerf - b.start < bubbleLife);
    ctx.font = 'bold 12px Verdana, sans-serif';
    for (const b of this.bubbles) {
      const bx = b.x * TS - cam.x + TS / 2;
      const by = b.y * TS - cam.y - 26;
      ctx.fillStyle = '#ffff66';
      ctx.fillText(b.text, bx, by);
    }
  }

  /** Tiny minimap in the given canvas: blocked grid + player + boss. */
  drawMinimap(mini: HTMLCanvasElement): void {
    if (!this.map || !this.snapshot) return;
    const ctx = mini.getContext('2d')!;
    const map = this.map;
    const cell = Math.max(Math.floor(Math.min(mini.width / map.w, mini.height / map.h)), 2);
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, mini.width, mini.height);
    for (let y = 0; y < map.h; y++) {
      for (let x = 0; x < map.w; x++) {
        if (map.blocked[y * map.w + x]) continue;
        ctx.fillStyle = '#4a3528';
        ctx.fillRect(x * cell, y * cell, cell, cell);
      }
    }
    for (const poi of map.pois) {
      ctx.fillStyle = poi.kind === 'chest' ? '#ffd35d' : '#7df0ff';
      ctx.fillRect(poi.x * cell, poi.y * cell, cell, cell);
    }
    for (const m of this.snapshot.monsters) {
      ctx.fillStyle = m.isBoss ? '#ff8c4d' : '#c03030';
      ctx.fillRect(m.x * cell, m.y * cell, cell, cell);
    }
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(this.snapshot.player.x * cell - 1, this.snapshot.player.y * cell - 1, cell + 2, cell + 2);
  }
}
