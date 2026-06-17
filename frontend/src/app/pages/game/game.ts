import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, effect, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { GameClientService } from '../../core/game-client.service';
import { GameRenderer } from '../../core/renderer';
import { ItemIcon } from '../../core/item-icon';
import { SoundService } from '../../core/sound.service';
import { SnapshotDto } from '../../core/types';

// G-01: alinhado abaixo do passo do player (~294ms a PlayerBaseSpeed=340) pra resend confiável.
const MOVE_HEARTBEAT_MS = 200;
const RESUME_TOAST_MS = 2500;

const MOVE_KEYS: Readonly<Record<string, Readonly<{ x: number; y: number }>>> = {
  KeyW: { x: 0, y: -1 },
  KeyA: { x: -1, y: 0 },
  KeyS: { x: 0, y: 1 },
  KeyD: { x: 1, y: 0 },
  KeyQ: { x: -1, y: -1 },
  KeyE: { x: 1, y: -1 },
  KeyZ: { x: -1, y: 1 },
  KeyC: { x: 1, y: 1 },
  ArrowUp: { x: 0, y: -1 },
  ArrowLeft: { x: -1, y: 0 },
  ArrowDown: { x: 0, y: 1 },
  ArrowRight: { x: 1, y: 0 },
};

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [ItemIcon],
  template: `
    <div class="game-root" tabindex="0" #root>
      <canvas #cv class="game-canvas"></canvas>
      @if (resumeToast()) {
        <div class="resume-toast">Run retomada</div>
      }

      <!-- top HUD -->
      <div class="hud top">
        @if (snapshot(); as s) {
          <div class="hpbar">
            <div class="label">{{ s.player.hp }} / {{ s.player.maxHp }}</div>
            <div class="bar hp"><div class="fill" [style.width.%]="(100 * s.player.hp) / s.player.maxHp"></div></div>
            <div class="bar xp"><div class="fill" [style.width.%]="(100 * s.run.xp) / s.run.xpNext"></div></div>
            <div class="sub">Lv {{ s.run.level }} · {{ s.run.kills }} abates · 🪙 {{ s.run.gold }} · {{ s.run.tierName }}</div>
            @if (hasEquipmentStats(s.player.equipmentStats)) {
              <div class="gear-stats">{{ equipmentStatsLabel(s.player.equipmentStats) }}</div>
            }
          </div>
          @if (s.run.bossHp !== null) {
            <div class="bossbar">
              <div class="bname">👑 {{ s.run.bossName }}</div>
              <div class="bar boss"><div class="fill" [style.width.%]="(100 * s.run.bossHp!) / s.run.bossMaxHp!"></div></div>
              @if (s.run.bossPostureMax) {
                <div class="bar posture" [class.high]="posturePct(s.run) >= 80" [class.staggered]="s.run.bossStaggered">
                  <div class="fill" [style.width.%]="posturePct(s.run)"></div>
                </div>
                <div class="posture-label">
                  @if (s.run.bossStaggered) {
                    <span class="broken">⚡ ECHO BREAK · dano ×{{ activeMult(s.run.bossPostureCycle) }}</span>
                  } @else {
                    <span>Postura → quebra ×{{ nextMult(s.run.bossPostureCycle) }}</span>
                  }
                </div>
              }
            </div>
          }
          <div class="buffs">
            @for (b of s.player.activeBuffs; track b) { <span class="buff">{{ buffLabel(b) }}</span> }
            @for (c of s.player.activeConditions; track c) {
              <span class="buff cond" [class]="'buff cond cond-' + c">{{ condLabel(c) }}</span>
            }
          </div>
          <button class="stance" [class.fixed]="!s.player.canToggleStance"
                  [disabled]="!s.player.canToggleStance" (click)="toggleStance()"
                  title="Tab alterna a postura">
            <span>{{ s.player.className }}</span>
            <b>{{ elementLabel(s.player.stanceElement) }}</b>
            @if (s.player.canToggleStance) { <small>TAB</small> }
          </button>
          <div class="helper-panel" title="Controle do helper de combate">
            <span>Helper</span>
            <button [class.on]="s.player.autoHelper.targeting"
                    (click)="setAutoHelper('targeting', !s.player.autoHelper.targeting)">Alvo</button>
            <button [class.on]="s.player.autoHelper.skills"
                    (click)="setAutoHelper('skills', !s.player.autoHelper.skills)">Skills</button>
            <button [class.on]="s.player.autoHelper.ultimate"
                    (click)="setAutoHelper('ultimate', !s.player.autoHelper.ultimate)">Ult</button>
            <button class="wide" [class.on]="s.player.autoHelper.targetPreference === 'nearest'"
                    (click)="toggleTargetPreference()">
              Pref: {{ targetPreferenceLabel(s.player.autoHelper.targetPreference) }}
            </button>
            <button [class.on]="s.player.autoHelper.movementMode === 'none'"
                    (click)="setAutoHelperMovement('none')">Stand</button>
            <button [class.on]="s.player.autoHelper.movementMode === 'follow'"
                    (click)="setAutoHelperMovement('follow')">Follow</button>
            <button [class.on]="s.player.autoHelper.movementMode === 'avoid'"
                    (click)="setAutoHelperMovement('avoid')">Avoid</button>
          </div>
        }
        <button class="btn secondary leave" (click)="leave()">Sair</button>
        <button class="btn secondary bag-toggle" [class.on]="showBag()" (click)="toggleBag()" title="Mochila da caçada (B)">🎒</button>
        <button class="btn secondary snd-toggle" [class.muted]="sound.muted()" (click)="sound.toggleMute()" [title]="sound.muted() ? 'Som desligado (M)' : 'Som ligado (M)'">{{ sound.muted() ? '🔇' : '🔊' }}</button>
      </div>

      <!-- minimap -->
      <canvas #mini class="minimap" width="160" height="160"></canvas>

      <!-- skill bar -->
      @if (snapshot(); as s) {
        <div class="hud skills">
          @for (sk of s.player.skills; track sk.id; let i = $index) {
            <button class="skill" [class.ready]="sk.ready" [class.ult]="i === 4"
                    [title]="sk.description" (click)="cast(i)">
              <span class="key">{{ ['1','2','3','4','R'][i] }}</span>
              <span class="name">{{ sk.name }}</span>
              <span class="element">{{ elementLabel(sk.element) }}</span>
              @if (i === 4) {
                <div class="gaugewrap"><div class="gauge" [style.width.%]="s.player.gauge"></div></div>
              } @else if (!sk.ready) {
                <div class="cd" [style.height.%]="(100 * sk.cooldownRemainingMs) / sk.cooldownTotalMs"></div>
              }
            </button>
          }
          <button class="skill potion"
                  [class.ready]="s.player.potionCharges > 0 && s.player.potionCooldownRemainingMs === 0"
                  [disabled]="s.player.potionCharges === 0"
                  [title]="potionTitle(s.player.potionHealPct)"
                  (click)="usePotion()">
            <span class="key">T</span>
            <app-item-icon [itemId]="s.player.potionItemId" [size]="28" />
            <span class="charges">{{ s.player.potionCharges }}/{{ s.player.potionMaxCharges }}</span>
            @if (s.player.potionCooldownRemainingMs > 0) {
              <div class="cd" [style.height.%]="(100 * s.player.potionCooldownRemainingMs) / s.player.potionCooldownTotalMs"></div>
            }
          </button>
        </div>

        @if (showBag()) {
          <div class="bagpanel">
            <div class="baghead"><b>Mochila da caçada</b><span>🪙 {{ s.run.gold }}</span></div>
            @if (s.run.items.length) {
              <div class="baggrid">
                @for (item of s.run.items; track item.itemId) {
                  <div class="bagitem" [title]="item.name">
                    <app-item-icon [itemId]="item.itemId" [size]="40" />
                    <span>×{{ item.count }}</span>
                  </div>
                }
              </div>
            } @else {
              <p class="bagempty">Nada coletado ainda — vá caçar!</p>
            }
          </div>
        }
      }

      <!-- card offer -->
      @if (snapshot()?.run?.offer; as offer) {
        <div class="overlay cards">
          <h2>Level up! Escolha um eco:</h2>
          <div class="choices">
            @for (c of offer; track c.id; let i = $index) {
              <button class="choice" (click)="chooseCard(c.id)">
                <b>{{ c.name }}</b>
                <p>{{ c.description }}</p>
                <span class="stacks">{{ c.currentStacks }}/3</span>
                <small class="card-key">[{{ i + 1 }}]</small>
              </button>
            }
          </div>
        </div>
      }

      <!-- run end -->
      @if (snapshot()?.run?.ended; as end) {
        <div class="overlay end">
          <h1 [class.victory]="end.victory">{{ end.victory ? 'VITÓRIA' : 'DERROTA' }}</h1>
          <p class="reason">{{ end.reason }}</p>
          <div class="stats">
            <div class="stat"><b>{{ end.kills }}</b><span>abates</span></div>
            <div class="stat"><b>{{ end.runLevel }}</b><span>level</span></div>
            <div class="stat"><b>{{ end.goldEarned }}</b><span>🪙 ouro</span></div>
            <div class="stat"><b>{{ end.kaerosEarned }}</b><span>✦ kaeros</span></div>
            <div class="stat"><b>{{ end.accountXpEarned }}</b><span>XP de conta</span></div>
            <div class="stat"><b>{{ formatTime(end.durationMs) }}</b><span>duração</span></div>
          </div>
          @if (end.items.length) {
            <div class="loot">
              @for (item of end.items; track item.itemId) {
                <div class="lootitem" [title]="item.name">
                  <app-item-icon [itemId]="item.itemId" [size]="40" />
                  <span>×{{ item.count }}</span>
                </div>
              }
            </div>
          }
          @for (note of end.dailyProgressNotes; track note) { <p class="note">📜 {{ note }}</p> }
          <div class="actions">
            <button class="btn" (click)="again()">JOGAR DE NOVO</button>
            <button class="btn secondary" (click)="leave()">VOLTAR À CAÇADA</button>
          </div>
        </div>
      }

      @if (!snapshot()) {
        <div class="overlay"><h2>Gerando dungeon...</h2></div>
      }
    </div>
  `,
  styles: [`
    .game-root { position: fixed; inset: 0; background: #06060a; outline: none; overflow: hidden; }
    .game-canvas { position: absolute; inset: 0; image-rendering: pixelated; }
    .resume-toast {
      position: absolute; top: 18px; left: 50%; z-index: 30; transform: translateX(-50%);
      padding: 9px 16px; border: 1px solid #2dd4bf; border-radius: 8px;
      background: rgba(10, 30, 32, 0.94); color: #8bfff1; font-size: 13px; font-weight: 800;
    }
    .hud.top { position: absolute; top: 12px; left: 14px; right: 14px; display: flex; gap: 18px; align-items: flex-start; pointer-events: none; }
    .hud.top .leave { pointer-events: auto; margin-left: auto; }
    .hpbar { width: 280px; background: rgba(10,10,16,0.8); border: 1px solid #2c2c3e; border-radius: 10px; padding: 8px 12px; }
    .hpbar .label { font-size: 12px; font-weight: 700; text-align: center; }
    .bar { height: 10px; background: #1c1c28; border-radius: 5px; overflow: hidden; margin-top: 4px; }
    .bar.xp { height: 4px; }
    .bar .fill { height: 100%; background: linear-gradient(90deg, #22c55e, #15803d); }
    .bar.xp .fill { background: #7df0ff; }
    .bar.boss .fill { background: linear-gradient(90deg, #f97316, #b91c1c); }
    .sub { font-size: 11px; color: #9c9ab0; margin-top: 4px; }
    .gear-stats { color: #2dd4bf; font-size: 10px; margin-top: 3px; }
    .bossbar { flex: 1; max-width: 420px; background: rgba(10,10,16,0.8); border: 1px solid #432; border-radius: 10px; padding: 8px 12px; }
    .bname { font-size: 13px; font-weight: 800; color: #ff8c4d; }
    .bar.boss { height: 12px; }
    .bar.posture { height: 5px; margin-top: 3px; background: #2a2118; }
    .bar.posture .fill { background: linear-gradient(90deg, #fbbf24, #f59e0b); transition: width 0.12s linear; }
    .bar.posture.high .fill { animation: posturePulse 0.5s infinite alternate; }
    .bar.posture.staggered { box-shadow: 0 0 10px #fbbf24; }
    .bar.posture.staggered .fill { background: linear-gradient(90deg, #fff, #fbbf24); }
    .posture-label { font-size: 10px; color: #e8a93c; margin-top: 2px; font-weight: 800; }
    .posture-label .broken { color: #fff; animation: posturePulse 0.4s infinite alternate; }
    @keyframes posturePulse { from { opacity: 0.45; } to { opacity: 1; } }
    .buffs { display: flex; gap: 6px; }
    .buff { background: rgba(45, 212, 191, 0.18); border: 1px solid #2dd4bf; color: #2dd4bf; font-size: 11px; font-weight: 800; border-radius: 6px; padding: 3px 8px; }
    .buff.cond { background: rgba(255, 93, 93, 0.18); border-color: #ff5d5d; color: #ff5d5d; }
    .buff.cond-poison { background: rgba(110, 231, 110, 0.18); border-color: #6ee76e; color: #6ee76e; }
    .buff.cond-fire { background: rgba(255, 140, 60, 0.18); border-color: #ff8c3c; color: #ff8c3c; }
    .buff.cond-energy { background: rgba(196, 125, 255, 0.18); border-color: #c47dff; color: #c47dff; }
    .buff.cond-slow { background: rgba(125, 240, 255, 0.18); border-color: #7df0ff; color: #7df0ff; }
    .stance {
      pointer-events: auto; min-width: 116px; border: 1px solid #2dd4bf; border-radius: 9px;
      background: rgba(10, 30, 32, 0.92); color: #b8fff5; padding: 6px 10px;
      display: grid; grid-template-columns: 1fr auto; gap: 1px 8px; text-align: left;
    }
    .stance span { grid-column: 1 / -1; color: #8bfff1; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .stance.fixed { border-color: #3a3a4c; background: rgba(16,16,26,0.9); }
    .helper-panel {
      pointer-events: auto; width: 260px; min-height: 78px; border: 1px solid #3a3a4c; border-radius: 9px;
      background: rgba(16,16,26,0.9); color: #cfcde0; padding: 6px 8px;
      display: grid; grid-template-columns: repeat(4, 1fr); gap: 5px; align-items: center;
    }
    .helper-panel span {
      grid-column: 1 / -1; color: #8bfff1; font-size: 10px; font-weight: 900; text-transform: uppercase;
    }
    .helper-panel button {
      height: 24px; border: 1px solid #4a4a5e; border-radius: 7px; background: #272738; color: #8b8a9c;
      font-size: 10px; font-weight: 900; padding: 0; line-height: 1;
    }
    .helper-panel button.on {
      border-color: #2dd4bf; background: rgba(45, 212, 191, 0.22); color: #8bfff1;
    }
    .helper-panel button.wide { grid-column: span 1; font-size: 9px; }
    .minimap { position: absolute; right: 14px; top: 64px; border: 1px solid #2c2c3e; border-radius: 8px; background: #000; opacity: 0.9; }
    .hud.skills { position: absolute; bottom: 16px; left: 50%; transform: translateX(-50%); display: flex; gap: 10px; }
    .skill {
      position: relative; width: 104px; height: 72px; border-radius: 10px; overflow: hidden;
      background: rgba(16,16,26,0.9); border: 2px solid #2c2c3e; color: #cfcde0;
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px;
    }
    .skill.ready { border-color: #2dd4bf; }
    .skill.ult.ready { border-color: #e8a93c; box-shadow: 0 0 12px rgba(232,169,60,0.5); }
    .skill .key { font-weight: 900; font-size: 15px; color: #fff; }
    .skill .name { font-size: 10px; text-align: center; line-height: 1.1; }
    .skill .element { color: #707088; font-size: 9px; }
    .skill .cd { position: absolute; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.65); pointer-events: none; }
    .gaugewrap { width: 80%; height: 5px; background: #1c1c28; border-radius: 3px; overflow: hidden; }
    .gauge { height: 100%; background: linear-gradient(90deg, #e8a93c, #fbbf24); }
    .skill.potion { width: 72px; cursor: pointer; }
    .skill.potion.ready { border-color: #ff6b8b; box-shadow: 0 0 10px rgba(255,107,139,0.4); }
    .skill.potion:disabled { opacity: 0.45; cursor: default; }
    .skill.potion .charges { font-size: 11px; font-weight: 800; color: #ffd1dc; }
    .bag-toggle, .snd-toggle { pointer-events: auto; font-size: 16px; padding: 4px 9px; }
    .bag-toggle.on { border-color: #2dd4bf; color: #8bfff1; }
    .snd-toggle.muted { opacity: 0.5; }
    .bagpanel {
      position: absolute; right: 14px; bottom: 100px; width: 250px; max-height: 46vh; overflow-y: auto;
      background: rgba(10,10,16,0.94); border: 1px solid #2c2c3e; border-radius: 10px; padding: 10px 12px; z-index: 15;
    }
    .baghead { display: flex; justify-content: space-between; align-items: center; font-size: 13px; color: #cfcde0; margin-bottom: 8px; }
    .baghead span { color: #ffd35d; font-weight: 800; }
    .baggrid { display: grid; grid-template-columns: repeat(auto-fill, minmax(56px, 1fr)); gap: 8px; }
    .bagitem { background: #15151f; border: 1px solid #2c2c3e; border-radius: 8px; padding: 5px; display: flex; flex-direction: column; align-items: center; font-size: 11px; color: #9c9ab0; }
    .bagempty { color: #707088; font-size: 12px; margin: 4px 0; }
    .overlay {
      position: absolute; inset: 0; background: rgba(5,5,10,0.88); z-index: 20;
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 14px;
    }
    .overlay.cards .choices { display: flex; gap: 16px; }
    .choice {
      width: 210px; min-height: 130px; border-radius: 12px; border: 2px solid #2dd4bf;
      background: linear-gradient(180deg, #16242a, #101018); color: inherit; padding: 14px;
      display: flex; flex-direction: column; gap: 6px; text-align: left;
      transition: transform 0.1s;
    }
    .choice:hover { transform: translateY(-4px); }
    .choice p { margin: 0; color: #9c9ab0; font-size: 13px; }
    .choice .stacks { color: #707088; font-size: 11px; }
    .choice .card-key { color: rgba(125, 240, 255, 0.55); font-size: 11px; font-weight: 700; font-family: monospace; align-self: flex-end; }
    .overlay.end h1 { font-size: 52px; margin: 0; color: #ff5d5d; letter-spacing: 4px; }
    .overlay.end h1.victory { color: #2dd4bf; }
    .reason { color: #9c9ab0; margin: 0; }
    .stats { display: flex; gap: 14px; flex-wrap: wrap; justify-content: center; }
    .stat { background: #15151f; border: 1px solid #2c2c3e; border-radius: 10px; padding: 12px 20px; text-align: center; min-width: 90px; }
    .stat b { display: block; font-size: 22px; }
    .stat span { color: #9c9ab0; font-size: 12px; }
    .loot { display: flex; gap: 10px; flex-wrap: wrap; justify-content: center; max-width: 600px; }
    .lootitem { background: #15151f; border: 1px solid #2c2c3e; border-radius: 8px; padding: 6px; display: flex; flex-direction: column; align-items: center; font-size: 11px; }
    .note { color: #e8a93c; font-size: 13px; margin: 0; }
    .actions { display: flex; gap: 14px; margin-top: 8px; }
  `],
})
export class GamePage implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('cv') cv!: ElementRef<HTMLCanvasElement>;
  @ViewChild('mini') mini!: ElementRef<HTMLCanvasElement>;
  @ViewChild('root') root!: ElementRef<HTMLDivElement>;

  readonly snapshot = computed(() => this.client.snapshot());
  readonly busyChoosing = signal(false);
  readonly resumeToast = signal(false);
  readonly showBag = signal(false);

  private renderer: GameRenderer | null = null;
  private raf = 0;
  private tier = 1;
  private waifuId: string | undefined;
  private keys = new Set<string>();
  private lastDir = { x: 0, y: 0 };
  private moveHeartbeat = 0;
  private resumeToastTimer = 0;
  private ladderTriggered = false;

  constructor(
    private readonly client: GameClientService,
    private readonly assets: AssetsService,
    private readonly api: ApiService,
    readonly sound: SoundService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {
    effect(() => {
      const map = this.client.map();
      if (map && this.renderer) this.renderer.setMap(map);
      this.ladderTriggered = false;
    });
    effect(() => {
      const snap = this.client.snapshot();
      if (snap && this.renderer) this.renderer.setSnapshot(snap, performance.now());
      if (snap && !snap.run.ended && !snap.run.offer) this.tryAutoLadder(snap);
    });
  }

  ngOnInit(): void {
    this.tier = Number(this.route.snapshot.paramMap.get('tier') ?? '1');
    this.waifuId = this.route.snapshot.queryParamMap.get('waifu') ?? undefined;
  }

  async ngAfterViewInit(): Promise<void> {
    const canvas = this.cv.nativeElement;
    const resize = () => {
      canvas.width = window.innerWidth;
      canvas.height = window.innerHeight;
    };
    resize();
    window.addEventListener('resize', resize);

    await this.assets.load();
    // best-effort atlas warmup; the renderer also lazy-loads on demand
    void this.assets.preload(['outfits', 'objects', 'effects', 'missiles']).catch(() => undefined);
    this.renderer = new GameRenderer(canvas, this.assets, this.sound);
    (window as unknown as Record<string, unknown>)['__kaezanRenderer'] = this.renderer; // debug/e2e hook
    const map = this.client.map();
    if (map) this.renderer.setMap(map);

    try {
      const joined = await this.client.joinRun(this.tier, this.waifuId, undefined, true);
      if (joined.resumed) {
        this.resumeToast.set(true);
        this.resumeToastTimer = window.setTimeout(() => this.resumeToast.set(false), RESUME_TOAST_MS);
      }
    } catch (err) {
      console.error('joinRun failed', err);
      alert((err as Error).message);
      void this.router.navigate(['/hunt']);
      return;
    }
    this.root.nativeElement.focus();

    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
    window.addEventListener('blur', this.onBlur);
    canvas.addEventListener('mousedown', this.onClick);
    canvas.addEventListener('mousemove', this.onMove);
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    this.moveHeartbeat = window.setInterval(this.resendMoveDir, MOVE_HEARTBEAT_MS);

    const loop = (now: number) => {
      this.renderer?.draw(now);
      if (this.mini?.nativeElement) this.renderer?.drawMinimap(this.mini.nativeElement);
      this.raf = requestAnimationFrame(loop);
    };
    this.raf = requestAnimationFrame(loop);
  }

  // ---- input ----

  private onKeyDown = (e: KeyboardEvent): void => {
    const move = MOVE_KEYS[e.code];
    if (move) {
      if (!e.repeat) {
        this.keys.add(e.code);
        this.sendMoveDir();
      }
      e.preventDefault();
      return;
    }
    if (e.repeat) return;
    const k = e.key.toLowerCase();
    if (k === '1' || k === '2' || k === '3') {
      const idx = Number(k) - 1;
      const offer = this.snapshot()?.run?.offer;
      if (offer) { const c = offer[idx]; if (c) this.chooseCard(c.id); }
      else this.cast(idx);
    } else if (k === '4') this.cast(3);
    else if (k === 'r') this.cast(4);
    else if (k === 't') this.usePotion();
    else if (k === '5') this.cycleMovementMode();
    else if (k === 'b') this.toggleBag();
    else if (k === 'm') this.sound.toggleMute();
    else if (k === 'f') this.interactNearest();
    else if (k === 'tab') { this.toggleStance(); e.preventDefault(); }
    else if (k === ' ') { this.targetNearest(); e.preventDefault(); }
    else if (k === 'escape') this.leave();
  };

  private onKeyUp = (e: KeyboardEvent): void => {
    if (this.keys.delete(e.code)) {
      this.sendMoveDir();
      e.preventDefault();
    }
  };

  private onBlur = (): void => {
    if (this.keys.size === 0) return;
    this.keys.clear();
    this.sendMoveDir();
  };

  private resendMoveDir = (): void => {
    if (this.lastDir.x === 0 && this.lastDir.y === 0) return;
    this.client.move(this.lastDir.x, this.lastDir.y);
  };

  private sendMoveDir(): void {
    let dx = 0;
    let dy = 0;
    for (const code of this.keys) {
      const move = MOVE_KEYS[code];
      if (!move) continue;
      dx += move.x;
      dy += move.y;
    }
    dx = Math.sign(dx);
    dy = Math.sign(dy);
    if (dx !== this.lastDir.x || dy !== this.lastDir.y) {
      this.lastDir = { x: dx, y: dy };
      this.client.move(dx, dy);
    }
  }

  private onClick = (e: MouseEvent): void => {
    if (!this.renderer) return;
    const rect = this.cv.nativeElement.getBoundingClientRect();
    const tile = this.renderer.screenToTile(e.clientX - rect.left, e.clientY - rect.top, performance.now());
    if (!tile) return;
    const monster = this.renderer.monsterAtTile(tile.x, tile.y);
    if (monster) {
      this.client.setTarget(monster.id);
    } else {
      this.client.interact(tile.x, tile.y);
    }
  };

  private onMove = (e: MouseEvent): void => {
    if (!this.renderer) return;
    const rect = this.cv.nativeElement.getBoundingClientRect();
    this.renderer.hoverTile = this.renderer.screenToTile(e.clientX - rect.left, e.clientY - rect.top, performance.now());
  };

  private targetNearest(): void {
    const snap = this.snapshot();
    if (!snap) return;
    const p = snap.player;
    const nearest = [...snap.monsters]
      .sort((a, b) =>
        Math.max(Math.abs(a.x - p.x), Math.abs(a.y - p.y)) - Math.max(Math.abs(b.x - p.x), Math.abs(b.y - p.y)))[0];
    if (nearest) this.client.setTarget(nearest.id);
  }

  cast(slot: number): void {
    this.client.castSkill(slot);
  }

  usePotion(): void {
    this.client.usePotion();
  }

  toggleBag(): void {
    this.showBag.update((v) => !v);
  }

  potionTitle(healPct: number): string {
    return `Poção de cura — restaura ${Math.round(healPct * 100)}% da vida (tecla 5)`;
  }

  toggleStance(): void {
    this.client.toggleStance();
  }

  setAutoHelper(module: 'targeting' | 'skills' | 'ultimate', enabled: boolean): void {
    const current = this.snapshot()?.player.autoHelper;
    if (!current) return;
    this.client.setAutoHelper(
      module === 'targeting' ? enabled : current.targeting,
      module === 'skills' ? enabled : current.skills,
      module === 'ultimate' ? enabled : current.ultimate,
      current.targetPreference,
      current.movementMode,
    );
  }

  cycleMovementMode(): void {
    const current = this.snapshot()?.player.autoHelper;
    if (!current) return;
    const next = current.movementMode === 'none' ? current.defaultMovementMode : 'none';
    this.setAutoHelperMovement(next);
  }

  setAutoHelperMovement(movementMode: 'none' | 'follow' | 'avoid'): void {
    const current = this.snapshot()?.player.autoHelper;
    if (!current) return;
    this.client.setAutoHelper(
      current.targeting,
      current.skills,
      current.ultimate,
      current.targetPreference,
      movementMode,
    );
  }

  toggleTargetPreference(): void {
    const current = this.snapshot()?.player.autoHelper;
    if (!current) return;
    this.client.setAutoHelper(
      current.targeting,
      current.skills,
      current.ultimate,
      current.targetPreference === 'nearest' ? 'lowestHp' : 'nearest',
      current.movementMode,
    );
  }

  targetPreferenceLabel(preference: string): string {
    return preference === 'nearest' ? 'Perto' : 'HP';
  }

  chooseCard(cardId: string): void {
    this.client.chooseCard(cardId);
  }

  private interactNearest(): void {
    const snap = this.snapshot();
    const map = this.client.map();
    if (!snap || !map) return;
    const { x: px, y: py } = snap.player;
    const poi = map.pois.find(p => !p.used && Math.max(Math.abs(p.x - px), Math.abs(p.y - py)) <= 1);
    if (poi) this.client.interact(poi.x, poi.y);
  }

  private tryAutoLadder(snap: SnapshotDto): void {
    const map = this.client.map();
    if (!map || this.ladderTriggered) return;
    const ladder = map.pois.find(p => p.kind === 'ladder' && !p.used && p.x === snap.player.x && p.y === snap.player.y);
    if (ladder) {
      this.ladderTriggered = true;
      this.client.interact(ladder.x, ladder.y);
    }
  }

  buffLabel(buff: string): string {
    return {
      atk: 'ATK+', haste: 'VEL+', atkspeed: 'AS+', shield: 'ESCUDO', crit: 'CRIT+',
      bloodrage: 'BLOOD RAGE', aegis: 'AEGIS',
    }[buff] ?? buff;
  }

  condLabel(condition: string): string {
    return {
      poison: 'PSN', fire: 'BRN', energy: 'ZAP', slow: 'SLOW', bleed: 'BLD',
      curse: 'CURSE', freeze: 'FRZ', drown: 'DRW', dazzle: 'DZL',
    }[condition] ?? condition.toUpperCase();
  }

  elementLabel(element: string): string {
    return {
      physical: 'Fisico', holy: 'Sagrado', ice: 'Gelo',
      earth: 'Terra', energy: 'Energia', fire: 'Fogo', support: 'Suporte',
    }[element] ?? element;
  }

  formatTime(ms: number): string {
    const s = Math.floor(ms / 1000);
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  }

  // ---- F-E: boss posture (echo break) ----
  private readonly staggerMults = [2.5, 3.5, 5.0, 6.5];

  posturePct(run: { bossPosture: number | null; bossPostureMax: number | null }): number {
    if (!run.bossPostureMax) return 0;
    return Math.min(100, (100 * (run.bossPosture ?? 0)) / run.bossPostureMax);
  }

  /** Multiplier the next break will grant (cycle = breaks already taken). */
  nextMult(cycle: number): string {
    return this.staggerMults[Math.min(cycle, this.staggerMults.length - 1)].toFixed(1);
  }

  /** Multiplier active during the current stagger (cycle already incremented at break). */
  activeMult(cycle: number): string {
    return this.staggerMults[Math.min(Math.max(cycle - 1, 0), this.staggerMults.length - 1)].toFixed(1);
  }

  hasEquipmentStats(stats: { attackBonus: number; maxHpBonus: number; damageReduction: number; moveSpeedPercent: number }): boolean {
    return !!(stats.attackBonus || stats.maxHpBonus || stats.damageReduction || stats.moveSpeedPercent);
  }

  equipmentStatsLabel(stats: { attackBonus: number; maxHpBonus: number; damageReduction: number; moveSpeedPercent: number }): string {
    const values = [
      stats.attackBonus ? `+${stats.attackBonus.toFixed(1)} ATK` : '',
      stats.maxHpBonus ? `+${stats.maxHpBonus} HP` : '',
      stats.damageReduction ? `${(stats.damageReduction * 100).toFixed(1)}% DEF` : '',
      stats.moveSpeedPercent ? `+${(stats.moveSpeedPercent * 100).toFixed(1)}% VEL` : '',
    ].filter(Boolean);
    return `Equip: ${values.join(' · ')}`;
  }

  async again(): Promise<void> {
    await this.client.joinRun(this.tier, this.waifuId, undefined, false);
    void this.api.refreshAccount();
  }

  async leave(): Promise<void> {
    await this.client.leave(true);
    void this.api.refreshAccount();
    void this.router.navigate(['/hunt']);
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.raf);
    window.clearInterval(this.moveHeartbeat);
    window.clearTimeout(this.resumeToastTimer);
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
    window.removeEventListener('blur', this.onBlur);
    void this.client.leave();
  }
}
