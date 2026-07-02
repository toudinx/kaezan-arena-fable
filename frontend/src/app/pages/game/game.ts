import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, effect, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { normalizeFarmRunCount, readFarmRunCount } from '../../core/farm-settings';
import { GameClientService, GameMode } from '../../core/game-client.service';
import { GameRenderer } from '../../core/renderer';
import { ItemIcon } from '../../core/item-icon';
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
    <div class="game-root" tabindex="0" #root>
      <canvas #cv class="game-canvas"></canvas>
      @if (resumeToast()) {
        <div class="resume-toast">Run resumed</div>
      }

      <!-- top HUD -->
      <div class="hud top">
        @if (snapshot(); as s) {
          <div class="hpbar">
            <div class="label">{{ s.player.hp }} / {{ s.player.maxHp }}</div>
            <div class="bar hp"><div class="fill" [style.width.%]="(100 * s.player.hp) / s.player.maxHp"></div></div>
            <div class="bar xp"><div class="fill" [style.width.%]="(100 * s.run.xp) / s.run.xpNext"></div></div>
            <div class="sub">Lv {{ s.run.level }} · {{ s.run.kills }} kills · 🪙 {{ s.run.gold }} · {{ s.run.tierName }}</div>
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
                    <span class="broken">⚡ ECHO BREAK · damage ×{{ activeMult(s.run.bossPostureCycle) }}</span>
                  } @else {
                    <span>Stance → break ×{{ nextMult(s.run.bossPostureCycle) }}</span>
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
          @if (s.player.trait; as tr) {
            <div class="passive" [class]="'passive trait-' + tr.kind"
                 [class.charged]="tr.max > 0 && tr.value >= tr.max"
                 [title]="tr.name">
              <span class="pname">{{ tr.name }}</span>
              @if (tr.max > 0) {
                <div class="pbar"><div class="pfill" [style.width.%]="(100 * tr.value) / tr.max"></div></div>
              }
              @if (tr.text && tr.text !== '—') { <span class="ptext">{{ tr.text }}</span> }
            </div>
          }
          <button class="stance" [class.fixed]="!s.player.canToggleStance"
                  [disabled]="!s.player.canToggleStance" (click)="toggleStance()"
                  title="Tab toggles stance">
            <span>{{ s.player.className }}</span>
            <b>{{ elementLabel(s.player.stanceElement) }}</b>
            @if (s.player.canToggleStance) { <small>TAB</small> }
          </button>
        }
        <button class="btn secondary leave" (click)="leave()">Leave</button>
        <button class="btn secondary bag-toggle" [class.on]="showBag()" (click)="toggleBag()" title="Hunt backpack (B)">🎒</button>
        <button class="btn secondary snd-toggle" [class.muted]="sound.muted()" (click)="sound.toggleMute()" [title]="sound.muted() ? 'Sound off (M)' : 'Sound on (M)'">{{ sound.muted() ? '🔇' : '🔊' }}</button>
        <button class="btn secondary helper-toggle" [class.on]="showHelper()" (click)="toggleHelper()" title="Combat helper">🤖</button>
      </div>

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
          <button class="skill dash" [class.ready]="s.player.dashReady" (click)="dash()"
                  title="Dash / Dodge (Space) - leaps 3 tiles in your movement direction, with i-frames">
            <span class="key">Spc</span>
            <span class="dashglyph">&gt;&gt;</span>
            <span class="name">Dash</span>
            @if (s.player.dashCooldownRemainingMs > 0) {
              <div class="cd" [style.height.%]="(100 * s.player.dashCooldownRemainingMs) / s.player.dashCooldownTotalMs"></div>
            }
          </button>
        </div>

        @if (showBag()) {
          <div class="bagpanel">
            <div class="baghead"><b>Hunt backpack</b><span>🪙 {{ s.run.gold }}</span></div>
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
            <h2>Choose an echo:</h2>
            <div class="offer-actions">
              @if (run.cardRerollsRemaining > 0) {
                <button class="offer-action" (click)="rerollCards()">
                  Reroll <b>{{ run.cardRerollsRemaining }}</b>
                </button>
              } @else {
                <!-- G-09: free rerolls depleted -> paid reroll (run altar shop) -->
                <button class="offer-action" [disabled]="run.gold < run.cardRerollGoldCost" (click)="rerollCards()">
                  Reroll <b>{{ run.cardRerollGoldCost }}🪙</b>
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
          <h1 [class.victory]="end.victory">{{ end.victory ? 'VICTORY' : 'DEFEAT' }}</h1>
          <p class="reason">{{ end.reason }}</p>
          <div class="stats">
            <div class="stat"><b>{{ end.kills }}</b><span>kills</span></div>
            <div class="stat"><b>{{ end.runLevel }}</b><span>level</span></div>
            <div class="stat"><b>{{ end.goldEarned }}</b><span>🪙 gold</span></div>
            <div class="stat"><b>{{ end.kaerosEarned }}</b><span>✦ kaeros</span></div>
            <div class="stat"><b>{{ end.accountXpEarned }}</b><span>Account XP</span></div>
            <div class="stat"><b>{{ formatTime(end.durationMs) }}</b><span>duration</span></div>
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
          @if (autoRunsRemaining() > 0) {
            <p class="note farm-note">Batch {{ farmProgressLabel() }}: next run in {{ autoRepeatCountdown() }}s</p>
          }
          <div class="actions">
            <button class="btn" (click)="again()">PLAY AGAIN</button>
            <button class="btn secondary" (click)="leave()">BACK TO HUNT</button>
          </div>
        </div>
      }

      @if (!snapshot()) {
        <div class="overlay"><h2>Generating dungeon...</h2></div>
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

    /* K-04: chip da passiva assinatura — nome + barra/texto do estado vivo */
    .passive { display: flex; align-items: center; gap: 7px; background: rgba(20, 16, 28, 0.82);
      border: 1px solid #6b51a8; border-radius: 8px; padding: 3px 9px; }
    .passive .pname { font-size: 11px; font-weight: 800; color: #c9aaff; letter-spacing: 0.2px; }
    .passive .ptext { font-size: 11px; font-weight: 800; color: #f4ecff; }
    .passive .pbar { width: 64px; height: 6px; background: #241a36; border-radius: 4px; overflow: hidden; }
    .passive .pfill { height: 100%; background: linear-gradient(90deg, #a07bff, #d6b3ff);
      transition: width 0.12s linear; }
    .passive.charged { border-color: #f4d35e; box-shadow: 0 0 10px rgba(244, 211, 94, 0.7); }
    .passive.charged .pfill { background: linear-gradient(90deg, #fff, #f4d35e); }
    .passive.charged .ptext, .passive.charged .pname { color: #f4d35e; }
    .stance {
      pointer-events: auto; min-width: 116px; border: 1px solid #2dd4bf; border-radius: 9px;
      background: rgba(10, 30, 32, 0.92); color: #b8fff5; padding: 6px 10px;
      display: grid; grid-template-columns: 1fr auto; gap: 1px 8px; text-align: left;
    }
    .stance span { grid-column: 1 / -1; color: #8bfff1; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .stance.fixed { border-color: #3a3a4c; background: rgba(16,16,26,0.9); }

    /* G-10: combat helper — autoplay control panel. Teal = on, echo-purple = identity, aurum = saved. */
    .helper-panel {
      position: absolute; left: 14px; bottom: 16px; z-index: 16;
      pointer-events: auto; width: 270px; max-height: 70vh; overflow-y: auto; border-radius: 12px;
      border: 1px solid rgba(196,125,255,0.18);
      background: linear-gradient(180deg, rgba(20,17,29,0.96), rgba(13,12,19,0.96));
      box-shadow: 0 12px 30px rgba(0,0,0,0.45), inset 0 1px 0 rgba(255,255,255,0.04);
      color: #d8d6e4; padding: 11px 12px 12px; display: flex; flex-direction: column; gap: 11px;
    }
    .hp-head { display: grid; grid-template-columns: 1fr auto; align-items: start; gap: 0 8px; }
    .hp-title { grid-column: 1; font-size: 10px; font-weight: 900; letter-spacing: 0.22em; text-transform: uppercase; color: #c47dff; }
    .hp-readout { grid-column: 1; font-size: 10.5px; line-height: 1.3; color: #9a98ae; min-height: 14px; }
    .hp-min {
      grid-column: 2; grid-row: 1 / span 2; align-self: center; width: 22px; height: 22px; border-radius: 7px;
      border: 1px solid rgba(255,255,255,0.1); background: rgba(255,255,255,0.03); color: #9594a8;
      font-size: 15px; font-weight: 800; line-height: 1; cursor: pointer; transition: border-color 110ms, color 110ms;
    }
    .hp-min:hover { border-color: rgba(45,212,191,0.5); color: #8bfff1; }
    .helper-toggle.on { border-color: #c47dff; color: #d9b6ff; }
    .hp-group { display: flex; flex-direction: column; gap: 6px; }
    .hp-label { font-size: 8.5px; font-weight: 800; letter-spacing: 0.16em; text-transform: uppercase; color: #6f6e84; }
    .hp-pills { display: grid; grid-template-columns: repeat(3, 1fr); gap: 5px; }
    .helper-panel .pill {
      height: 27px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.1); background: rgba(255,255,255,0.03);
      color: #9594a8; font-size: 10.5px; font-weight: 800; letter-spacing: 0.02em; cursor: pointer;
      transition: border-color 110ms, background 110ms, color 110ms;
    }
    .helper-panel .pill:hover { border-color: rgba(45,212,191,0.4); color: #cfcde0; }
    .helper-panel .pill.on { border-color: rgba(45,212,191,0.7); background: rgba(45,212,191,0.16); color: #8bfff1; }
    .helper-panel .row-pill {
      width: 100%; display: flex; align-items: center; gap: 9px; height: 34px; padding: 0 11px; text-align: left;
    }
    .helper-panel .row-pill small { color: #6f6e84; font-size: 9.5px; font-weight: 600; letter-spacing: 0; }
    .helper-panel .row-pill .dot {
      margin-left: auto; width: 9px; height: 9px; border-radius: 50%; background: #3a3a4c; flex: 0 0 auto;
      transition: background 110ms, box-shadow 110ms;
    }
    .helper-panel .row-pill.on small { color: #6fb9b0; }
    .helper-panel .row-pill.on .dot { background: #2dd4bf; box-shadow: 0 0 8px rgba(45,212,191,0.75); }
    .seg {
      display: grid; grid-auto-flow: column; grid-auto-columns: 1fr; gap: 2px; padding: 2px;
      border-radius: 9px; background: rgba(0,0,0,0.3);
    }
    .seg button {
      height: 24px; border: 0; border-radius: 7px; background: transparent; color: #8b8a9c;
      font-size: 10px; font-weight: 800; cursor: pointer; transition: background 110ms, color 110ms;
    }
    .seg button:hover { color: #cfcde0; }
    .seg button.on { background: rgba(45,212,191,0.18); color: #8bfff1; box-shadow: inset 0 0 0 1px rgba(45,212,191,0.32); }
    .seg.muted { opacity: 0.4; }
    .hp-hint { font-size: 9.5px; color: #6f6e84; line-height: 1.25; }
    .hp-slider { display: flex; align-items: center; gap: 9px; padding: 0 2px; }
    .hp-slider input[type=range] {
      flex: 1; height: 4px; -webkit-appearance: none; appearance: none; border-radius: 3px; cursor: pointer;
      background: linear-gradient(90deg, #2dd4bf, rgba(45,212,191,0.25)); outline: none;
    }
    .hp-slider input[type=range]::-webkit-slider-thumb {
      -webkit-appearance: none; appearance: none; width: 13px; height: 13px; border-radius: 50%;
      background: #8bfff1; border: 2px solid #0f1118; box-shadow: 0 0 6px rgba(45,212,191,0.7); cursor: pointer;
    }
    .hp-slider input[type=range]::-moz-range-thumb {
      width: 13px; height: 13px; border-radius: 50%; background: #8bfff1; border: 2px solid #0f1118; cursor: pointer;
    }
    .hp-slider .hp-pct { font-size: 10px; font-weight: 800; color: #8bfff1; width: 30px; text-align: right; }
    .hp-actions { display: flex; gap: 6px; padding-top: 10px; border-top: 1px solid rgba(255,255,255,0.06); }
    .hp-save {
      flex: 1; height: 28px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.12); background: rgba(255,255,255,0.03);
      color: #d8d6e4; font-size: 10px; font-weight: 800; letter-spacing: 0.02em; cursor: pointer; transition: border-color 120ms, background 120ms, color 120ms;
    }
    .hp-save:hover { border-color: rgba(240,180,80,0.55); color: #ffd98a; }
    .hp-save.saved { border-color: rgba(240,180,80,0.7); background: rgba(240,180,80,0.16); color: #ffd98a; }
    .hp-reset {
      flex: 0 0 auto; height: 28px; padding: 0 13px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.1);
      background: transparent; color: #8b8a9c; font-size: 10px; font-weight: 800; cursor: pointer;
    }
    .hp-reset:hover { color: #cfcde0; border-color: rgba(255,255,255,0.2); }
    .minimap { position: absolute; right: 14px; top: 64px; border: 1px solid #2c2c3e; border-radius: 8px; background: #000; opacity: 0.9; }
    .hud.skills { position: absolute; bottom: 16px; left: 50%; transform: translateX(-50%); display: flex; gap: 10px; }
    .train-toggle {
      position: absolute; bottom: 100px; left: 50%; transform: translateX(-50%); z-index: 16;
      display: flex; align-items: center; gap: 8px; padding: 7px 14px; border-radius: 999px; cursor: pointer;
      background: rgba(16,16,26,0.9); border: 2px solid #2c2c3e; color: #cfcde0; font-size: 12px; font-weight: 800;
    }
    .train-toggle small { color: #707088; font-weight: 600; font-size: 10px; }
    .train-toggle .dot { width: 9px; height: 9px; border-radius: 50%; background: #3a3a4e; }
    .train-toggle.on { border-color: #e8a93c; color: #fbe7c0; box-shadow: 0 0 12px rgba(232,169,60,0.4); }
    .train-toggle.on .dot { background: #e8a93c; box-shadow: 0 0 8px rgba(232,169,60,0.8); }
    .train-toggle.on small { color: #d9b878; }
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
    .skill.dash { width: 72px; cursor: pointer; }
    .skill.dash .dashglyph { font-size: 24px; font-weight: 900; line-height: 1; letter-spacing: -3px; color: #5cc8ff; }
    .skill.dash.ready { border-color: #5cc8ff; box-shadow: 0 0 10px rgba(92,200,255,0.4); }
    .skill.dash:not(.ready) { opacity: 0.6; }
    .skill.dash:not(.ready) .dashglyph { color: #46506b; }
    .bag-toggle, .snd-toggle, .helper-toggle { pointer-events: auto; font-size: 16px; padding: 4px 9px; }
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
    .overlay.cards .choices { display: flex; gap: 16px; flex-wrap: wrap; justify-content: center; max-width: 760px; }
    .offer-actions, .ban-actions { display: flex; align-items: center; gap: 10px; color: #9c9ab0; font-size: 12px; font-weight: 800; text-transform: uppercase; }
    .offer-actions button, .ban-actions button { border: 1px solid rgba(125,240,255,0.28); border-radius: 999px; background: rgba(125,240,255,0.10); color: #d7fbff; font-weight: 900; padding: 8px 12px; cursor: pointer; }
    .offer-action:disabled { opacity: .45; cursor: not-allowed; }
    .choice {
      width: 210px; min-height: 130px; border-radius: 12px; border: 2px solid #2dd4bf;
      background: linear-gradient(180deg, #16242a, #101018); color: inherit; padding: 14px;
      display: flex; flex-direction: column; gap: 6px; text-align: left;
      transition: transform 0.1s;
    }
    /* G-04: border/highlight color by rarity (common teal, rare blue, echo gold). */
    .choice[data-rarity="common"] { border-color: #2dd4bf; }
    .choice[data-rarity="rare"] { border-color: #5b9bff; box-shadow: 0 0 14px rgba(91,155,255,0.25); }
    .choice[data-rarity="echo"] {
      border-color: #ffd35d; box-shadow: 0 0 20px rgba(255,211,93,0.35);
      background: linear-gradient(180deg, #2a2410, #14110a);
    }
    .choice:hover { transform: translateY(-4px); }
    .choice p { margin: 0; color: #9c9ab0; font-size: 13px; }
    .choice .rarity { font-size: 10px; font-weight: 800; letter-spacing: 1.5px; text-transform: uppercase; }
    .choice[data-rarity="common"] .rarity { color: #2dd4bf; }
    .choice[data-rarity="rare"] .rarity { color: #5b9bff; }
    .choice[data-rarity="echo"] .rarity { color: #ffd35d; }
    .choice .tags { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 2px; }
    .choice .tag {
      font-size: 10px; font-weight: 700; padding: 1px 7px; border-radius: 999px;
      background: rgba(125,240,255,0.10); border: 1px solid rgba(125,240,255,0.25); color: #aee9ff;
    }
    .choice .stacks { color: #707088; font-size: 11px; }
    .choice .card-key { color: rgba(125, 240, 255, 0.55); font-size: 11px; font-weight: 700; font-family: monospace; align-self: flex-end; }
    .ban-actions button { color: #ffd1d1; border-color: rgba(255,93,93,0.35); background: rgba(255,93,93,0.10); }
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
    .farm-note { color: #8bfff1; font-weight: 800; }
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
  readonly showHelper = signal(false);
  /** Training Room only: reveals the free-cast switch (skills/ult ignore cooldown & gauge). */
  readonly isTraining = signal(false);

  // G-10: feedback for the HELPER panel "Save as default" button.
  readonly helperSaved = signal(false);
  readonly plannedRuns = signal(1);
  readonly autoRunsRemaining = signal(0);
  readonly autoRepeatCountdown = signal(0);

  private renderer: GameRenderer | null = null;
  private raf = 0;
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
      // A bad frame must never kill the loop: if draw() throws, the requestAnimationFrame below would not
      // be scheduled again and the canvas would freeze while the backend/helper kept playing.
      // Isolate the frame: log the first failure with its stack, then continue.
      try {
        this.renderer?.draw(now);
        if (this.mini?.nativeElement) this.renderer?.drawMinimap(this.mini.nativeElement);
      } catch (err) {
        this.onRenderError(err);
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
    return `Healing potion - restores ${Math.round(healPct * 100)}% HP (key 5)`;
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
    return rarity === 'echo' ? 'Eco' : rarity === 'rare' ? 'Raro' : 'Comum';
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
      stats.moveSpeedPercent ? `+${(stats.moveSpeedPercent * 100).toFixed(1)}% VEL` : '',
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
