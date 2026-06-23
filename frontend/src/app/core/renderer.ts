import { AssetsService } from './assets.service';
import { BiomeDto, EventDto, MapDto, MonsterDto, PlayerDto, SnapshotDto, TICK_MS } from './types';

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

const LOOT_FLY_MS = 460;
const LOOT_ARC_TILES = 0.9;

// ---- G-02 combat juice tuning (all cosmetic, client-side; engine untouched) ----
const HIT_FLASH_MS = 150;          // additive white bloom on a struck sprite
const HIT_PUNCH_MS = 170;          // scale-pop "hit-stop" on the struck sprite
const HIT_PUNCH_AMP = 0.17;        // base pop magnitude (scaled by hit intensity)
const SHAKE_DUR_MS = 240;
const SHAKE_MAX_PX = 7;
const SHAKE_MIN_INTENSITY = 0.42;  // below this a hit does not shake (avoid constant rattle)
const DISSOLVE_MS = 640;           // pixel dissolve on death
const DISSOLVE_EDGE = 0.18;        // softness of the dissolving wavefront
const DISSOLVE_RISE_PX = 9;        // upward drift of the dissipating sprite
const TEXT_LIFE_MS = 1100;
const PROC_LIFE_MS = 1350;

// ---- G-03 helper legibility + Echo Break climax (client-side reads of the snapshot only) ----
const RETICLE_SPAN = 0.92;         // target-reticle bracket span (tiles)
const INTENT_DASH = 14;            // intention-line dash period (px)
const TELE_DEFAULT_RANGE = 4;      // fallback skill range (tiles) when the shape lookup is missing
const TELE_DEFAULT_RADIUS = 1.4;   // fallback skill radius (tiles)
// ---- CUT-05 skill-cast FX (cosmetic; reads the engine's `skill_cast` visual event only) ----
const SKILL_FX_MS = 440;           // base lifetime of a shape-keyed cast flourish
const SKILL_FX_ULT_MS = 700;       // ultimates linger a touch longer and read heavier
const ULT_FLASH_MS = 380;          // brief element-tinted screen bloom on an ultimate cast
const SKILL_FX_MAX = 24;           // hard cap so a burst of casts can't grow the layer unbounded
const ECHO_FLASH_MS = 540;         // full-screen flash + banner on the break instant
const ECHO_WARP_MS = 240;          // slow-mo window (visual time-dilation only)
const ECHO_WARP_SCALE = 0.28;      // interpolation playback speed during the slow-mo
const ECHO_WARP_RECOVER = 0.6;     // catch-up rate (ms repaid per real ms) after the window
const ECHO_WARP_MAX = 420;         // cap on how far visuals may lag the authoritative clock
const SHOCKWAVE_MS = 620;          // expanding ring bursting from the boss on break

/** Punchy colours for proc/callout texts routed through the `text` event kind. */
const PROC_COLORS: { match: string; color: string }[] = [
  { match: 'QUEBR', color: '#ff5d5d' },   // Echo Break
  { match: 'EXECU', color: '#ff3b3b' },
  { match: 'JULGAD', color: '#ffe07a' },
  { match: 'PRESA', color: '#ff7a7a' },
  { match: 'DESCARGA', color: '#c47dff' },
  { match: 'ESTILHA', color: '#7df0ff' },
  { match: 'IMUNE', color: '#9aa6b2' },
  { match: 'LENTID', color: '#7df0ff' },
  { match: 'STANCE', color: '#7df0ff' },
];

function procColor(text: string): string {
  const up = text.toUpperCase();
  for (const p of PROC_COLORS) if (up.includes(p.match)) return p.color;
  return '#9dffe0';
}

function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}

/** Tiny deterministic PRNG (mulberry32) — seeds the cosmetic atmosphere particle field per map. */
function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// ---- G-07: biome atmosphere (color-grade + fog + vignette + drifting motes), all cosmetic ----
const ATMO_PARTICLE_MAX = 90;      // particle count at density 1.0
/** Minimap markers for room types not already covered by a POI sprite (chest/sanctuary/ladder). */
const ROOM_ICONS: Record<string, { color: string; glyph: string }> = {
  elite: { color: '#ff5d5d', glyph: '!' },
  miniboss: { color: '#ff8c4d', glyph: '×' },
  hazard: { color: '#c08cff', glyph: '?' },
  boss: { color: '#ff3b3b', glyph: '★' },
};
interface AtmoParticle { x: number; y: number; size: number; speed: number; phase: number; }

/** Slight-overshoot ease used for the damage-number "pop-in". */
function easeOutBack(x: number): number {
  const c1 = 1.70158;
  const c3 = c1 + 1;
  return 1 + c3 * Math.pow(x - 1, 3) + c1 * Math.pow(x - 1, 2);
}

interface ActiveEffect { x: number; y: number; id: number; start: number; }
interface ActiveProjectile { fromX: number; fromY: number; toX: number; toY: number; id: number; start: number; dur: number; }
/** A coin/item that bursts from a kill and homes in on the player along an arc. */
interface ActiveLoot { fromX: number; fromY: number; id: number; text: string; color: string; start: number; }
type FloatKind = 'dmg' | 'proc' | 'heal' | 'info';
interface FloatText {
  x: number; y: number; text: string; color: string; start: number;
  kind: FloatKind; crit?: boolean; mag?: number; vx?: number; life?: number;
}
/** Transient impact state per-actor: drives the flash + scale-pop on the struck sprite. */
interface HitFx { start: number; intensity: number; crit: boolean; }
/** A dying creature dissolving away pixel-by-pixel (captured from its last outfit). */
interface Dissolve {
  x: number; y: number; dir: number; start: number;
  lookType: number; head: number; body: number; legs: number; feet: number;
  addons: number; mount: number;
  built?: boolean; failed?: boolean;
  sprite?: HTMLCanvasElement; base?: ImageData; noise?: Float32Array;
  cellW?: number; cellH?: number;
}
interface Bubble { x: number; y: number; text: string; start: number; }
interface Corpse { x: number; y: number; itemId: number; start: number; }
/** Skill footprint used to telegraph what the helper is about to land (from the catalog). */
interface SkillShape { shape: string; range: number; radius: number; }
/** Expanding ring spawned at the boss tile when an Echo Break fires. */
interface Shockwave { x: number; y: number; start: number; }
/**
 * CUT-05: a transient, shape-keyed flourish stamped when the Kaeli casts a skill. Built purely from
 * the engine's `skill_cast` visual event (origin tile, aim tile, skill id, ultimate flag) plus the
 * footprint catalog already loaded via `setSkillShapes` — it never touches the simulation. Tile
 * coordinates are resolved against the camera each frame, like the other effect layers.
 */
