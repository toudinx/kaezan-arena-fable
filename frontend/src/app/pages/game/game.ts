import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, effect, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { normalizeFarmRunCount, readFarmRunCount } from '../../core/farm-settings';
import { GameClientService, GameMode } from '../../core/game-client.service';
import { GameRenderer } from '../../core/renderer';
import { ItemIcon } from '../../core/item-icon';
import { PerfRing } from '../../core/perf-ring';
import { SoundService } from '../../core/sound.service';
import { AutoHelperSettingsDto, SnapshotDto } from '../../core/types';

// G-01: aligned below the player step (~294ms at PlayerBaseSpeed=340) for reliable resends.
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
    <div class="game-root" tabindex="0" #root [style.--accent-el]="accentEl()">
      <canvas #cv class="game-canvas"></canvas>
      <!-- cathedral gloom: vignette + element haze framing the arena -->
      <div class="arena-veil" aria-hidden="true"></div>
      @if (resumeToast()) {
        <div class="resume-toast">Run resumed</div>
      }
      @if (visiblePerfReadout(); as perf) {
        <div class="perf-overlay">
          <div>frame p50 {{ perf.frameP50.toFixed(1) }}ms &middot; p95 {{ perf.frameP95.toFixed(1) }}ms</div>
          <div>draw p95 {{ perf.drawP95.toFixed(1) }}ms &middot; long frames {{ perf.longFrames }}</div>
          <div>snapshot age {{ perf.snapAgeMs.toFixed(0) }}ms</div>
          <div>events {{ perf.eventsIngested }} (+{{ perf.eventsDeduped }} deduped)</div>
        </div>
      }

      <!-- top chrome: Kaeli plaque + status chips (left) · system cluster (right) -->
      <div class="hud top">
        <div class="hud-left">
          @if (snapshot(); as s) {
            <div class="plaque">
              <div class="plaque-head">
                <span class="kclass">{{ s.player.className }}</span>
                <button class="stance" [disabled]="!s.player.canToggleStance" (click)="toggleStance()"
                        title="Tab toggles stance">
                  <i class="el-dot" aria-hidden="true"></i>
                  {{ elementLabel(s.player.stanceElement) }}
                  @if (s.player.canToggleStance) { <small>Tab</small> }
                </button>
              </div>
              <div class="hp-row">
                <b>{{ s.player.hp }}</b>
                <span class="hp-max">/ {{ s.player.maxHp }}</span>
                <span class="lv">Lv {{ s.run.level }}</span>
              </div>
              <div class="bar hp" [class.low]="s.player.hp < s.player.maxHp * 0.35">
                <div class="fill" [style.width.%]="(100 * s.player.hp) / s.player.maxHp"></div>
              </div>
              <div class="bar xp"><div class="fill" [style.width.%]="(100 * s.run.xp) / s.run.xpNext"></div></div>
              <div class="plaque-sub">
                <span>{{ s.run.kills }} kills</span>
                <span class="sep">·</span>
                <span class="gold-amt">{{ s.run.gold }} gold</span>
                <span class="sep">·</span>
                <span>{{ s.run.tierName }}</span>
              </div>
              @if (hasEquipmentStats(s.player.equipmentStats)) {
                <div class="gear-stats">{{ equipmentStatsLabel(s.player.equipmentStats) }}</div>
              }
            </div>
            <div class="chips">
              @for (b of s.player.activeBuffs; track b) { <span class="chip">{{ buffLabel(b) }}</span> }
              @for (c of s.player.activeConditions; track c) {
                <span [class]="'chip cond cond-' + c">{{ condLabel(c) }}</span>
              }
              @if (s.player.trait; as tr) {
                <div class="passive" [class.charged]="tr.max > 0 && tr.value >= tr.max" [title]="tr.name">
                  <span class="pname">{{ tr.name }}</span>
                  @if (tr.max > 0) {
                    <div class="pbar"><div class="pfill" [style.width.%]="(100 * tr.value) / tr.max"></div></div>
                  }
                  @if (tr.text && tr.text !== '—') { <span class="ptext">{{ tr.text }}</span> }
                </div>
              }
            </div>
          }
        </div>

        <div class="sys">
          <button class="sys-pill" (click)="leave()" title="Leave the run (Esc)">Leave</button>
          <button class="sys-pill" [class.on]="showBag()" (click)="toggleBag()" title="Hunt backpack (B)">Bag</button>
          <button class="sys-pill" [class.on]="showHelper()" (click)="toggleHelper()" title="Combat helper">Auto</button>
          <button class="sys-pill" [class.off]="sound.muted()" (click)="sound.toggleMute()"
                  [title]="sound.muted() ? 'Sound off (M)' : 'Sound on (M)'">{{ sound.muted() ? 'Muted' : 'Sound' }}</button>
        </div>
      </div>

      <!-- boss cartouche: pennant hanging from the top edge -->
      @if (snapshot(); as s) {
        @if (s.run.bossHp !== null) {
          <div class="cartouche">
            <span class="c-eyebrow">Boss</span>
            <span class="c-name">{{ s.run.bossName }}</span>
            <div class="bar boss"><div class="fill" [style.width.%]="(100 * s.run.bossHp!) / s.run.bossMaxHp!"></div></div>
            @if (s.run.bossPostureMax) {
              <div class="bar posture" [class.high]="posturePct(s.run) >= 80" [class.staggered]="s.run.bossStaggered">
                <div class="fill" [style.width.%]="posturePct(s.run)"></div>
              </div>
              <div class="posture-label">
                @if (s.run.bossStaggered) {
                  <span class="broken">Echo Break — damage ×{{ activeMult(s.run.bossPostureCycle) }}</span>
                } @else {
                  <span>Stance → break ×{{ nextMult(s.run.bossPostureCycle) }}</span>
                }
              </div>
            }
          </div>
        }
      }

      <!-- helper panel (lower-left corner, minimizable) -->
      @if (snapshot(); as s) {
        @if (showHelper()) {
          <div class="helper-panel" title="Combat helper — set it and watch">
            <div class="hp-head">
              <span class="hp-title">Helper</span>
              <span class="hp-readout">{{ helperReadout(s.player.autoHelper) }}</span>
              <button class="hp-min" (click)="toggleHelper()" title="Minimize">-</button>
            </div>

            <div class="hp-group">
              <span class="hp-label">Combat</span>
              <div class="hp-pills">
                <button class="pill" [class.on]="s.player.autoHelper.targeting"
                        (click)="setAutoHelper('targeting', !s.player.autoHelper.targeting)">Target</button>
                <button class="pill" [class.on]="s.player.autoHelper.skills"
                        (click)="setAutoHelper('skills', !s.player.autoHelper.skills)">Skills</button>
                <button class="pill" [class.on]="s.player.autoHelper.ultimate"
                        (click)="setAutoHelper('ultimate', !s.player.autoHelper.ultimate)">Ultimate</button>
              </div>
              <div class="seg" [class.muted]="!s.player.autoHelper.targeting">
                <button [class.on]="s.player.autoHelper.targetPreference === 'nearest'"
                        (click)="setTargetPreference('nearest')">Nearest</button>
                <button [class.on]="s.player.autoHelper.targetPreference === 'lowestHp'"
                        (click)="setTargetPreference('lowestHp')">Lowest HP</button>
              </div>
            </div>

            <div class="hp-group">
              <span class="hp-label">Movement</span>
              <div class="seg" [class.muted]="s.player.autoHelper.navMode === 'loot'">
                <button [class.on]="s.player.autoHelper.movementMode === 'none'"
                        (click)="setAutoHelperMovement('none')">Stand</button>
                <button [class.on]="s.player.autoHelper.movementMode === 'follow'"
                        (click)="setAutoHelperMovement('follow')">Follow</button>
                <button [class.on]="s.player.autoHelper.movementMode === 'avoid'"
                        (click)="setAutoHelperMovement('avoid')">Avoid</button>
              </div>
              @if (s.player.autoHelper.navMode === 'loot') {
                <span class="hp-hint">Auto-loot is steering movement - Follow/Avoid return when it is disabled.</span>
              }
            </div>

            <div class="hp-group">
              <span class="hp-label">Autopilot</span>
              <button class="pill row-pill" [class.on]="s.player.autoHelper.autoHeal" (click)="toggleAutoHeal()">
                <span>Auto-heal</span>
                <small>potion under {{ s.player.autoHelper.autoHealPct }}% HP</small>
                <span class="dot"></span>
              </button>
              @if (s.player.autoHelper.autoHeal) {
                <label class="hp-slider">
                  <input type="range" min="10" max="90" step="5"
                         [value]="s.player.autoHelper.autoHealPct"
                         (input)="setHealPct($any($event.target).value)" />
                  <span class="hp-pct">{{ s.player.autoHelper.autoHealPct }}%</span>
                </label>
              }
              <button class="pill row-pill" [class.on]="s.player.autoHelper.autoCards" (click)="toggleAutoCards()">
                <span>Auto-pick cards</span>
                <small>takes the highest-rarity eco</small>
                <span class="dot"></span>
              </button>
              <button class="pill row-pill" [class.on]="s.player.autoHelper.navMode === 'loot'" (click)="toggleAutoLoot()">
                <span>Auto-loot</span>
                <small>explore chests &amp; altars, then exit</small>
                <span class="dot"></span>
              </button>
            </div>

            <div class="hp-actions">
              <button class="hp-save" [class.saved]="helperSaved()" (click)="saveHelperProfile()">
                {{ helperSaved() ? '✓ Saved for this Kaeli' : 'Save as default' }}
              </button>
              <button class="hp-reset" (click)="resetHelper()" title="Back to defaults">Reset</button>
            </div>
          </div>
        }
      }

      <!-- minimap -->
      <canvas #mini class="minimap" width="160" height="160"></canvas>

      <!-- training sandbox controls -->
      @if (isTraining() && snapshot(); as s) {
        <button class="train-toggle" [class.on]="s.player.trainingFreeCast"
                (click)="toggleFreeCast()"
                title="Skills and the ultimate ignore cooldown & gauge — spam anything to test it">
          <span class="dot"></span>
          <span>Free cast</span>
          <small>no cooldown / energy</small>
        </button>
      }

      <!-- skill bar: cathedral windows — four arches, the ultimate rose window, two votive coins -->
      @if (snapshot(); as s) {
        <div class="hud skills">
          @for (sk of s.player.skills; track sk.id; let i = $index) {
            @if (i < 4) {
              <button class="arch" [class.ready]="sk.ready"
                      [style.--sk-el]="elVar(sk.element)" [style.--cd]="cdFrac(sk)"
                      [title]="sk.name + ' — ' + sk.description" (click)="cast(i)">
                <span class="a-name">{{ sk.name }}</span>
                @if (!sk.ready) { <span class="a-cd" aria-hidden="true"></span> }
                <span class="a-key">{{ i + 1 }}</span>
              </button>
            } @else {
              <button class="rosette" [class.ready]="sk.ready" [style.--gauge]="s.player.gauge / 100"
                      [title]="sk.name + ' — ' + sk.description" (click)="cast(4)">
                <span class="r-core" aria-hidden="true"></span>
                <span class="r-ring" aria-hidden="true"></span>
                <span class="r-key">R</span>
              </button>
            }
          }
          <button class="arch coin potion"
                  [class.ready]="s.player.potionCharges > 0 && s.player.potionCooldownRemainingMs === 0"
                  [disabled]="s.player.potionCharges === 0"
                  [style.--cd]="potionCdFrac(s.player)"
                  [title]="potionTitle(s.player.potionHealPct)"
                  (click)="usePotion()">
            <app-item-icon [itemId]="s.player.potionItemId" [size]="26" />
            <span class="charges">{{ s.player.potionCharges }}/{{ s.player.potionMaxCharges }}</span>
            @if (s.player.potionCooldownRemainingMs > 0) { <span class="a-cd" aria-hidden="true"></span> }
            <span class="a-key">T</span>
          </button>
          <button class="arch coin dash" [class.ready]="s.player.dashReady" (click)="dash()"
                  [style.--cd]="dashCdFrac(s.player)"
                  title="Dash / Dodge (Space) — leaps 3 tiles in your movement direction, with i-frames">
            <span class="dashglyph" aria-hidden="true">&raquo;</span>
            <span class="charges">Dash</span>
            @if (s.player.dashCooldownRemainingMs > 0) { <span class="a-cd" aria-hidden="true"></span> }
            <span class="a-key">Spc</span>
          </button>
        </div>

        @if (showBag()) {
          <div class="bagpanel">
            <div class="baghead"><b>Hunt backpack</b><span>{{ s.run.gold }} gold</span></div>
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
              <p class="bagempty">Nothing collected yet - go hunt!</p>
            }
          </div>
        }
      }

      <!-- card offer -->
      @if (snapshot()?.run; as run) {
        @if (run.offer; as offer) {
          <div class="overlay cards">
            <span class="ov-eyebrow">The dungeon offers</span>
            <h2 class="ov-title">Choose an Echo</h2>
            <div class="offer-actions">
              @if (run.cardRerollsRemaining > 0) {
                <button class="offer-action" (click)="rerollCards()">
                  Reroll <b>{{ run.cardRerollsRemaining }}</b>
                </button>
              } @else {
                <!-- G-09: free rerolls depleted -> paid reroll (run altar shop) -->
                <button class="offer-action" [disabled]="run.gold < run.cardRerollGoldCost" (click)="rerollCards()">
                  Reroll <b>{{ run.cardRerollGoldCost }} gold</b>
                </button>
              }
              <span>Banned {{ run.bannedCardsCount }}</span>
            </div>
            <div class="choices">
              @for (c of offer; track c.id; let i = $index) {
                <button class="choice" [attr.data-rarity]="c.rarity" (click)="chooseCard(c.id)">
                  <span class="rarity">{{ rarityLabel(c.rarity) }}</span>
                  <b>{{ c.name }}</b>
                  <p>{{ c.description }}</p>
                  @if (c.tags.length) {
                    <div class="tags">
                      @for (t of c.tags; track t) {
                        <span class="tag">{{ t }}</span>
                      }
                    </div>
                  }
                  <span class="stacks">{{ c.currentStacks }}/{{ c.maxStacks }}</span>
                  <small class="card-key">[{{ i + 1 }}]</small>
                </button>
              }
            </div>
            <div class="ban-actions">
              @for (c of offer; track c.id; let i = $index) {
                <button (click)="banCard(c.id)">Ban {{ i + 1 }}</button>
              }
            </div>
          </div>
        }
      }

      <!-- run end -->
      @if (snapshot()?.run?.ended; as end) {
        <div class="overlay end">
          <span class="ov-eyebrow">{{ end.victory ? 'The arena falls silent' : 'The echo fades' }}</span>
          <h1 class="verdict" [class.victory]="end.victory">{{ end.victory ? 'Victory' : 'Defeat' }}</h1>
          <p class="reason">{{ end.reason }}</p>
          <div class="stats">
            <div class="stat"><b>{{ end.kills }}</b><span>Kills</span></div>
            <div class="stat"><b>{{ end.runLevel }}</b><span>Level</span></div>
            <div class="stat gold"><b>{{ end.goldEarned }}</b><span>Gold</span></div>
            <div class="stat gold"><b>{{ end.kaerosEarned }}</b><span>✦ Kaeros</span></div>
            <div class="stat"><b>{{ end.accountXpEarned }}</b><span>Account XP</span></div>
            <div class="stat"><b>{{ formatTime(end.durationMs) }}</b><span>Duration</span></div>
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
          @for (note of end.dailyProgressNotes; track note) { <p class="note">{{ note }}</p> }
          @if (autoRunsRemaining() > 0) {
            <p class="note farm-note">Batch {{ farmProgressLabel() }}: next run in {{ autoRepeatCountdown() }}s</p>
          }
          <div class="actions">
            <button class="btn" (click)="again()">Play again</button>
            <button class="btn secondary" (click)="leave()">Back to Hunt</button>
          </div>
        </div>
      }

      @if (!snapshot()) {
        <div class="overlay loading">
          <span class="spin-rosette" aria-hidden="true"></span>
          <h2 class="ov-title">Shaping the dungeon…</h2>
        </div>
      }
    </div>
  `,
  styles: [`
    /* =====================================================================
       Gameplay chrome — "Reliquary Combat"
       The HUD borrows the reliquary language: glass tablets, cathedral-arch
       skill windows, a rose-window ultimate, and an element-tinted gloom
       framing the arena. Tokens come from styles.css (Cathedral Ink + Aurum);
       --accent-el is bound to the active stance element at runtime.
       Reference: docs/design/gameplay_style_guide.md
       ===================================================================== */
    .game-root {
      position: fixed; inset: 0; background: var(--bg-0); outline: none; overflow: hidden;
      --accent-el: var(--accent);
      --el-bright: color-mix(in srgb, var(--accent-el) 64%, white);
      --el-glow: color-mix(in srgb, var(--accent-el) 40%, transparent);
      --el-haze: color-mix(in srgb, var(--accent-el) 16%, transparent);
      font-family: var(--font-ui);
    }
    .game-canvas { position: absolute; inset: 0; image-rendering: pixelated; }

    /* Cathedral gloom: vignette seats the HUD, element haze glows under the altar (skill bar). */
    .arena-veil {
      position: absolute; inset: 0; z-index: 5; pointer-events: none;
      background:
        radial-gradient(120% 95% at 50% 42%, transparent 58%, rgba(7, 7, 13, 0.62) 100%),
        linear-gradient(180deg, rgba(7, 7, 13, 0.5), transparent 130px),
        linear-gradient(0deg, rgba(7, 7, 13, 0.58), transparent 150px),
        radial-gradient(52% 24% at 50% 105%, var(--el-haze), transparent 72%);
    }

    .resume-toast {
      position: absolute; top: 18px; left: 50%; z-index: 30; transform: translateX(-50%);
      padding: 9px 18px; border: 1px solid color-mix(in srgb, var(--success) 55%, transparent);
      border-radius: var(--r-full); background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      box-shadow: var(--glass-edge), var(--sh-2);
      color: var(--success); font-size: 12px; font-weight: 700; letter-spacing: 0.04em;
    }
    .perf-overlay {
      position: absolute; top: 8px; right: 8px; z-index: 40;
      font: 11px/1.5 monospace; color: #9fe8a0;
      background: rgba(0, 0, 0, 0.55); padding: 6px 9px;
      border-radius: 6px; pointer-events: none;
    }

    /* ---- top chrome ---------------------------------------------------- */
    .hud.top {
      position: absolute; top: 14px; left: 16px; right: 16px; z-index: 10;
      display: flex; gap: 16px; align-items: flex-start; pointer-events: none;
    }
    .hud-left { display: flex; flex-direction: column; gap: 8px; min-width: 0; }

    .plaque {
      width: 300px; padding: 10px 14px 11px; border-radius: var(--r-md); pointer-events: auto;
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(1.2); backdrop-filter: blur(var(--glass-blur)) saturate(1.2);
      border: 1px solid var(--line); border-left: 2px solid color-mix(in srgb, var(--accent-el) 55%, transparent);
      box-shadow: var(--glass-edge), var(--sh-2);
    }
    .plaque-head { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
    .kclass {
      font-family: var(--font-display); font-style: italic; font-weight: 600; font-size: 15px;
      color: var(--el-bright); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }
    .stance {
      display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px; flex: 0 0 auto;
      border-radius: var(--r-full); cursor: pointer;
      border: 1px solid color-mix(in srgb, var(--accent-el) 40%, transparent);
      background: color-mix(in srgb, var(--accent-el) 12%, transparent);
      color: var(--el-bright); font-family: var(--font-ui); font-size: 10px; font-weight: 700;
      text-transform: uppercase; letter-spacing: 0.08em;
      transition: border-color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
    }
    .stance:hover:not(:disabled) { border-color: var(--accent-el); background: color-mix(in srgb, var(--accent-el) 20%, transparent); }
    .stance:disabled { cursor: default; }
    .stance small { color: var(--text-mute); font-weight: 600; letter-spacing: 0; text-transform: none; }
    .el-dot { width: 7px; height: 7px; border-radius: 50%; background: var(--accent-el); box-shadow: 0 0 8px var(--el-glow); }

    .hp-row { display: flex; align-items: baseline; gap: 5px; margin-top: 7px; }
    .hp-row b { font-family: var(--font-display); font-size: 22px; font-weight: 600; line-height: 1; color: var(--text); }
    .hp-max { color: var(--text-mute); font-size: 12px; }
    .lv { margin-left: auto; font-size: 9.5px; font-weight: 700; letter-spacing: 0.14em; text-transform: uppercase; color: var(--text-dim); }

    .bar { position: relative; height: 8px; border-radius: var(--r-full); background: rgba(255, 255, 255, 0.07); overflow: hidden; margin-top: 6px; }
    .bar .fill { height: 100%; border-radius: inherit; transition: width 160ms var(--ease-out); }
    /* HP is ivory light, not Tibia green; it turns to blood only when the run is in danger. */
    .bar.hp .fill { background: linear-gradient(90deg, #f4eedb, #cbbd97); box-shadow: 0 0 10px rgba(244, 238, 219, 0.3); }
    .bar.hp.low .fill { background: linear-gradient(90deg, #ff8a9d, var(--danger)); animation: lowPulse 0.9s ease-in-out infinite alternate; }
    @keyframes lowPulse { from { filter: brightness(0.85); } to { filter: brightness(1.3); } }
    .bar.xp { height: 3px; margin-top: 5px; background: rgba(255, 255, 255, 0.05); }
    .bar.xp .fill { background: var(--accent); box-shadow: none; }

    .plaque-sub { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 7px; font-size: 11px; color: var(--text-dim); }
    .sep { color: var(--text-faint); }
    .gold-amt { color: var(--gold-bright); font-weight: 600; }
    .gear-stats { margin-top: 3px; font-size: 10px; color: var(--text-mute); }

    /* ---- status chips + signature passive ------------------------------ */
    .chips { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; pointer-events: none; }
    .chip {
      padding: 3px 9px; border-radius: var(--r-full); font-size: 9.5px; font-weight: 700;
      letter-spacing: 0.07em; text-transform: uppercase;
      background: color-mix(in srgb, var(--accent) 14%, transparent);
      border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent);
      color: var(--accent-bright);
      -webkit-backdrop-filter: blur(8px); backdrop-filter: blur(8px);
    }
    .chip.cond { --cond: var(--danger); background: color-mix(in srgb, var(--cond) 14%, transparent);
      border-color: color-mix(in srgb, var(--cond) 45%, transparent); color: color-mix(in srgb, var(--cond) 65%, white); }
    .chip.cond-poison { --cond: var(--el-earth); }
    .chip.cond-fire { --cond: var(--el-fire); }
    .chip.cond-energy { --cond: var(--el-energy); }
    .chip.cond-slow { --cond: var(--el-ice); }
    .chip.cond-freeze { --cond: var(--el-ice); }
    .chip.cond-curse { --cond: var(--el-death); }

    /* K-04: signature passive chip — name + live state bar/text */
    .passive {
      display: inline-flex; align-items: center; gap: 7px; padding: 3px 10px; border-radius: var(--r-full);
      background: var(--glass-bg); border: 1px solid color-mix(in srgb, var(--accent-el) 35%, transparent);
      -webkit-backdrop-filter: blur(8px); backdrop-filter: blur(8px);
    }
    .passive .pname { font-size: 9.5px; font-weight: 700; letter-spacing: 0.07em; text-transform: uppercase; color: var(--el-bright); }
    .passive .ptext { font-size: 10.5px; font-weight: 700; color: var(--text); }
    .passive .pbar { width: 60px; height: 4px; border-radius: var(--r-full); background: rgba(255, 255, 255, 0.08); overflow: hidden; }
    .passive .pfill { height: 100%; background: linear-gradient(90deg, var(--accent-el), var(--el-bright)); transition: width 0.12s linear; }
    .passive.charged { border-color: color-mix(in srgb, var(--gold) 60%, transparent); box-shadow: 0 0 12px var(--gold-glow); }
    .passive.charged .pfill { background: linear-gradient(90deg, var(--gold), var(--gold-bright)); }
    .passive.charged .pname, .passive.charged .ptext { color: var(--gold-bright); }

    /* ---- system cluster ------------------------------------------------- */
    .sys { margin-left: auto; display: flex; gap: 7px; pointer-events: auto; }
    .sys-pill {
      height: 30px; padding: 0 14px; border-radius: var(--r-full); cursor: pointer;
      border: 1px solid var(--line-strong); background: var(--glass-bg);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      box-shadow: var(--glass-edge); color: var(--text-dim);
      font-size: 10px; font-weight: 700; letter-spacing: 0.1em; text-transform: uppercase;
      transition: color var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
    }
    .sys-pill:hover { color: var(--text); border-color: var(--accent); }
    .sys-pill.on { color: var(--accent-bright); border-color: var(--accent); background: color-mix(in srgb, var(--accent) 14%, transparent); }
    .sys-pill.off { opacity: 0.55; }
    .sys-pill:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 2px; }

    /* ---- boss cartouche: pennant hanging from the top edge -------------- */
    .cartouche {
      position: absolute; top: 0; left: 50%; transform: translateX(-50%); z-index: 9;
      width: min(440px, 42vw); padding: 9px 20px 11px; text-align: center; pointer-events: none;
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      border: 1px solid var(--line); border-top: 0; border-radius: 0 0 var(--r-lg) var(--r-lg);
      box-shadow: var(--sh-2);
    }
    .c-eyebrow { display: block; font-size: 8.5px; font-weight: 700; letter-spacing: 0.24em; text-transform: uppercase; color: var(--danger); }
    .c-name { display: block; font-family: var(--font-display); font-style: italic; font-weight: 600; font-size: 17px; color: var(--text); margin-top: 1px; }
    .bar.boss { height: 8px; margin-top: 7px; }
    .bar.boss .fill { background: linear-gradient(90deg, #ff8a9d, var(--danger)); box-shadow: 0 0 12px color-mix(in srgb, var(--danger) 45%, transparent); }
    .bar.posture { height: 3px; margin-top: 4px; background: color-mix(in srgb, var(--gold) 14%, transparent); }
    .bar.posture .fill { background: linear-gradient(90deg, var(--gold), var(--gold-bright)); transition: width 0.12s linear; }
    .bar.posture.high .fill { animation: posturePulse 0.5s infinite alternate; }
    .bar.posture.staggered { box-shadow: 0 0 10px var(--gold-glow); }
    .bar.posture.staggered .fill { background: linear-gradient(90deg, #fff, var(--gold-bright)); }
    .posture-label { font-size: 9px; font-weight: 700; letter-spacing: 0.1em; text-transform: uppercase; color: var(--gold); margin-top: 4px; }
    .posture-label .broken { color: var(--gold-bright); animation: posturePulse 0.4s infinite alternate; }
    @keyframes posturePulse { from { opacity: 0.45; } to { opacity: 1; } }

    /* G-10: combat helper — autoplay control panel. Iris = on, aurum = saved. */
    .helper-panel {
      position: absolute; left: 16px; bottom: 18px; z-index: 16;
      pointer-events: auto; width: 270px; max-height: 70vh; overflow-y: auto; border-radius: var(--r-md);
      border: 1px solid var(--line);
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      box-shadow: var(--glass-edge), var(--sh-2);
      color: var(--text); padding: 11px 12px 12px; display: flex; flex-direction: column; gap: 11px;
    }
    .hp-head { display: grid; grid-template-columns: 1fr auto; align-items: start; gap: 0 8px; }
    .hp-title { grid-column: 1; font-size: 10px; font-weight: 700; letter-spacing: 0.22em; text-transform: uppercase; color: var(--accent-bright); }
    .hp-readout { grid-column: 1; font-size: 10.5px; line-height: 1.3; color: var(--text-mute); min-height: 14px; }
    .hp-min {
      grid-column: 2; grid-row: 1 / span 2; align-self: center; width: 22px; height: 22px; border-radius: 7px;
      border: 1px solid var(--line); background: rgba(255, 255, 255, 0.03); color: var(--text-mute);
      font-size: 15px; font-weight: 700; line-height: 1; cursor: pointer; transition: border-color 110ms, color 110ms;
    }
    .hp-min:hover { border-color: var(--accent); color: var(--accent-bright); }
    .hp-group { display: flex; flex-direction: column; gap: 6px; }
    .hp-label { font-size: 8.5px; font-weight: 700; letter-spacing: 0.16em; text-transform: uppercase; color: var(--text-mute); }
    .hp-pills { display: grid; grid-template-columns: repeat(3, 1fr); gap: 5px; }
    .helper-panel .pill {
      height: 27px; border-radius: var(--r-sm); border: 1px solid var(--line); background: rgba(255, 255, 255, 0.03);
      color: var(--text-mute); font-size: 10.5px; font-weight: 700; letter-spacing: 0.02em; cursor: pointer;
      transition: border-color 110ms, background 110ms, color 110ms;
    }
    .helper-panel .pill:hover { border-color: color-mix(in srgb, var(--accent) 45%, transparent); color: var(--text-dim); }
    .helper-panel .pill.on { border-color: color-mix(in srgb, var(--accent) 70%, transparent); background: color-mix(in srgb, var(--accent) 16%, transparent); color: var(--accent-bright); }
    .helper-panel .row-pill {
      width: 100%; display: flex; align-items: center; gap: 9px; height: 34px; padding: 0 11px; text-align: left;
    }
    .helper-panel .row-pill small { color: var(--text-mute); font-size: 9.5px; font-weight: 600; letter-spacing: 0; }
    .helper-panel .row-pill .dot {
      margin-left: auto; width: 9px; height: 9px; border-radius: 50%; background: var(--bg-4); flex: 0 0 auto;
      transition: background 110ms, box-shadow 110ms;
    }
    .helper-panel .row-pill.on small { color: color-mix(in srgb, var(--accent-bright) 70%, var(--text-mute)); }
    .helper-panel .row-pill.on .dot { background: var(--accent-bright); box-shadow: 0 0 8px var(--accent-glow); }
    .seg {
      display: grid; grid-auto-flow: column; grid-auto-columns: 1fr; gap: 2px; padding: 2px;
      border-radius: 9px; background: rgba(0, 0, 0, 0.3);
    }
    .seg button {
      height: 24px; border: 0; border-radius: 7px; background: transparent; color: var(--text-mute);
      font-size: 10px; font-weight: 700; cursor: pointer; transition: background 110ms, color 110ms;
    }
    .seg button:hover { color: var(--text-dim); }
    .seg button.on { background: color-mix(in srgb, var(--accent) 18%, transparent); color: var(--accent-bright); box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--accent) 32%, transparent); }
    .seg.muted { opacity: 0.4; }
    .hp-hint { font-size: 9.5px; color: var(--text-mute); line-height: 1.25; }
    .hp-slider { display: flex; align-items: center; gap: 9px; padding: 0 2px; }
    .hp-slider input[type=range] {
      flex: 1; height: 4px; -webkit-appearance: none; appearance: none; border-radius: 3px; cursor: pointer;
      background: linear-gradient(90deg, var(--accent), var(--accent-glow)); outline: none;
    }
    .hp-slider input[type=range]::-webkit-slider-thumb {
      -webkit-appearance: none; appearance: none; width: 13px; height: 13px; border-radius: 50%;
      background: var(--accent-bright); border: 2px solid var(--bg-1); box-shadow: 0 0 6px var(--accent-glow); cursor: pointer;
    }
    .hp-slider input[type=range]::-moz-range-thumb {
      width: 13px; height: 13px; border-radius: 50%; background: var(--accent-bright); border: 2px solid var(--bg-1); cursor: pointer;
    }
    .hp-slider .hp-pct { font-size: 10px; font-weight: 700; color: var(--accent-bright); width: 30px; text-align: right; }
    .hp-actions { display: flex; gap: 6px; padding-top: 10px; border-top: 1px solid var(--line); }
    .hp-save {
      flex: 1; height: 28px; border-radius: var(--r-sm); border: 1px solid var(--line-strong); background: rgba(255, 255, 255, 0.03);
      color: var(--text); font-size: 10px; font-weight: 700; letter-spacing: 0.02em; cursor: pointer; transition: border-color 120ms, background 120ms, color 120ms;
    }
    .hp-save:hover { border-color: color-mix(in srgb, var(--gold) 55%, transparent); color: var(--gold-bright); }
    .hp-save.saved { border-color: color-mix(in srgb, var(--gold) 70%, transparent); background: color-mix(in srgb, var(--gold) 16%, transparent); color: var(--gold-bright); }
    .hp-reset {
      flex: 0 0 auto; height: 28px; padding: 0 13px; border-radius: var(--r-sm); border: 1px solid var(--line);
      background: transparent; color: var(--text-mute); font-size: 10px; font-weight: 700; cursor: pointer;
    }
    .hp-reset:hover { color: var(--text-dim); border-color: var(--line-strong); }
    /* ---- minimap: obsidian mirror --------------------------------------- */
    .minimap {
      position: absolute; right: 16px; top: 58px; z-index: 10;
      width: 152px; height: 152px; border-radius: var(--r-lg);
      border: 1px solid var(--line-strong); background: var(--bg-0);
      box-shadow: 0 0 0 4px rgba(7, 7, 13, 0.35), var(--sh-2); opacity: 0.92;
    }

    /* ---- training sandbox ------------------------------------------------ */
    .train-toggle {
      position: absolute; bottom: 118px; left: 50%; transform: translateX(-50%); z-index: 16;
      display: flex; align-items: center; gap: 8px; padding: 7px 15px; border-radius: var(--r-full); cursor: pointer;
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      border: 1px solid var(--line-strong); box-shadow: var(--glass-edge);
      color: var(--text-dim); font-size: 11px; font-weight: 700; letter-spacing: 0.04em;
    }
    .train-toggle small { color: var(--text-mute); font-weight: 600; font-size: 10px; }
    .train-toggle .dot { width: 9px; height: 9px; border-radius: 50%; background: var(--bg-4); }
    .train-toggle.on { border-color: color-mix(in srgb, var(--gold) 60%, transparent); color: var(--gold-bright); box-shadow: var(--glass-edge), 0 0 12px var(--gold-glow); }
    .train-toggle.on .dot { background: var(--gold); box-shadow: 0 0 8px var(--gold-glow); }
    .train-toggle.on small { color: color-mix(in srgb, var(--gold-bright) 70%, var(--text-mute)); }

    /* ---- skill bar: cathedral windows ----------------------------------- */
    .hud.skills { position: absolute; bottom: 18px; left: 50%; transform: translateX(-50%); z-index: 10;
      display: flex; align-items: flex-end; gap: 9px; }
    .arch {
      position: relative; width: 92px; height: 80px; padding: 6px 6px 16px;
      border-radius: 46px 46px 12px 12px / 60px 60px 12px 12px;
      border: 1px solid var(--line); overflow: hidden; cursor: pointer;
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      box-shadow: var(--glass-edge), var(--sh-1);
      color: var(--text-mute); display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 3px;
      --sk-el: var(--accent-el);
      transition: border-color var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out),
                  color var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out);
    }
    .arch.ready {
      color: var(--text); border-color: color-mix(in srgb, var(--sk-el) 55%, transparent);
      box-shadow: var(--glass-edge), inset 0 -14px 26px -18px var(--sk-el),
                  0 0 18px color-mix(in srgb, var(--sk-el) 22%, transparent), var(--sh-1);
    }
    .arch:hover:not(:disabled) { transform: translateY(-2px); }
    .arch:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 2px; }
    .arch:disabled { opacity: 0.5; cursor: default; }
    .a-name { position: relative; z-index: 1; font-size: 10px; font-weight: 600; text-align: center; line-height: 1.15; }
    .a-key {
      position: absolute; bottom: 3px; left: 50%; transform: translateX(-50%); z-index: 1;
      font-family: var(--font-display); font-size: 11.5px; font-weight: 600; color: var(--text-faint);
    }
    .arch.ready .a-key { color: color-mix(in srgb, var(--sk-el) 65%, white); }
    /* cooldown: a dark sweep that recedes clockwise as the skill returns (text stays above it) */
    .a-cd {
      position: absolute; inset: 0; pointer-events: none;
      background: conic-gradient(rgba(7, 7, 13, 0.72) calc(var(--cd, 0) * 1turn), transparent 0);
    }
    .charges { position: relative; z-index: 1; }
    .arch.coin { width: 62px; }
    .charges { font-size: 9.5px; font-weight: 700; color: var(--text-dim); }
    .arch.potion { --sk-el: var(--danger); }
    .arch.dash { --sk-el: var(--el-ice); }
    .dashglyph { font-size: 24px; font-weight: 700; line-height: 0.9; color: inherit; }
    .arch.dash.ready .dashglyph { color: color-mix(in srgb, var(--el-ice) 70%, white); }

    /* the rose window: ultimate. Gold ring fills with the gauge; blooms when ready. */
    .rosette {
      position: relative; width: 88px; height: 88px; border-radius: 50%; margin: 0 5px; flex: 0 0 auto;
      border: 1px solid var(--line); cursor: pointer;
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      box-shadow: var(--glass-edge), var(--sh-2);
      display: grid; place-items: center;
      transition: border-color var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .r-ring {
      position: absolute; inset: 3px; border-radius: 50%;
      background: conic-gradient(var(--gold) calc(var(--gauge, 0) * 1turn), rgba(255, 255, 255, 0.13) 0);
      -webkit-mask: radial-gradient(closest-side, transparent calc(100% - 6px), #000 calc(100% - 5px));
      mask: radial-gradient(closest-side, transparent calc(100% - 6px), #000 calc(100% - 5px));
    }
    .r-core {
      position: absolute; inset: 12px; border-radius: 50%; border: 1px solid var(--line);
      background:
        repeating-conic-gradient(color-mix(in srgb, var(--accent-el) 28%, transparent) 0deg 2deg, transparent 2deg 30deg),
        radial-gradient(circle at 50% 38%, color-mix(in srgb, var(--accent-el) 24%, transparent), transparent 72%);
    }
    .r-key {
      position: relative; font-family: var(--font-display); font-size: 25px; font-weight: 600;
      color: var(--text-dim); text-shadow: 0 2px 10px rgba(0, 0, 0, 0.6);
    }
    .rosette:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; }
    .rosette.ready { border-color: color-mix(in srgb, var(--gold) 60%, transparent); animation: ultBloom 1.6s var(--ease-in-out) infinite alternate; }
    .rosette.ready .r-key { color: var(--gold-bright); }
    @keyframes ultBloom {
      from { box-shadow: var(--glass-edge), 0 0 14px var(--gold-glow), var(--sh-2); }
      to { box-shadow: var(--glass-edge), 0 0 32px var(--gold-glow), var(--sh-2); }
    }

    /* ---- hunt backpack --------------------------------------------------- */
    .bagpanel {
      position: absolute; right: 16px; bottom: 110px; width: 252px; max-height: 46vh; overflow-y: auto;
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur));
      border: 1px solid var(--line); border-radius: var(--r-md); padding: 10px 12px; z-index: 15;
      box-shadow: var(--glass-edge), var(--sh-2);
    }
    .baghead { display: flex; justify-content: space-between; align-items: center; font-size: 13px; color: var(--text); margin-bottom: 8px; }
    .baghead b { font-family: var(--font-display); font-weight: 600; }
    .baghead span { color: var(--gold-bright); font-weight: 600; font-size: 12px; }
    .baggrid { display: grid; grid-template-columns: repeat(auto-fill, minmax(56px, 1fr)); gap: 8px; }
    .bagitem { background: rgba(255, 255, 255, 0.03); border: 1px solid var(--line); border-radius: var(--r-sm); padding: 5px; display: flex; flex-direction: column; align-items: center; font-size: 11px; color: var(--text-dim); }
    .bagempty { color: var(--text-mute); font-size: 12px; margin: 4px 0; }

    /* ---- overlays --------------------------------------------------------- */
    .overlay {
      position: absolute; inset: 0; z-index: 20;
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 14px;
      background: rgba(7, 7, 13, 0.74);
      -webkit-backdrop-filter: blur(10px); backdrop-filter: blur(10px);
    }
    .ov-eyebrow { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: var(--tracking-eyebrow); color: var(--text-mute); }
    .ov-title { font-family: var(--font-display); font-weight: 600; font-size: 30px; margin: 0; color: var(--text); }

    .overlay.cards .choices { display: flex; gap: 16px; flex-wrap: wrap; justify-content: center; max-width: 780px; }
    .offer-actions, .ban-actions { display: flex; align-items: center; gap: 10px; color: var(--text-mute); font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; }
    .offer-actions button, .ban-actions button {
      border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent); border-radius: var(--r-full);
      background: color-mix(in srgb, var(--accent) 12%, transparent); color: var(--accent-bright);
      font-weight: 700; font-size: 11px; letter-spacing: 0.05em; padding: 8px 14px; cursor: pointer;
      transition: border-color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
    }
    .offer-actions button:hover:not(:disabled) { border-color: var(--accent); background: color-mix(in srgb, var(--accent) 20%, transparent); }
    .offer-action:disabled { opacity: .45; cursor: not-allowed; }
    /* Echo cards are small cathedral windows too; the arch top echoes the skill bar. */
    .choice {
      width: 218px; min-height: 150px; padding: 20px 15px 12px; text-align: left; color: inherit;
      border-radius: 60px 60px 14px 14px / 74px 74px 14px 14px; --rar: var(--rarity-3);
      border: 1px solid color-mix(in srgb, var(--rar) 45%, transparent);
      background: linear-gradient(180deg, color-mix(in srgb, var(--rar) 10%, var(--bg-2)), var(--bg-2) 62%);
      display: flex; flex-direction: column; gap: 6px;
      transition: transform var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    /* G-04: rarity drives the tint (common blue · rare violet · echo aurum). */
    .choice[data-rarity="common"] { --rar: var(--rarity-3); }
    .choice[data-rarity="rare"] { --rar: var(--rarity-4); box-shadow: 0 0 14px color-mix(in srgb, var(--rar) 20%, transparent); }
    .choice[data-rarity="echo"] { --rar: var(--gold); box-shadow: 0 0 22px var(--gold-glow); }
    .choice:hover { transform: translateY(-4px); box-shadow: 0 14px 34px color-mix(in srgb, var(--rar) 25%, transparent); }
    .choice:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; }
    .choice b { font-family: var(--font-display); font-weight: 600; font-size: 16px; color: var(--text); }
    .choice p { margin: 0; color: var(--text-dim); font-size: 12.5px; line-height: 1.4; }
    .choice .rarity { font-size: 9px; font-weight: 700; letter-spacing: 0.2em; text-transform: uppercase; text-align: center; color: color-mix(in srgb, var(--rar) 70%, white); }
    .choice .tags { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 2px; }
    .choice .tag {
      font-size: 10px; font-weight: 600; padding: 1px 8px; border-radius: var(--r-full);
      background: rgba(255, 255, 255, 0.04); border: 1px solid var(--line); color: var(--text-dim);
    }
    .choice .stacks { color: var(--text-mute); font-size: 11px; }
    .choice .card-key { color: var(--text-faint); font-size: 11px; font-weight: 600; align-self: flex-end; }
    .ban-actions button { color: color-mix(in srgb, var(--danger) 65%, white); border-color: color-mix(in srgb, var(--danger) 40%, transparent); background: color-mix(in srgb, var(--danger) 10%, transparent); }
    .ban-actions button:hover { border-color: var(--danger); background: color-mix(in srgb, var(--danger) 18%, transparent); }

    /* ---- run end ---------------------------------------------------------- */
    .verdict {
      font-family: var(--font-display); font-weight: 900; font-size: clamp(48px, 7vw, 74px); line-height: 1;
      margin: 0; text-transform: uppercase; letter-spacing: 0.05em; color: var(--danger);
    }
    .verdict.victory { color: var(--gold-bright); text-shadow: 0 0 44px var(--gold-glow); }
    .reason { color: var(--text-dim); margin: 0; }
    .stats { display: flex; gap: 10px; flex-wrap: wrap; justify-content: center; }
    .stat {
      min-width: 96px; padding: 12px 18px; text-align: center; border-radius: var(--r-md);
      background: var(--glass-bg); border: 1px solid var(--line); box-shadow: var(--glass-edge);
    }
    .stat b { display: block; font-family: var(--font-display); font-weight: 600; font-size: 24px; color: var(--text); }
    .stat span { display: block; margin-top: 2px; color: var(--text-mute); font-size: 9px; font-weight: 700; letter-spacing: 0.14em; text-transform: uppercase; }
    .stat.gold b { color: var(--gold-bright); }
    .loot { display: flex; gap: 10px; flex-wrap: wrap; justify-content: center; max-width: 600px; }
    .lootitem { background: var(--glass-bg); border: 1px solid var(--line); border-radius: var(--r-sm); padding: 6px; display: flex; flex-direction: column; align-items: center; font-size: 11px; color: var(--text-dim); }
    .note { color: var(--text-dim); font-size: 13px; margin: 0; }
    .farm-note { color: var(--accent-bright); font-weight: 600; }
    .actions { display: flex; gap: 14px; margin-top: 8px; }

    /* ---- loading ---------------------------------------------------------- */
    .spin-rosette {
      width: 42px; height: 42px; border-radius: 50%;
      background: conic-gradient(var(--accent) 100deg, rgba(255, 255, 255, 0.08) 0);
      -webkit-mask: radial-gradient(closest-side, transparent calc(100% - 5px), #000 calc(100% - 4px));
      mask: radial-gradient(closest-side, transparent calc(100% - 5px), #000 calc(100% - 4px));
      animation: spin 1.1s linear infinite;
    }
    @keyframes spin { to { transform: rotate(1turn); } }

    /* ---- responsive -------------------------------------------------------- */
    @media (max-width: 1100px) {
      .plaque { width: 258px; }
      .cartouche { width: min(340px, 34vw); }
    }
    @media (max-width: 820px) {
      .plaque { width: 232px; }
      .arch { width: 76px; height: 70px; }
      .a-name { font-size: 9px; }
      .arch.coin { width: 54px; }
      .rosette { width: 74px; height: 74px; }
      .r-key { font-size: 21px; }
      .minimap { width: 118px; height: 118px; }
      .sys-pill { padding: 0 10px; }
      .cartouche { width: 40vw; }
      .helper-panel { width: 240px; }
    }
    @media (max-width: 560px) {
      .hud.top { flex-wrap: wrap; }
      .plaque { width: 200px; padding: 8px 11px 9px; }
      .hp-row b { font-size: 18px; }
      .sys { gap: 5px; }
      .sys-pill { height: 26px; padding: 0 8px; font-size: 9px; }
      .minimap { width: 88px; height: 88px; top: 90px; }
      .cartouche { width: 62vw; }
      .hud.skills { gap: 5px; transform: translateX(-50%) scale(0.84); transform-origin: bottom center; }
      .arch { width: 60px; height: 60px; padding-bottom: 12px; }
      .a-name { font-size: 8px; }
      .arch.coin { width: 46px; }
      .rosette { width: 60px; height: 60px; margin: 0 2px; }
      .r-key { font-size: 17px; }
    }
  `],
})
export class GamePage implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('cv') cv!: ElementRef<HTMLCanvasElement>;
  @ViewChild('mini') mini!: ElementRef<HTMLCanvasElement>;
  @ViewChild('root') root!: ElementRef<HTMLDivElement>;

  readonly snapshot = computed(() => this.client.snapshot());
  /** Elements with a --el-* token in styles.css; anything else falls back to the iris accent. */
  private static readonly THEMED_ELEMENTS = new Set(['physical', 'fire', 'ice', 'energy', 'earth', 'death', 'holy']);
  /** Element tint driving the HUD chrome (--accent-el); follows the active stance. */
  readonly accentEl = computed(() => this.elVar(this.snapshot()?.player.stanceElement ?? ''));
  readonly busyChoosing = signal(false);
  readonly resumeToast = signal(false);
  readonly showBag = signal(false);
  readonly showHelper = signal(false);
  /** Training Room only: reveals the free-cast switch (skills/ult ignore cooldown & gauge). */
  readonly isTraining = signal(false);

  // G-10: feedback for the HELPER panel "Save as default" button.
  readonly helperSaved = signal(false);
  readonly plannedRuns = signal(1);
  readonly autoRunsRemaining = signal(0);
  readonly autoRepeatCountdown = signal(0);
  readonly showPerf = signal(false);
  readonly perfReadout = signal<{
    frameP50: number;
    frameP95: number;
    drawP95: number;
    snapAgeMs: number;
    eventsIngested: number;
    eventsDeduped: number;
    longFrames: number;
  } | null>(null);
  readonly visiblePerfReadout = computed(() => this.showPerf() ? this.perfReadout() : null);

  private renderer: GameRenderer | null = null;
  private raf = 0;
  private readonly framePerf = new PerfRing(300);
  private readonly drawPerf = new PerfRing(300);
  private lastFrameAt = -1;
  private longFrames = 0;
  private perfFrameCount = 0;
  private tier = 1;
  private waifuId: string | undefined;
  private mode: GameMode = GameMode.Dungeon;
  private keys = new Set<string>();
  private lastDir = { x: 0, y: 0 };
  private moveHeartbeat = 0;
  private resumeToastTimer = 0;
  private autoRepeatTimer = 0;
  private autoRepeatCountdownTimer = 0;
  private autoRepeatEndKey = '';
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
      if (snap?.run.ended) this.maybeScheduleAutoRepeat(snap);
      else if (snap && !snap.run.ended) this.clearAutoRepeatSchedule();
    });
  }

  ngOnInit(): void {
    this.tier = Number(this.route.snapshot.paramMap.get('tier') ?? '1');
    this.waifuId = this.route.snapshot.queryParamMap.get('waifu') ?? undefined;
    this.mode = this.route.snapshot.queryParamMap.get('mode') === 'training' ? GameMode.Training : GameMode.Dungeon;
    this.isTraining.set(this.mode === GameMode.Training);
    const runs = normalizeFarmRunCount(Number(this.route.snapshot.queryParamMap.get('runs') ?? readFarmRunCount()));
    this.plannedRuns.set(runs);
    this.autoRunsRemaining.set(Math.max(0, runs - 1));
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
    // G-03: feed skill footprints so the helper telegraph can preview the right shape.
    void this.api.loadCatalog().then((cat) => this.renderer?.setSkillShapes(cat.skills)).catch(() => undefined);
    const map = this.client.map();
    if (map) this.renderer.setMap(map);

    try {
      const joined = await this.client.joinRun(this.tier, this.waifuId, undefined, true, this.mode);
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
      if (this.lastFrameAt >= 0) {
        const frameMs = now - this.lastFrameAt;
        this.framePerf.add(frameMs);
        if (frameMs > 33) this.longFrames++;
      }
      this.lastFrameAt = now;
      // A bad frame must never kill the loop: if draw() throws, the requestAnimationFrame below would not
      // be scheduled again and the canvas would freeze while the backend/helper kept playing.
      // Isolate the frame: log the first failure with its stack, then continue.
      try {
        const drawStart = performance.now();
        this.renderer?.draw(now);
        if (this.mini?.nativeElement) this.renderer?.drawMinimap(this.mini.nativeElement);
        this.drawPerf.add(performance.now() - drawStart);
      } catch (err) {
        this.onRenderError(err);
      }
      if (this.showPerf() && ++this.perfFrameCount % 30 === 0) {
        this.perfReadout.set({
          frameP50: this.framePerf.percentile(50),
          frameP95: this.framePerf.percentile(95),
          drawP95: this.drawPerf.percentile(95),
          snapAgeMs: this.renderer?.snapshotAgeMs(now) ?? 0,
          eventsIngested: this.renderer?.eventsIngested ?? 0,
          eventsDeduped: this.renderer?.eventsDeduped ?? 0,
          longFrames: this.longFrames,
        });
      }
      this.raf = requestAnimationFrame(loop);
    };
    this.raf = requestAnimationFrame(loop);
  }

  // The render loop is best-effort: a draw error degrades to one lost frame, not a dead game.
  private renderErrorLogged = false;
  private onRenderError(err: unknown): void {
    if (this.renderErrorLogged) return; // Do not flood the console at 60fps.
    this.renderErrorLogged = true;
    console.error('[game] render loop error (keeping loop alive):', err);
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
    if (k === 'f3') {
      e.preventDefault();
      this.showPerf.update((v) => !v);
      return;
    }
    if (k === '1' || k === '2' || k === '3') {
      const idx = Number(k) - 1;
      const offer = this.snapshot()?.run?.offer;
      if (offer) { const c = offer[idx]; if (c) this.chooseCard(c.id); }
      else this.cast(idx);
    } else if (this.snapshot()?.run?.offer && k === 'r') this.rerollCards();
    else if (k === '4') this.cast(3);
    else if (k === 'r') this.cast(4);
    else if (k === 't') this.usePotion();
    else if (k === '5') this.cycleMovementMode();
    else if (k === 'b') this.toggleBag();
    else if (k === 'm') this.sound.toggleMute();
    else if (k === 'f') this.interactNearest();
    else if (k === 'v') this.targetNearest();
    else if (k === 'tab') { this.toggleStance(); e.preventDefault(); }
    // Dash/Dodge on Space (moved off Shift: 5x Shift triggers the Windows Sticky Keys popup).
    else if (k === ' ') { this.dash(); e.preventDefault(); }
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

  dash(): void {
    this.client.dash(this.lastDir.x, this.lastDir.y);
  }

  toggleBag(): void {
    this.showBag.update((v) => !v);
  }

  toggleHelper(): void {
    this.showHelper.update((v) => !v);
  }

  potionTitle(healPct: number): string {
    return `Healing potion — restores ${Math.round(healPct * 100)}% HP (T)`;
  }

  /** CSS var for an element's accent color, used to tint HUD pieces per skill/stance. */
  elVar(element: string): string {
    return GamePage.THEMED_ELEMENTS.has(element) ? `var(--el-${element})` : 'var(--accent)';
  }

  /** Remaining cooldown as a 0..1 fraction for the conic sweep. */
  cdFrac(sk: { cooldownRemainingMs: number; cooldownTotalMs: number }): number {
    return sk.cooldownTotalMs > 0 ? sk.cooldownRemainingMs / sk.cooldownTotalMs : 0;
  }

  potionCdFrac(p: { potionCooldownRemainingMs: number; potionCooldownTotalMs: number }): number {
    return p.potionCooldownTotalMs > 0 ? p.potionCooldownRemainingMs / p.potionCooldownTotalMs : 0;
  }

  dashCdFrac(p: { dashCooldownRemainingMs: number; dashCooldownTotalMs: number }): number {
    return p.dashCooldownTotalMs > 0 ? p.dashCooldownRemainingMs / p.dashCooldownTotalMs : 0;
  }

  toggleStance(): void {
    this.client.toggleStance();
  }

  /** Training Room only: flip the free-cast switch (skills/ult ignore cooldown & gauge). */
  toggleFreeCast(): void {
    this.client.setTrainingFreeCast(!this.snapshot()?.player.trainingFreeCast);
  }

  // G-10: applies a partial helper config change (merges with current state and sends it).
  private applyHelper(patch: Partial<AutoHelperSettingsDto>): void {
    const c = this.snapshot()?.player.autoHelper;
    if (!c) return;
    const next = { ...c, ...patch };
    this.client.setAutoHelper(
      next.targeting, next.skills, next.ultimate,
      next.targetPreference, next.movementMode,
      next.autoHeal, next.autoHealPct, next.navMode, next.autoCards,
    );
  }

  setAutoHelper(module: 'targeting' | 'skills' | 'ultimate', enabled: boolean): void {
    this.applyHelper({ [module]: enabled });
  }

  cycleMovementMode(): void {
    const c = this.snapshot()?.player.autoHelper;
    if (!c) return;
    this.setAutoHelperMovement(c.movementMode === 'none' ? c.defaultMovementMode : 'none');
  }

  setAutoHelperMovement(movementMode: 'none' | 'follow' | 'avoid'): void {
    this.applyHelper({ movementMode });
  }

  setTargetPreference(targetPreference: 'lowestHp' | 'nearest'): void {
    this.applyHelper({ targetPreference });
  }

  toggleAutoHeal(): void {
    const c = this.snapshot()?.player.autoHelper;
    if (!c) return;
    this.applyHelper({ autoHeal: !c.autoHeal });
  }

  setHealPct(value: string | number): void {
    const n = Math.round(Number(value));
    if (!Number.isFinite(n)) return;
    this.applyHelper({ autoHealPct: Math.min(90, Math.max(10, n)) });
  }

  toggleAutoCards(): void {
    const c = this.snapshot()?.player.autoHelper;
    if (!c) return;
    this.applyHelper({ autoCards: !c.autoCards });
  }

  toggleAutoLoot(): void {
    const c = this.snapshot()?.player.autoHelper;
    if (!c) return;
    this.applyHelper({ navMode: c.navMode === 'loot' ? 'off' : 'loot' });
  }

  saveHelperProfile(): void {
    this.client.saveHelperProfile();
    this.helperSaved.set(true);
    window.setTimeout(() => this.helperSaved.set(false), 1600);
  }

  resetHelper(): void {
    const c = this.snapshot()?.player.autoHelper;
    if (!c) return;
    this.applyHelper({
      targeting: true, skills: true, ultimate: true,
      targetPreference: 'nearest', movementMode: c.defaultMovementMode,
      autoHeal: true, autoHealPct: 50, navMode: 'loot', autoCards: true,
    });
  }

  // Signature: a plain-English line of what the helper will do, so you can "read" the build at a glance.
  helperReadout(h: AutoHelperSettingsDto): string {
    const parts: string[] = [];
    if (h.navMode === 'loot') parts.push('exploring & looting');

    if (h.targeting) parts.push(`hitting ${h.targetPreference === 'nearest' ? 'the nearest' : 'the weakest'} foe`);
    else if (!h.skills && !h.ultimate) parts.push('holding fire');

    if (h.navMode === 'off') {
      if (h.movementMode === 'follow') parts.push('chasing');
      else if (h.movementMode === 'avoid') parts.push('kiting');
      else parts.push('standing ground');
    }
    if (h.autoHeal) parts.push('auto-healing');

    if (!parts.length) return 'Idle — nothing automated.';
    const text = parts.join(' · ');
    return text.charAt(0).toUpperCase() + text.slice(1) + '.';
  }

  rarityLabel(rarity: string): string {
    return rarity === 'echo' ? 'Echo' : rarity === 'rare' ? 'Rare' : 'Common';
  }

  chooseCard(cardId: string): void {
    this.client.chooseCard(cardId);
  }

  private maybeScheduleAutoRepeat(snap: SnapshotDto): void {
    const end = snap.run.ended;
    if (!end || this.autoRunsRemaining() <= 0) return;
    const key = `${snap.run.seed}:${end.durationMs}:${end.victory ? 1 : 0}`;
    if (this.autoRepeatEndKey === key) return;

    this.clearAutoRepeatSchedule(false);
    this.autoRepeatEndKey = key;

    const delay = this.api.catalog()?.farm.autoRepeatDelayMs ?? 2500;
    const started = Date.now();
    const updateCountdown = () => {
      const remaining = Math.max(1, Math.ceil((delay - (Date.now() - started)) / 1000));
      this.autoRepeatCountdown.set(remaining);
    };
    updateCountdown();
    this.autoRepeatCountdownTimer = window.setInterval(updateCountdown, 250);
    this.autoRepeatTimer = window.setTimeout(() => {
      this.autoRunsRemaining.update((remaining) => Math.max(0, remaining - 1));
      this.clearAutoRepeatSchedule();
      void this.again(true);
    }, delay);
  }

  private clearAutoRepeatSchedule(resetKey = true): void {
    window.clearTimeout(this.autoRepeatTimer);
    window.clearInterval(this.autoRepeatCountdownTimer);
    this.autoRepeatTimer = 0;
    this.autoRepeatCountdownTimer = 0;
    this.autoRepeatCountdown.set(0);
    if (resetKey) this.autoRepeatEndKey = '';
  }

  farmProgressLabel(): string {
    const planned = this.plannedRuns();
    return `${planned - this.autoRunsRemaining()}/${planned}`;
  }

  rerollCards(): void {
    this.client.rerollCards();
  }

  banCard(cardId: string): void {
    this.client.banCard(cardId);
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
      atk: 'ATK+', haste: 'SPD+', atkspeed: 'AS+', shield: 'SHIELD', crit: 'CRIT+',
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
      physical: 'Physical', holy: 'Holy', ice: 'Ice',
      earth: 'Earth', energy: 'Energy', fire: 'Fire', support: 'Support',
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
      stats.moveSpeedPercent ? `+${(stats.moveSpeedPercent * 100).toFixed(1)}% SPD` : '',
    ].filter(Boolean);
    return `Equip: ${values.join(' · ')}`;
  }

  async again(fromAutoRepeat = false): Promise<void> {
    this.clearAutoRepeatSchedule();
    if (!fromAutoRepeat) this.autoRunsRemaining.set(0);
    await this.client.joinRun(this.tier, this.waifuId, undefined, false, this.mode);
    void this.api.refreshAccount();
  }

  async leave(): Promise<void> {
    this.clearAutoRepeatSchedule();
    this.autoRunsRemaining.set(0);
    await this.client.leave(true);
    void this.api.refreshAccount();
    void this.router.navigate(['/hunt']);
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.raf);
    window.clearInterval(this.moveHeartbeat);
    window.clearTimeout(this.resumeToastTimer);
    this.clearAutoRepeatSchedule();
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
    window.removeEventListener('blur', this.onBlur);
    void this.client.leave();
  }
}