interface SkillFx {
  fromX: number; fromY: number; aimX: number; aimY: number;
  shape: string; range: number; radius: number;
  ult: boolean; color: string; start: number;
}
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
  private loot: ActiveLoot[] = [];
  private lootChain = 0;
  private texts: FloatText[] = [];
  private bubbles: Bubble[] = [];
  private corpses: Corpse[] = [];

  // G-02 juice state
  private hits = new Map<number, HitFx>();
  private dissolves: Dissolve[] = [];
  private shakeStart = -1;
  private shakeMag = 0;
  private textJitter = 0;
  private dissolveWork: HTMLCanvasElement | null = null;
  /** Snapshot of the previous frame's monsters, so a death event can capture the dying outfit. */
  private deathLookup = new Map<number, MonsterDto>();

  // G-07 biome atmosphere (cosmetic): a deterministic mote field rebuilt per map.
  private atmoParticles: AtmoParticle[] = [];

  // G-03 helper legibility + Echo Break climax
  private skillShapes = new Map<string, SkillShape>();
  // CUT-05 skill-cast FX layer (cosmetic): per-cast flourishes + an ultimate screen bloom.
  private skillFx: SkillFx[] = [];
  private ultFlashStart = -1;
  private ultFlashColor = '#ffe6a0';
  private shockwaves: Shockwave[] = [];
  private echoFlashStart = -1;
  private echoBreakCount = 0;
  // Visual slow-mo: `warpAccum` is how many ms the interpolation clock currently lags the live one.
  // It is banked while slowed then repaid, so interpolation always resyncs to the authoritative
  // clock — we never simulate, we only vary the playback rate of interpolation.
  private warpAccum = 0;
  private warpUntil = 0;
  private lastFrame = -1;

  private snapArrival = 0;
  private serverClockOffsetMs: number | null = null;
  private readonly motionHistory = new Map<number, MotionSample[]>();
  private snapshot: SnapshotDto | null = null;
  private map: MapDto | null = null;

  hoverTile: { x: number; y: number } | null = null;

  constructor(
    private readonly canvas: HTMLCanvasElement,
    private readonly assets: AssetsService,
    private readonly sound?: { coinChing(chainIndex: number): void; echoBreak?(): void },
  ) {}

  /** Feed the catalog's skill footprints so the helper telegraph previews the right shape. */
  setSkillShapes(skills: { id: string; shape: string; range: number; radius: number }[]): void {
    this.skillShapes.clear();
    for (const s of skills) this.skillShapes.set(s.id, { shape: s.shape, range: s.range, radius: s.radius });
  }

  setMap(map: MapDto): void {
    this.map = map;
    this.buildAtmoParticles(map);
    this.effects = [];
    this.projectiles = [];
    this.loot = [];
    this.corpses = [];
    this.hits.clear();
    this.dissolves = [];
    this.shakeMag = 0;
    this.deathLookup.clear();
    this.motionHistory.clear();
    this.shockwaves = [];
    this.skillFx = [];
    this.ultFlashStart = -1;
    this.echoFlashStart = -1;
    this.warpAccum = 0;
    this.warpUntil = 0;
    this.lastFrame = -1;
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
    // Keep a lookup of who was alive last frame so a `death` event can capture the dying outfit
    // (the killed monster is already gone from the new snapshot's monster list).
    this.deathLookup.clear();
    if (previous) for (const m of previous.monsters) this.deathLookup.set(m.id, m);
    this.snapshot = snap;
    this.snapArrival = nowPerf;
    // G-03: rising edge of the (engine-authoritative) Echo Break → fire the run-climax FX.
    if (snap.run.bossStaggered && !previous?.run.bossStaggered) this.triggerEchoBreak(snap, nowPerf);
    for (const ev of snap.events) this.ingest(ev, nowPerf);
  }

  /** The Echo Break already exists in the engine (effect 35 + "QUEBRADO!"); here we elevate it to a
   *  run climax purely client-side: a brief slow-mo, a gold flash + banner, a shockwave and a shake. */
  private triggerEchoBreak(snap: SnapshotDto, now: number): void {
    this.echoFlashStart = now;
    this.echoBreakCount = snap.run.bossPostureCycle;
    this.warpUntil = now + ECHO_WARP_MS;
    this.triggerShake(now, 1.4);
    const boss = snap.monsters.find((m) => m.isBoss);
    if (boss) this.shockwaves.push({ x: boss.x, y: boss.y, start: now });
    this.sound?.echoBreak?.();
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
      case 'skill_cast':
        this.ingestSkillCast(ev, now);
        break;
      case 'projectile': {
        const dist = Math.max(Math.abs(ev.toX - ev.x), Math.abs(ev.toY - ev.y), 1);
        this.projectiles.push({
          fromX: ev.x, fromY: ev.y, toX: ev.toX, toY: ev.toY,
          id: ev.value, start: now, dur: 80 + dist * 45,
        });
        break;
      }
      case 'damage': {
        const playerVictim = ev.actorId === this.snapshot?.player.id;
        // Impact weight is the hit as a fraction of the victim's max HP (engine value, not RNG),
        // so a chip and a haymaker feel different. The killing blow's victim is already gone, so
        // fall back to an absolute scale for that frame.
        const maxHp = this.actorMaxHp(ev.actorId);
        const frac = maxHp > 0 ? ev.value / maxHp : Math.min(ev.value / 360, 0.9);
        const intensity = clamp((ev.crit ? 0.55 : 0.28) + frac * 1.1, 0.2, 1.4);
        this.registerHit(ev.actorId, now, intensity, ev.crit);
        if (ev.crit || intensity >= SHAKE_MIN_INTENSITY || playerVictim) {
          this.triggerShake(now, intensity * (ev.crit ? 1.15 : 1) + (playerVictim ? 0.2 : 0));
        }
        this.texts.push({
          x: ev.x, y: ev.y, text: String(ev.value),
          color: playerVictim
            ? DAMAGE_TYPE_COLORS[ev.text] ?? '#ff5d5d'
            : ev.crit ? '#ffd35d' : '#ffffff',
          start: now, kind: 'dmg', crit: ev.crit, mag: intensity,
          vx: ((this.textJitter++ % 3) - 1) * 0.16,
        });
        break;
      }
      case 'heal':
        this.texts.push({ x: ev.x, y: ev.y, text: `+${ev.value}`, color: '#6ee76e', start: now, kind: 'heal' });
        break;
      case 'text':
        // Engine `text` events are proc/callouts (QUEBRADO!, JULGADO, IMUNE, STANCE…): make them pop.
        this.texts.push({ x: ev.x, y: ev.y, text: ev.text, color: procColor(ev.text), start: now, kind: 'proc' });
        break;
      case 'gold':
        this.texts.push({ x: ev.x, y: ev.y, text: `+${ev.value} gold`, color: '#ffd35d', start: now, kind: 'info' });
        break;
      case 'pickup':
        this.texts.push({ x: ev.x, y: ev.y, text: ev.text, color: '#9dff9d', start: now, kind: 'info' });
        break;
      case 'loot':
        // value = sprite que voa, text = rótulo na chegada, crit = é ouro (cor dourada)
        this.loot.push({
          fromX: ev.x, fromY: ev.y, id: ev.value, text: ev.text,
          color: ev.crit ? '#ffd35d' : '#9dff9d', start: now,
        });
        break;
      case 'levelup':
        this.texts.push({ x: ev.x, y: ev.y, text: `LEVEL ${ev.value}!`, color: '#7dff7d', start: now, kind: 'proc' });
        break;
      case 'voice':
        this.bubbles.push({ x: ev.x, y: ev.y, text: ev.text, start: now });
        break;
      case 'death':
        this.corpses.push({ x: ev.x, y: ev.y, itemId: ev.value, start: now });
        this.spawnDissolve(ev.actorId, ev.x, ev.y, now);
        break;
    }
  }

  /**
   * CUT-05: turn the engine's `skill_cast` visual event into a shape-keyed flourish. The skill id
   * (in `ev.text`) is resolved against the footprint catalog for the shape/range/radius, and the
   * accent colour comes from the caster's current stance element — so the cast reads on-brand
   * without the engine ever sending colour. Ultimates (`ev.crit`) also bloom the screen + shake.
   */
  private ingestSkillCast(ev: EventDto, now: number): void {
    const cat = this.skillShapes.get(ev.text);
    const shape = cat?.shape ?? 'single';
    if (shape === 'summon' && !cat) return; // unknown id with no footprint → nothing meaningful
    const element = this.snapshot?.player.stanceElement ?? '';
    const color = DAMAGE_TYPE_COLORS[element] ?? '#9dffe0';
    const ult = ev.crit;
    this.skillFx.push({
      fromX: ev.x, fromY: ev.y, aimX: ev.toX, aimY: ev.toY,
      shape, range: cat?.range || TELE_DEFAULT_RANGE, radius: cat?.radius || TELE_DEFAULT_RADIUS,
      ult, color, start: now,
    });
    if (this.skillFx.length > SKILL_FX_MAX) this.skillFx.shift();
    if (ult) {
      this.ultFlashStart = now;
      this.ultFlashColor = color;
      this.triggerShake(now, 0.95);
    }
  }

  /** Look up a victim's max HP for impact weighting (player or a still-living monster). */
  private actorMaxHp(id: number): number {
    if (!this.snapshot) return 0;
    if (this.snapshot.player.id === id) return this.snapshot.player.maxHp;
    const m = this.snapshot.monsters.find((mm) => mm.id === id) ?? this.deathLookup.get(id);
    return m?.maxHp ?? 0;
  }

  private registerHit(id: number, now: number, intensity: number, crit: boolean): void {
    const prev = this.hits.get(id);
    // Keep the strongest recent impact so rapid multi-hits don't weaken the pop (a crit always wins).
    if (prev && now - prev.start < HIT_PUNCH_MS && prev.intensity >= intensity && (prev.crit || !crit)) return;
    this.hits.set(id, { start: now, intensity, crit });
  }

  private triggerShake(now: number, intensity: number): void {
    const mag = clamp(intensity, 0, 1.4) / 1.4 * SHAKE_MAX_PX;
    const remaining = this.shakeStart >= 0
      ? this.shakeMag * Math.max(0, 1 - (now - this.shakeStart) / SHAKE_DUR_MS)
      : 0;
    if (mag <= remaining) return; // don't let a small hit cut short a big rumble
    this.shakeMag = mag;
    this.shakeStart = now;
  }

  /** Decaying sinusoidal camera offset (deterministic trig — purely cosmetic). */
  private shakeOffset(now: number): { x: number; y: number } {
    if (this.shakeStart < 0) return { x: 0, y: 0 };
    const t = (now - this.shakeStart) / SHAKE_DUR_MS;
    if (t >= 1) return { x: 0, y: 0 };
    const decay = (1 - t) * (1 - t);
    const a = this.shakeMag * decay;
    return { x: Math.cos(now * 0.085) * a, y: Math.sin(now * 0.127) * a };
  }

  /** Capture the dying creature's outfit so it can dissolve away pixel by pixel. */
  private spawnDissolve(id: number, x: number, y: number, now: number): void {
    const m = this.snapshot?.monsters.find((mm) => mm.id === id) ?? this.deathLookup.get(id);
    if (!m) return; // no outfit to dissolve (e.g. corpse-only event) — corpse fade handles it
    this.dissolves.push({
      x: m.x, y: m.y, dir: m.dir, start: now,
      lookType: m.outfit.lookType, head: m.outfit.head, body: m.outfit.body,
      legs: m.outfit.legs, feet: m.outfit.feet, addons: m.outfit.addons,
      mount: m.outfit.mountLookType,
    });
  }

  /**
   * Draws one creature sprite with the G-02 impact "juice": a brief scale-pop (hit-stop) around the
   * tile centre and an additive flash bloom, both eased from the last hit on that actor. The actual
   * sprite draw is delegated so this works for both monsters and the player.
   */
  private drawActorSprite(
    ctx: CanvasRenderingContext2D, id: number, screenX: number, screenY: number,
    now: number, drawSprite: () => void,
  ): void {
    const hit = this.hits.get(id);
    const cx = screenX + TS / 2;
    const cy = screenY + TS / 2;
    let punch = 1;
    if (hit) {
      const age = now - hit.start;
      if (age < HIT_PUNCH_MS) punch = 1 + HIT_PUNCH_AMP * hit.intensity * Math.sin(Math.PI * (age / HIT_PUNCH_MS));
    }
    if (punch !== 1) {
      ctx.save();
      ctx.translate(cx, cy);
      ctx.scale(punch, punch);
      ctx.translate(-cx, -cy);
      drawSprite();
      ctx.restore();
    } else {
      drawSprite();
    }
    if (hit) {
      const fage = now - hit.start;
      if (fage < HIT_FLASH_MS) {
        const a = clamp(hit.intensity, 0.2, 1.4) * 0.58 * (1 - fage / HIT_FLASH_MS);
        const r = TS * 0.72;
        const tint = hit.crit ? '255,224,150' : '255,255,255';
        const grd = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
        grd.addColorStop(0, `rgba(${tint},${a})`);
        grd.addColorStop(1, `rgba(${tint},0)`);
        ctx.save();
        ctx.globalCompositeOperation = 'lighter';
        ctx.fillStyle = grd;
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }
    }
  }

  /**
   * Renders one dissolving corpse. The captured outfit is rasterised once into an offscreen buffer
   * with a per-pixel noise threshold; each frame the wavefront (driven by `progress`) erases pixels
   * below the threshold with a soft, glowing edge, while the whole sprite drifts up and fades.
   */
  private drawDissolve(ctx: CanvasRenderingContext2D, d: Dissolve, now: number, cam: { x: number; y: number }): boolean {
    const age = now - d.start;
    if (age >= DISSOLVE_MS || d.failed) return false;
    if (!d.built) this.buildDissolve(d);
    if (d.failed || !d.sprite || !d.base || !d.noise) return !d.failed; // keep until built or failed

    const progress = age / DISSOLVE_MS;
    const base = d.base;
    const noise = d.noise;
    const w = base.width;
    const h = base.height;

    const work = this.dissolveWork ??= document.createElement('canvas');
    if (work.width < w || work.height < h) { work.width = w; work.height = h; }
    const wctx = work.getContext('2d', { willReadFrequently: true });
    if (!wctx) return false;
    const out = wctx.createImageData(w, h);
    const sd = base.data;
    const od = out.data;
    for (let i = 0, p = 0; i < noise.length; i++, p += 4) {
      const a0 = sd[p + 3];
      if (a0 === 0) continue;
      const k = (progress - noise[i]) / DISSOLVE_EDGE; // <0 intact, >1 gone
      if (k >= 1) continue;
      if (k <= 0) {
        od[p] = sd[p]; od[p + 1] = sd[p + 1]; od[p + 2] = sd[p + 2]; od[p + 3] = a0;
      } else {
        // dissolving wavefront: glowing ember edge fading out
        const f = 1 - k;
        od[p] = Math.min(255, sd[p] + (255 - sd[p]) * k * 0.9);
        od[p + 1] = Math.min(255, sd[p + 1] + (180 - sd[p + 1]) * k * 0.6);
        od[p + 2] = sd[p + 2] * (1 - k);
        od[p + 3] = a0 * f;
      }
    }
    wctx.putImageData(out, 0, 0);

    const offX = (d.cellW! - 32) * SCALE;
    const offY = (d.cellH! - 32) * SCALE;
    const destX = Math.round(d.x * TS - cam.x) - offX;
    const destY = Math.round(d.y * TS - cam.y) - offY - progress * DISSOLVE_RISE_PX;
    ctx.save();
    ctx.globalAlpha = Math.min(1, (1 - progress) * 1.3);
    ctx.drawImage(work, 0, 0, w, h, destX, destY, w, h);
    ctx.restore();
    return true;
  }

  /** Rasterise the dying outfit once + build its dissolve noise map. */
  private buildDissolve(d: Dissolve): void {
    d.built = true;
    const entry = this.assets.entry('outfits', d.lookType);
    if (!entry) { d.failed = true; return; }
    const w = Math.ceil(entry.cellW * SCALE);
    const h = Math.ceil(entry.cellH * SCALE);
    const sprite = document.createElement('canvas');
    sprite.width = w;
    sprite.height = h;
    const sctx = sprite.getContext('2d', { willReadFrequently: true });
    if (!sctx) { d.failed = true; return; }
    sctx.imageSmoothingEnabled = false;
    // Pass dx/dy so the sprite's bottom-right tile anchor lands at the offscreen origin.
    this.assets.drawOutfit(
      sctx, d.lookType, (entry.cellW - 32) * SCALE, (entry.cellH - 32) * SCALE, SCALE,
      d.dir, false, 0, d.head, d.body, d.legs, d.feet, d.addons, d.mount,
    );
    const base = sctx.getImageData(0, 0, w, h);
    let anyOpaque = false;
    for (let p = 3; p < base.data.length; p += 4) { if (base.data[p] !== 0) { anyOpaque = true; break; } }
    if (!anyOpaque) { d.built = false; return; } // atlas not decoded yet — retry next frame
    // Per-pixel threshold biased so the sprite dissipates roughly top-first (spirit rising).
    const noise = new Float32Array(w * h);
    for (let y = 0, i = 0; y < h; y++) {
      const bias = (y / h) * 0.25; // lower threshold near the top → erased earlier
      for (let x = 0; x < w; x++, i++) noise[i] = Math.min(1, Math.random() * 0.8 + bias);
    }
    d.sprite = sprite;
    d.base = base;
    d.noise = noise;
    d.cellW = entry.cellW;
    d.cellH = entry.cellH;
  }

  // ---- coordinate helpers ----

  private serverNow(nowPerf: number): number {
    if (!this.snapshot) return 0;
    if (this.snapshot.run.offer) return this.snapshot.simulationMs;
    const live = this.serverClockOffsetMs === null
      ? this.snapshot.simulationMs + (nowPerf - this.snapArrival) - RENDER_DELAY_MS
      : nowPerf + this.serverClockOffsetMs - RENDER_DELAY_MS;
    // Echo Break slow-mo: replay interpolation a touch behind the live clock, then resync.
    return live - this.warpAccum;
  }

  /** Advance the Echo Break time-warp: bank a lag while slowed, repay it after so the visual clock
   *  smoothly converges back to the authoritative one. Cosmetic only — never gates a command. */
  private updateWarp(now: number): void {
    if (this.lastFrame < 0) { this.lastFrame = now; return; }
    const dt = Math.min(now - this.lastFrame, 80);
    this.lastFrame = now;
    if (now < this.warpUntil) {
      this.warpAccum = Math.min(ECHO_WARP_MAX, this.warpAccum + dt * (1 - ECHO_WARP_SCALE));
    } else if (this.warpAccum > 0) {
      this.warpAccum = Math.max(0, this.warpAccum - dt * ECHO_WARP_RECOVER);
    }
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
    // Apply screen-shake after clamping so the rumble is visible even at map edges.
    const shake = this.shakeOffset(nowPerf);
    return {
      x: Math.max(0, Math.min(camX, Math.max(maxX, 0))) + shake.x,
      y: Math.max(0, Math.min(camY, Math.max(maxY, 0))) + shake.y,
    };
  }

  // ---- main draw ----

  draw(nowPerf: number): void {
    const ctx = this.canvas.getContext('2d')!;
    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = '#0a0a0f';
    ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);

    if (!this.snapshot || !this.map || !this.assets.ready) return;
    this.updateWarp(nowPerf);
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
      const cursed = poi.kind === 'chest' && poi.variant === 'cursed';
      // G-09: baú amaldiçoado ganha uma névoa sombria sob o sprite (telegrafa o risco).
      if (cursed) {
        const aura = 0.18 + 0.12 * Math.sin(nowPerf / 280);
        ctx.fillStyle = `rgba(140, 40, 200, ${aura})`;
        ctx.fillRect(sx(poi.x) + 1, sy(poi.y) + 1, TS - 2, TS - 2);
      }
      this.assets.drawObject(ctx, poi.itemId, sx(poi.x), sy(poi.y), SCALE, poi.x, poi.y, nowPerf);
      // gentle highlight pulse on interactables (sanctuary = echo purple, chest = gold, ladder = cyan,
      // cursed chest = ominous magenta).
      const pulse = 0.35 + 0.25 * Math.sin(nowPerf / 350);
      ctx.strokeStyle =
        cursed ? `rgba(214, 76, 255, ${0.55 + 0.3 * Math.sin(nowPerf / 220)})`
        : poi.kind === 'chest' ? `rgba(255, 211, 93, ${pulse})`
        : poi.kind === 'sanctuary' ? `rgba(196, 125, 255, ${pulse})`
        : `rgba(125, 240, 255, ${pulse})`;
      ctx.lineWidth = poi.kind === 'sanctuary' || cursed ? 3 : 2;
      ctx.strokeRect(sx(poi.x) + 4, sy(poi.y) + 4, TS - 8, TS - 8);
      // show [F] hint when player is adjacent to a chest/sanctuary (ladder is auto)
      if ((poi.kind === 'chest' || poi.kind === 'sanctuary') && snap.player) {
        const dist = Math.max(Math.abs(poi.x - snap.player.x), Math.abs(poi.y - snap.player.y));
        if (dist <= 1) {
          const cx = sx(poi.x) + TS / 2;
          const cy = sy(poi.y) - 2;
          ctx.fillStyle = 'rgba(10,12,20,0.88)';
          ctx.fillRect(cx - 7, cy - 12, 14, 12);
          ctx.strokeStyle = 'rgba(255,211,93,0.9)';
          ctx.lineWidth = 1;
          ctx.strokeRect(cx - 7, cy - 12, 14, 12);
          ctx.fillStyle = '#fef3c7';
          ctx.font = 'bold 9px monospace';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText('F', cx, cy - 6);
        }
      }
    }

    // 4. walls and creatures, row by row (y-sorted)
    const creatures: { ry: number; draw: () => void; ref: PlayerDto | MonsterDto; pos: { x: number; y: number } }[] = [];
    for (const m of snap.monsters) {
      const pos = this.actorRenderState(m, serverNow);
      creatures.push({
        ry: pos.y, pos, ref: m,
        draw: () => {
          const px = Math.round(pos.x * TS - cam.x);
          const py = Math.round(pos.y * TS - cam.y);
          this.drawActorSprite(ctx, m.id, px, py, nowPerf, () => {
            this.assets.drawOutfit(
              ctx, m.outfit.lookType, px, py, SCALE,
              m.dir, pos.moving, pos.moving ? serverNow : nowPerf,
              m.outfit.head, m.outfit.body, m.outfit.legs, m.outfit.feet, m.outfit.addons,
            );
          });
        },
      });
    }
    {
      const p = snap.player;
      const pos = this.actorRenderState(p, serverNow);
      creatures.push({
        ry: pos.y, pos, ref: p,
        draw: () => {
          const px = Math.round(pos.x * TS - cam.x);
          const py = Math.round(pos.y * TS - cam.y);
          this.drawActorSprite(ctx, p.id, px, py, nowPerf, () => {
            this.assets.drawOutfit(
              ctx, p.outfit.lookType, px, py, SCALE,
              p.dir, pos.moving, pos.moving ? serverNow : nowPerf,
              p.outfit.head, p.outfit.body, p.outfit.legs, p.outfit.feet, p.outfit.addons,
              p.outfit.mountLookType,
            );
          });
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

    // 4.5 biome atmosphere (G-07): color-grade + fog + vignette + drifting motes over the world,
    //     under the helper/effects/HUD so combat stays legible. Cosmetic only — driven by map.biome.
    this.drawAtmosphere(ctx, map.biome, nowPerf);

    // 5. helper legibility (G-03): intention line + animated reticle + skill telegraph, then the
    //    Echo Break "damage window" aura while the boss is staggered.
    this.drawNavTarget(ctx, snap, cam, nowPerf);
    this.drawHelperIntent(ctx, snap, serverNow, cam, nowPerf);
    this.drawBreakWindow(ctx, snap, serverNow, cam, nowPerf);
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

    // 6.5 death dissolves (pixel disintegration of the dying creature)
    this.dissolves = this.dissolves.filter((d) => this.drawDissolve(ctx, d, nowPerf, cam));
    // 6.6 Echo Break shockwaves bursting from the boss tile
    this.drawShockwaves(ctx, cam, nowPerf);
    // 6.7 CUT-05 skill-cast flourishes (shape-keyed, over the effect sprites)
    this.drawSkillFx(ctx, cam, nowPerf);
    // prune expired impact state so the map can't grow unbounded
    for (const [id, hit] of this.hits) {
      if (nowPerf - hit.start > Math.max(HIT_PUNCH_MS, HIT_FLASH_MS)) this.hits.delete(id);
    }

    // 7. projectiles
    this.projectiles = this.projectiles.filter((p) => {
      const t = (nowPerf - p.start) / p.dur;
      if (t >= 1) return false;
      const x = p.fromX + (p.toX - p.fromX) * t;
      const y = p.fromY + (p.toY - p.fromY) * t;
      this.assets.drawMissile(ctx, p.id, Math.round(x * TS - cam.x), Math.round(y * TS - cam.y), SCALE, p.toX - p.fromX, p.toY - p.fromY);
      return true;
    });

    // 7.5 loot bursting from kills, arcing in and getting sucked into the player
    const lootTarget = this.actorRenderState(snap.player, serverNow);
    this.loot = this.loot.filter((l) => {
      const t = (nowPerf - l.start) / LOOT_FLY_MS;
      if (t >= 1) {
        // arrived: pop the label at the player and play the chained "cha-ching"
        this.texts.push({ x: lootTarget.x, y: lootTarget.y, text: l.text, color: l.color, start: nowPerf, kind: 'info' });
        this.sound?.coinChing(this.lootChain++);
        return false;
      }
      const ease = t * t; // accelerate toward the player (suck-in)
      const lx = l.fromX + (lootTarget.x - l.fromX) * ease;
      const ly = l.fromY + (lootTarget.y - l.fromY) * ease;
      const arc = Math.sin(t * Math.PI) * LOOT_ARC_TILES;
      this.assets.drawObject(
        ctx, l.id, Math.round(lx * TS - cam.x), Math.round((ly - arc) * TS - cam.y), SCALE,
        Math.round(lx), Math.round(ly), nowPerf,
      );
      return true;
    });
    if (this.loot.length === 0) this.lootChain = 0;

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
        // K-04: badge da passiva assinatura (marca/stacks por-alvo) acima do nome
        let badge = '';
        let badgeCol = '#c79bff';
        if (m.traitTag === 'prey') { badge = '◎'; badgeCol = '#ff7a7a'; }
        else if (m.traitTag === 'judged') { badge = '✦'; badgeCol = '#ffe07a'; }
        if (m.traitStacks > 0) {
          badgeCol = m.traitTag === 'frozen' ? '#7df0ff'
            : m.traitTag === 'judged' ? '#ffe07a' : badgeCol;
          badge = (badge ? badge + ' ' : '') + '◆'.repeat(Math.min(m.traitStacks, 5));
        }
        if (badge) {
          ctx.fillStyle = badgeCol;
          ctx.fillText(badge, px + TS / 2, py - 34);
        }
      }
    }

    // 9. floating texts — weighted: outline, pop-in, crit/proc emphasis, magnitude scaling
    this.texts = this.texts.filter((t) => nowPerf - t.start < (t.life ?? (t.kind === 'proc' ? PROC_LIFE_MS : TEXT_LIFE_MS)));
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';
    for (const t of this.texts) {
      const life = t.life ?? (t.kind === 'proc' ? PROC_LIFE_MS : TEXT_LIFE_MS);
      const age = nowPerf - t.start;
      const a01 = age / life;
      const popMs = 150;
      const popIn = age < popMs ? Math.max(easeOutBack(age / popMs), 0.1) : 1;
      const alpha = a01 < 0.62 ? 1 : Math.max(0, 1 - (a01 - 0.62) / 0.38);

      let size: number;
      if (t.kind === 'dmg') size = (t.crit ? 21 : 13) + (t.mag ?? 0.4) * 5;
      else if (t.kind === 'proc') size = 18;
      else if (t.kind === 'heal') size = 14;
      else size = 13;

      const rise = (t.kind === 'proc' ? 16 : 30) * a01;
      const drawX = t.x * TS - cam.x + TS / 2 + (t.vx ?? 0) * TS;
      const drawY = t.y * TS - cam.y - 14 - rise;

      ctx.save();
      ctx.globalAlpha = clamp(alpha, 0, 1);
      ctx.translate(drawX, drawY);
      ctx.scale(popIn, popIn);
      ctx.font = `bold ${size}px Verdana, sans-serif`;
      if (t.crit || t.kind === 'proc') {
        ctx.shadowColor = t.color;
        ctx.shadowBlur = t.crit ? 10 : 6;
      }
      ctx.lineWidth = Math.max(2.2, size * 0.2);
      ctx.strokeStyle = 'rgba(0,0,0,0.85)';
      ctx.strokeText(t.text, 0, 0);
      ctx.shadowBlur = 0;
      ctx.fillStyle = t.color;
      ctx.fillText(t.text, 0, 0);
      ctx.restore();
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

    // 11. Echo Break flash + banner (screen-space, drawn on top of everything); CUT-05 ult bloom
    this.drawUltFlash(ctx, nowPerf);
    this.drawEchoFlash(ctx, nowPerf);
  }

  // ---- G-03 helper legibility ----

  /**
   * G-10: where the auto-loot helper is walking. A pulsing marker on the destination tile when it's
   * on screen, plus an edge pointer toward it when it's off screen — so the autoplay is legible
   * ("she's heading there"). Pure snapshot read; the engine owns the pathing.
   */
  private drawNavTarget(
    ctx: CanvasRenderingContext2D, snap: SnapshotDto, cam: { x: number; y: number }, now: number,
  ): void {
    const nav = snap.run.navTarget;
    if (!nav || snap.run.ended) return;
    const W = this.canvas.width;
    const H = this.canvas.height;
    const tcx = nav.x * TS - cam.x + TS / 2;
    const tcy = nav.y * TS - cam.y + TS / 2;
    const loot = nav.kind === 'chest' || nav.kind === 'sanctuary';
    const color = loot ? '#c47dff' : '#7df0ff'; // eco-purple for loot, cyan for the exit/boss
    const pulse = 0.5 + 0.5 * Math.sin(now / 320);

    ctx.save();
    ctx.globalCompositeOperation = 'lighter';

    if (tcx >= -TS && tcx <= W + TS && tcy >= -TS && tcy <= H + TS) {
      // on-screen: pulsing ring + center dot + a bobbing chevron above the tile
      ctx.strokeStyle = color;
      ctx.fillStyle = color;
      ctx.lineWidth = 2;
      ctx.globalAlpha = 0.22 + 0.26 * pulse;
      ctx.beginPath();
      ctx.arc(tcx, tcy, TS * (0.52 + 0.12 * pulse), 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalAlpha = 0.6;
      ctx.beginPath();
      ctx.arc(tcx, tcy, 2.5, 0, Math.PI * 2);
      ctx.fill();
      const top = tcy - TS * 0.72 + Math.sin(now / 300) * 3;
      ctx.globalAlpha = 0.85;
      ctx.lineWidth = 2.5;
      ctx.beginPath();
      ctx.moveTo(tcx - 6, top - 6);
      ctx.lineTo(tcx, top);
      ctx.lineTo(tcx + 6, top - 6);
      ctx.stroke();
    } else {
      // off-screen: a small arrow pinned to the screen edge, pointing toward the destination
      const cx = W / 2;
      const cy = H / 2;
      const ang = Math.atan2(tcy - cy, tcx - cx);
      const halfW = W / 2 - 26;
      const halfH = H / 2 - 26;
      const t = Math.min(halfW / (Math.abs(Math.cos(ang)) || 1e-6), halfH / (Math.abs(Math.sin(ang)) || 1e-6));
      const ex = cx + Math.cos(ang) * t;
      const ey = cy + Math.sin(ang) * t;
      ctx.translate(ex, ey);
      ctx.rotate(ang);
      ctx.globalAlpha = 0.5 + 0.3 * pulse;
      ctx.fillStyle = color;
      ctx.beginPath();
      ctx.moveTo(11, 0);
      ctx.lineTo(-7, -7);
      ctx.lineTo(-3, 0);
      ctx.lineTo(-7, 7);
      ctx.closePath();
      ctx.fill();
    }
    ctx.restore();
  }

  /**
   * "Read the build's intent": an intention line from the Kaeli to the helper's current target, an
   * animated reticle on that target, and a pulsing telegraph of the footprint the next ready skill
   * will stamp. Pure snapshot reads — the engine still owns targeting and casting.
   */
  private drawHelperIntent(
    ctx: CanvasRenderingContext2D, snap: SnapshotDto, serverNow: number,
    cam: { x: number; y: number }, now: number,
  ): void {
    if (!snap.player.targetId) return;
    const target = snap.monsters.find((m) => m.id === snap.player.targetId);
    if (!target) return;
    const tp = this.actorRenderState(target, serverNow);
    const pp = this.actorRenderState(snap.player, serverNow);
    const tcx = tp.x * TS - cam.x + TS / 2;
    const tcy = tp.y * TS - cam.y + TS / 2;
    const pcx = pp.x * TS - cam.x + TS / 2;
    const pcy = pp.y * TS - cam.y + TS / 2;

    const tele = this.pickTelegraph(snap.player);
    const hot = tele !== null; // a skill is loaded and ready to land — go gold
    const accent = hot ? '#ffd35d' : '#7df0ff';

    this.drawIntentLine(ctx, pcx, pcy, tcx, tcy, now, accent);
    if (tele) this.drawTelegraph(ctx, tele, pcx, pcy, tcx, tcy, now);
    this.drawReticle(ctx, tcx, tcy, now, accent, hot);
  }

  /** The footprint to telegraph: the first ready, offensive skill the helper could land right now
   *  (the ult joins once its gauge is full). Honest legibility, not a tick-exact prediction. */
  private pickTelegraph(player: PlayerDto): SkillShape | null {
    if (!player.autoHelper.skills) return null;
    const skills = player.skills;
    for (let i = 0; i < skills.length; i++) {
      const sk = skills[i];
      const isUlt = i === 4;
      if (isUlt) {
        if (!player.autoHelper.ultimate || player.gauge < 100 || !sk.ready) continue;
      } else if (!sk.ready) continue;
      const shape = this.skillShapes.get(sk.id);
      if (!shape || shape.shape === 'buff' || shape.shape === 'summon') continue;
      return shape;
    }
    return null;
  }

  /** Dashed line Kaeli → target with a bright bead racing along it. */
  private drawIntentLine(
    ctx: CanvasRenderingContext2D, x0: number, y0: number, x1: number, y1: number,
    now: number, color: string,
  ): void {
    const dx = x1 - x0;
    const dy = y1 - y0;
    const len = Math.hypot(dx, dy) || 1;
    // start a little off the Kaeli so the line doesn't cover her sprite
    const t0 = Math.min(TS * 0.4, len * 0.3) / len;
    const sx0 = x0 + dx * t0;
    const sy0 = y0 + dy * t0;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.globalAlpha = 0.4 + 0.2 * Math.sin(now / 160);
    ctx.setLineDash([6, 8]);
    ctx.lineDashOffset = -(now / 28) % INTENT_DASH;
    ctx.beginPath();
    ctx.moveTo(sx0, sy0);
    ctx.lineTo(x1, y1);
    ctx.stroke();
    ctx.setLineDash([]);
    const bt = (now % 700) / 700;
    ctx.globalAlpha = 0.9;
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(sx0 + (x1 - sx0) * bt, sy0 + (y1 - sy0) * bt, 2.4, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  /** Four corner brackets breathing (and spinning when a skill is hot) around the target. */
  private drawReticle(
    ctx: CanvasRenderingContext2D, cx: number, cy: number, now: number, color: string, hot: boolean,
  ): void {
    const r = (TS * RETICLE_SPAN / 2) * (1 + 0.06 * Math.sin(now / 180));
    const arm = TS * 0.22;
    ctx.save();
    ctx.translate(cx, cy);
    if (hot) ctx.rotate((now / 2600) % (Math.PI / 2));
    ctx.strokeStyle = color;
    ctx.lineWidth = hot ? 2.6 : 2;
    ctx.globalAlpha = hot ? 0.85 + 0.15 * Math.sin(now / 90) : 0.7;
    if (hot) { ctx.shadowColor = color; ctx.shadowBlur = 8; }
    for (const [qx, qy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]] as const) {
      const cxn = qx * r;
      const cyn = qy * r;
      ctx.beginPath();
      ctx.moveTo(cxn - qx * arm, cyn);
      ctx.lineTo(cxn, cyn);
      ctx.lineTo(cxn, cyn - qy * arm);
      ctx.stroke();
    }
    ctx.restore();
  }

  /** Pulsing preview of the skill footprint (cone/beam from the Kaeli, ring/area around her or the
   *  target) so the watcher can see the shape before it fires. */
  private drawTelegraph(
    ctx: CanvasRenderingContext2D, tele: SkillShape,
    pcx: number, pcy: number, tcx: number, tcy: number, now: number,
  ): void {
    const dx = tcx - pcx;
    const dy = tcy - pcy;
    const len = Math.hypot(dx, dy) || 1;
    const nx = dx / len;
    const ny = dy / len;
    const range = (tele.range || TELE_DEFAULT_RANGE) * TS;
    const radius = (tele.radius || TELE_DEFAULT_RADIUS) * TS;
    const pulse = 0.16 + 0.12 * Math.abs(Math.sin(now / 130));
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = `rgba(255, 211, 93, ${pulse})`;
    ctx.strokeStyle = `rgba(255, 211, 93, ${pulse + 0.35})`;
    ctx.lineWidth = 1.6;
    switch (tele.shape) {
      case 'nova': case 'ring': case 'field':
        ctx.beginPath(); ctx.arc(pcx, pcy, radius, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
        break;
      case 'area': case 'barrage':
        ctx.beginPath(); ctx.arc(tcx, tcy, radius, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
        break;
      case 'cone': {
        const ang = Math.atan2(ny, nx);
        const half = Math.PI / 6;
        ctx.beginPath();
        ctx.moveTo(pcx, pcy);
        ctx.arc(pcx, pcy, range, ang - half, ang + half);
        ctx.closePath(); ctx.fill(); ctx.stroke();
        break;
      }
      case 'beam': {
        const w = TS * 0.45;
        const ox = -ny * w;
        const oy = nx * w;
        const ex = pcx + nx * range;
        const ey = pcy + ny * range;
        ctx.beginPath();
        ctx.moveTo(pcx + ox, pcy + oy);
        ctx.lineTo(ex + ox, ey + oy);
        ctx.lineTo(ex - ox, ey - oy);
        ctx.lineTo(pcx - ox, pcy - oy);
        ctx.closePath(); ctx.fill(); ctx.stroke();
        break;
      }
      default: // single / chain — punch a tight ring on the target
        ctx.beginPath(); ctx.arc(tcx, tcy, TS * 0.42, 0, Math.PI * 2); ctx.stroke();
    }
    ctx.restore();
  }

  // ---- G-03 Echo Break climax ----

  /** Golden "damage window" aura under the boss while it is staggered (the engine flag drives it). */
  private drawBreakWindow(
    ctx: CanvasRenderingContext2D, snap: SnapshotDto, serverNow: number,
    cam: { x: number; y: number }, now: number,
  ): void {
    if (!snap.run.bossStaggered) return;
    const boss = snap.monsters.find((m) => m.isBoss);
    if (!boss) return;
    const bp = this.actorRenderState(boss, serverNow);
    const cx = bp.x * TS - cam.x + TS / 2;
    const cy = bp.y * TS - cam.y + TS / 2;
    const pulse = 0.5 + 0.5 * Math.sin(now / 110);
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    const r = TS * (0.75 + 0.08 * Math.sin(now / 110));
    const grd = ctx.createRadialGradient(cx, cy, r * 0.2, cx, cy, r);
    grd.addColorStop(0, `rgba(255, 211, 93, ${0.05 + 0.12 * pulse})`);
    grd.addColorStop(0.7, `rgba(255, 170, 60, ${0.12 + 0.18 * pulse})`);
    grd.addColorStop(1, 'rgba(255, 170, 60, 0)');
    ctx.fillStyle = grd;
    ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = `rgba(255, 224, 150, ${0.5 + 0.4 * pulse})`;
    ctx.lineWidth = 2.4;
    ctx.shadowColor = '#ffd35d';
    ctx.shadowBlur = 10;
    ctx.beginPath(); ctx.arc(cx, cy, TS * 0.62, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();
    ctx.save();
    ctx.font = 'bold 12px Verdana, sans-serif';
    ctx.textAlign = 'center';
    ctx.lineJoin = 'round';
    ctx.globalAlpha = 0.7 + 0.3 * pulse;
    ctx.lineWidth = 3;
    ctx.strokeStyle = 'rgba(0,0,0,0.85)';
    ctx.strokeText('⚡ JANELA DE DANO', cx, cy - TS * 0.9);
    ctx.fillStyle = '#ffe07a';
    ctx.fillText('⚡ JANELA DE DANO', cx, cy - TS * 0.9);
    ctx.restore();
  }

  /** Expanding concentric rings bursting from the boss on the break instant. */
  private drawShockwaves(ctx: CanvasRenderingContext2D, cam: { x: number; y: number }, now: number): void {
    this.shockwaves = this.shockwaves.filter((s) => {
      const age = now - s.start;
      if (age >= SHOCKWAVE_MS) return false;
      const t = age / SHOCKWAVE_MS;
      const cx = s.x * TS - cam.x + TS / 2;
      const cy = s.y * TS - cam.y + TS / 2;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = `rgba(255, 224, 150, ${(1 - t) * 0.8})`;
      ctx.lineWidth = (1 - t) * 6 + 1;
      ctx.beginPath(); ctx.arc(cx, cy, t * TS * 3.2, 0, Math.PI * 2); ctx.stroke();
      const t2 = Math.min(1, t * 1.6);
      ctx.strokeStyle = `rgba(255, 255, 255, ${(1 - t2) * 0.7})`;
      ctx.lineWidth = (1 - t) * 3 + 1;
      ctx.beginPath(); ctx.arc(cx, cy, t2 * TS * 2.4, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
      return true;
    });
  }

  // ---- CUT-05 skill-cast FX ----

  /**
   * Renders the active skill-cast flourishes: a bright origin spark on every cast, plus a
   * shape-specific stamp (beam lance, cone wedge, expanding nova/area ring, summon pillar, buff
   * halo, single/chain spark). All additive, eased and faded — purely cosmetic, prunes on expiry.
   */
  private drawSkillFx(ctx: CanvasRenderingContext2D, cam: { x: number; y: number }, now: number): void {
    this.skillFx = this.skillFx.filter((fx) => {
      const life = fx.ult ? SKILL_FX_ULT_MS : SKILL_FX_MS;
      const age = now - fx.start;
      if (age >= life) return false;
      const t = age / life;                       // 0..1
      const grow = 1 - (1 - t) * (1 - t);         // easeOutQuad — the stamp expands then settles
      const fade = t < 0.5 ? 1 : Math.max(0, 1 - (t - 0.5) / 0.5);
      const wide = fx.ult ? 1.5 : 1;
      const core = fx.ult ? '#ffffff' : fx.color;
      const ocx = fx.fromX * TS - cam.x + TS / 2;
      const ocy = fx.fromY * TS - cam.y + TS / 2;
      const acx = fx.aimX * TS - cam.x + TS / 2;
      const acy = fx.aimY * TS - cam.y + TS / 2;
      const dx = acx - ocx;
      const dy = acy - ocy;
      const len = Math.hypot(dx, dy);

      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.shadowColor = fx.color;

      // origin spark — gives every cast a sense of "weight" leaving the Kaeli
      const sparkR = TS * (0.18 + 0.5 * grow) * wide;
      const sg = ctx.createRadialGradient(ocx, ocy, 0, ocx, ocy, sparkR);
      sg.addColorStop(0, this.rgba(core, fade * 0.8));
      sg.addColorStop(0.5, this.rgba(fx.color, fade * 0.35));
      sg.addColorStop(1, this.rgba(fx.color, 0));
      ctx.fillStyle = sg;
      ctx.beginPath(); ctx.arc(ocx, ocy, sparkR, 0, Math.PI * 2); ctx.fill();

      switch (fx.shape) {
        case 'beam': {
          // a tapering lance from the Kaeli through the aim, extended to the skill's reach
          const reach = Math.max(len, fx.range * TS, TS * 2);
          const nx = len > 1 ? dx / len : 0;
          const ny = len > 1 ? dy / len : 0;
          if (nx === 0 && ny === 0) break; // no facing this frame (self-aimed) → spark only
          const ex = ocx + nx * reach;
          const ey = ocy + ny * reach;
          const w = TS * (0.16 + 0.16 * (1 - t)) * wide;
          ctx.shadowBlur = 18 * wide;
          ctx.strokeStyle = this.rgba(fx.color, fade * 0.32);
          ctx.lineWidth = w * 2.4;
          ctx.beginPath(); ctx.moveTo(ocx, ocy); ctx.lineTo(ex, ey); ctx.stroke();
          ctx.strokeStyle = this.rgba(core, fade * 0.95);
          ctx.lineWidth = w;
          ctx.beginPath(); ctx.moveTo(ocx, ocy); ctx.lineTo(ex, ey); ctx.stroke();
          break;
        }
        case 'cone': {
          if (len < 1) break;
          const ang = Math.atan2(dy, dx);
          const half = Math.PI / 6;
          const reach = Math.max(fx.radius, 1) * TS * (0.6 + 0.4 * grow);
          ctx.shadowBlur = 10 * wide;
          ctx.fillStyle = this.rgba(fx.color, fade * 0.26);
          ctx.beginPath();
          ctx.moveTo(ocx, ocy);
          ctx.arc(ocx, ocy, reach, ang - half, ang + half);
          ctx.closePath();
          ctx.fill();
          ctx.strokeStyle = this.rgba(core, fade * 0.7);
          ctx.lineWidth = 2 * wide;
          ctx.stroke();
          break;
        }
        case 'nova': case 'ring': case 'summon': {
          // expanding ring centred on the caster (summon also gets a brief rising pillar)
          const r = Math.max(fx.radius, 1) * TS * grow;
          ctx.shadowBlur = 12 * wide;
          ctx.strokeStyle = this.rgba(core, fade * 0.85);
          ctx.lineWidth = (1 - t) * 5 * wide + 1.5;
          ctx.beginPath(); ctx.arc(ocx, ocy, r, 0, Math.PI * 2); ctx.stroke();
          if (fx.shape === 'summon') {
            const ph = TS * (1.1 + 0.6 * wide) * (1 - t);
            ctx.strokeStyle = this.rgba(core, fade * 0.6);
            ctx.lineWidth = TS * 0.18 * wide;
            ctx.beginPath(); ctx.moveTo(ocx, ocy); ctx.lineTo(ocx, ocy - ph); ctx.stroke();
          }
          break;
        }
        case 'area': case 'field': case 'barrage': {
          // expanding ring at the aimed tile (where the strike lands)
          const r = Math.max(fx.radius, 1) * TS * grow;
          ctx.shadowBlur = 12 * wide;
          ctx.strokeStyle = this.rgba(core, fade * 0.85);
          ctx.lineWidth = (1 - t) * 5 * wide + 1.5;
          ctx.beginPath(); ctx.arc(acx, acy, r, 0, Math.PI * 2); ctx.stroke();
          ctx.fillStyle = this.rgba(fx.color, fade * 0.12);
          ctx.beginPath(); ctx.arc(acx, acy, r, 0, Math.PI * 2); ctx.fill();
          break;
        }
        case 'buff': {
          // a rising halo + upward shimmer around the Kaeli (no aim)
          const r = TS * (0.5 + 0.35 * grow) * wide;
          ctx.shadowBlur = 14 * wide;
          ctx.strokeStyle = this.rgba(core, fade * 0.8);
          ctx.lineWidth = 2.2 * wide;
          ctx.beginPath();
          ctx.ellipse(ocx, ocy + TS * 0.2, r, r * 0.5, 0, 0, Math.PI * 2);
          ctx.stroke();
          break;
        }
        default: {
          // single / chain — a converging ring and a spark on the struck tile
          const r = Math.max(fx.radius, 0.5) * TS * (1.2 - 0.7 * grow);
          ctx.shadowBlur = 10 * wide;
          ctx.strokeStyle = this.rgba(core, fade * 0.85);
          ctx.lineWidth = 2 * wide;
          ctx.beginPath(); ctx.arc(acx, acy, r, 0, Math.PI * 2); ctx.stroke();
        }
      }
      ctx.restore();
      return true;
    });
  }

  /** Brief element-tinted screen bloom on an ultimate cast (cosmetic; pairs with the cast shake). */
  private drawUltFlash(ctx: CanvasRenderingContext2D, now: number): void {
    if (this.ultFlashStart < 0) return;
    const age = now - this.ultFlashStart;
    if (age >= ULT_FLASH_MS) { this.ultFlashStart = -1; return; }
    const t = age / ULT_FLASH_MS;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = this.rgba(this.ultFlashColor, (1 - t) * 0.26);
    ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    ctx.restore();
  }

  /** Parse a `#rgb`/`#rrggbb` hex into an `rgba(...)` string at the given alpha (for FX gradients). */
  private rgba(hex: string, alpha: number): string {
    let h = hex.replace('#', '');
    if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    return `rgba(${r},${g},${b},${clamp(alpha, 0, 1)})`;
  }

  /** Full-screen gold flash + punchy "ECHO BREAK ×N" banner on the break instant. */
  private drawEchoFlash(ctx: CanvasRenderingContext2D, now: number): void {
    if (this.echoFlashStart < 0) return;
    const age = now - this.echoFlashStart;
    if (age >= ECHO_FLASH_MS) { this.echoFlashStart = -1; return; }
    const t = age / ECHO_FLASH_MS;
    const w = this.canvas.width;
    const h = this.canvas.height;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = `rgba(255, 220, 150, ${(1 - t) * 0.45})`;
    ctx.fillRect(0, 0, w, h);
    ctx.restore();
    const pop = age < 160 ? Math.max(easeOutBack(age / 160), 0.1) : 1;
    const alpha = t < 0.7 ? 1 : Math.max(0, 1 - (t - 0.7) / 0.3);
    const label = `⚡ ECHO BREAK${this.echoBreakCount > 1 ? ' ×' + this.echoBreakCount : ''}`;
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.translate(w / 2, h * 0.32);
    ctx.scale(pop, pop);
    ctx.textAlign = 'center';
    ctx.font = 'bold 46px Verdana, sans-serif';
    ctx.lineJoin = 'round';
    ctx.lineWidth = 6;
    ctx.strokeStyle = 'rgba(0,0,0,0.85)';
    ctx.shadowColor = '#ffd35d';
    ctx.shadowBlur = 24;
    ctx.strokeText(label, 0, 0);
    ctx.fillStyle = '#ffe6a0';
    ctx.fillText(label, 0, 0);
    ctx.restore();
  }

  /** G-07: seed the cosmetic mote field for this map (stable per floor → no shimmer on redraw). */
  private buildAtmoParticles(map: MapDto): void {
    this.atmoParticles = [];
    const biome = map.biome;
    if (!biome || biome.particleDensity <= 0) return;
    const count = Math.round(biome.particleDensity * ATMO_PARTICLE_MAX);
    const rnd = mulberry32(((map.floor + 1) * 2654435761) ^ (map.w * 40503) ^ (map.h * 2246822519));
    for (let i = 0; i < count; i++) {
      this.atmoParticles.push({
        x: rnd(), y: rnd(),
        size: 0.6 + rnd() * 1.8,
        speed: 0.25 + rnd() * 0.8,
        phase: rnd() * Math.PI * 2,
      });
    }
  }

  /**
   * G-07: post-process the world with the stratum's palette — a multiply color-grade, a vertical fog
   * haze, an edge vignette and drifting ambient motes. Pure screen-space cosmetics; reads map.biome.
   */
  private drawAtmosphere(ctx: CanvasRenderingContext2D, biome: BiomeDto | undefined, now: number): void {
    if (!biome) return;
    const W = this.canvas.width;
    const H = this.canvas.height;

    if (biome.tintStrength > 0) {
      ctx.globalCompositeOperation = 'multiply';
      ctx.globalAlpha = biome.tintStrength;
      ctx.fillStyle = `rgb(${biome.tintR},${biome.tintG},${biome.tintB})`;
      ctx.fillRect(0, 0, W, H);
      ctx.globalAlpha = 1;
      ctx.globalCompositeOperation = 'source-over';
    }

    if (biome.fogStrength > 0) {
      const g = ctx.createLinearGradient(0, 0, 0, H);
      const c = `${biome.fogR},${biome.fogG},${biome.fogB}`;
      g.addColorStop(0, `rgba(${c},${biome.fogStrength})`);
      g.addColorStop(0.5, `rgba(${c},${biome.fogStrength * 0.35})`);
      g.addColorStop(1, `rgba(${c},${biome.fogStrength})`);
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, W, H);
    }

    if (biome.vignette > 0) {
      const r = Math.max(W, H);
      const vg = ctx.createRadialGradient(W / 2, H / 2, r * 0.34, W / 2, H / 2, r * 0.76);
      vg.addColorStop(0, 'rgba(0,0,0,0)');
      vg.addColorStop(1, `rgba(0,0,0,${biome.vignette})`);
      ctx.fillStyle = vg;
      ctx.fillRect(0, 0, W, H);
    }

    if (this.atmoParticles.length) {
      const t = now / 1000;
      const drift = biome.particleDrift;
      ctx.fillStyle = `rgb(${biome.particleR},${biome.particleG},${biome.particleB})`;
      for (const p of this.atmoParticles) {
        const sway = Math.sin(t * 0.6 + p.phase) * 12;
        const baseY = p.y * H;
        const py = drift === 0
          ? baseY + Math.sin(t * 0.5 + p.phase) * 18
          : baseY + drift * t * p.speed * 22;
        const wx = (((p.x * W + sway) % W) + W) % W;
        const wy = ((py % H) + H) % H;
        ctx.globalAlpha = 0.2 + 0.4 * (0.5 + 0.5 * Math.sin(t * 1.3 + p.phase));
        ctx.beginPath();
        ctx.arc(wx, wy, p.size, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;
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
    // G-07: room-type icons make the route readable at a glance (POIs cover chest/sanctuary/ladder;
    // these add elite/miniboss/hazard/boss). Drawn under the POIs/monsters/player.
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    for (const room of map.rooms) {
      const icon = ROOM_ICONS[room.role];
      if (!icon) continue;
      const cx = (room.x + room.w / 2) * cell;
      const cy = (room.y + room.h / 2) * cell;
      const big = room.role === 'boss' || room.role === 'miniboss';
      const rad = Math.max(big ? cell * 1.7 : cell * 1.2, 3.5);
      ctx.beginPath();
      ctx.arc(cx, cy, rad, 0, Math.PI * 2);
      ctx.fillStyle = icon.color;
      ctx.globalAlpha = 0.92;
      ctx.fill();
      ctx.globalAlpha = 1;
      ctx.lineWidth = 1;
      ctx.strokeStyle = 'rgba(0,0,0,0.6)';
      ctx.stroke();
      ctx.fillStyle = '#10131c';
      ctx.font = `bold ${big ? 9 : 7}px monospace`;
      ctx.fillText(icon.glyph, cx, cy + 0.5);
    }
    for (const poi of map.pois) {
      if (poi.kind === 'chest' && poi.used) continue;
      ctx.fillStyle =
        poi.kind === 'chest' && poi.variant === 'cursed' ? '#d64cff'
        : poi.kind === 'chest' ? '#ffd35d'
        : poi.kind === 'sanctuary' ? '#c47dff'
        : '#7df0ff';
      // sanctuary reads as a slightly larger beacon so the beat is easy to spot on the route
      const sz = poi.kind === 'sanctuary' ? cell + 1 : cell;
      ctx.fillRect(poi.x * cell, poi.y * cell, sz, sz);
    }
    for (const m of this.snapshot.monsters) {
      ctx.fillStyle = m.isBoss ? '#ff8c4d' : '#c03030';
      ctx.fillRect(m.x * cell, m.y * cell, cell, cell);
    }
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(this.snapshot.player.x * cell - 1, this.snapshot.player.y * cell - 1, cell + 2, cell + 2);
  }
}
