import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { KaeliArtService } from '../../core/kaeli-art.service';
import { ItemIcon } from '../../core/item-icon';
import { OutfitPreview } from '../../core/outfit-preview';
import { KaeliIdle } from '../../core/ui/kaeli-idle';
import {
  ClassDef, ClassStanceDef, ELEMENT_LABELS, EquipmentSlot, GEAR_MATERIAL_ID_BASE, ItemCatalogEntry,
  MasteryNodeDef, MasteryState, RARITY_COLORS, SET_TIERS, SkillDef, SkinDef, WaifuDef, equipKey,
  isGearMaterial,
} from '../../core/types';

type KaeliTab = 'profile' | 'skins' | 'mastery' | 'equipment' | 'info';

@Component({
  selector: 'app-kaelis',
  standalone: true,
  imports: [OutfitPreview, ItemIcon, KaeliIdle],
  template: `
    <div class="atelier">

      <!-- ── ROSTER STRIP ── -->
      <nav class="roster" aria-label="Selecionar Kaeli">
        @for (w of allWaifus(); track w.id) {
          <button class="roster-item" [class.owned]="owned(w.id)" [class.active]="selected()?.id === w.id"
                  [style.--rc]="rarityColor(w.rarity)" [title]="w.name + ' - Kaeli'"
                  [attr.aria-label]="w.name" [attr.aria-pressed]="selected()?.id === w.id" (click)="select(w)">
            <span class="bust">
              @if (thumb(w.id); as t) {
                <img [src]="t" alt="" decoding="async" />
              } @else {
                <app-outfit-preview [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
                  [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
                  [addons]="skinFor(w).addons ?? 0" [size]="52" [animate]="false" />
              }
              @if (!owned(w.id)) { <span class="lock">🔒</span> }
            </span>
          </button>
        }
      </nav>

      <!-- ── ART ALCOVE ── -->
      <div class="stage">
        @if (selected(); as w) {
          <div class="bg">
            @if (bgPortrait(w); as bp) {
              <img class="bg-img" [src]="bp" alt="" decoding="async" />
            } @else {
              <img class="bg-img gradient" [src]="bgFallback(w)" alt="" decoding="async" />
            }
          </div>
          <div class="vignette"></div>
          <div class="floor" [style.--el]="elementColor(w.element)"></div>

          <div class="figure">
            @if (hasArt(w.id)) {
              <app-kaeli-idle [waifuId]="w.id" [intervalMs]="7000" />
            } @else {
              <div class="sprite-stand">
                <app-outfit-preview
                  [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
                  [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
                  [addons]="skinFor(w).addons ?? 0" [mountLookType]="mountLookType(w.id)" [size]="240" />
              </div>
            }
          </div>

          <div class="identity">
            <div class="id-tags">
              <span class="el-tag" [style.--el]="elementColor(w.element)">{{ elementLabel(w.element) }}</span>
              <span class="kaeli-tag">Kaeli</span>
            </div>
            <h1 class="id-name">{{ w.name }}</h1>
            <p class="id-title">{{ w.title }}</p>
            <div class="id-class">
              <span class="chip">{{ classFor(w)?.name }}</span>
            </div>
          </div>
        } @else {
          <div class="stage-empty">
            <p class="muted">Select a Kaeli from the sidebar.</p>
          </div>
        }
      </div>

      <!-- ── DOSSIER ── -->
      <div class="dossier">
        @if (selected(); as w) {
          @if (owned(w.id)) {
            <div class="tabs">
              @for (t of tabs; track t.id) {
                <button class="tab" [class.active]="tab() === t.id" (click)="tab.set(t.id)">{{ t.label }}</button>
              }
            </div>

            <!-- ═══ PROFILE ═══ -->
            @if (tab() === 'profile') {
              <div class="tab-content">
                <!-- attributes -->
                <section class="sheet glass">
                  <div class="sheet-stats">
                    <div class="big-stat">
                      <span class="bs-label">ATK</span>
                      <b class="bs-val">{{ w.baseAtk }}</b>
                    </div>
                    <div class="big-stat">
                      <span class="bs-label">HP</span>
                      <b class="bs-val">{{ w.baseHp }}</b>
                    </div>
                    <div class="big-stat">
                      <span class="bs-label">Affinity bonus</span>
                      <b class="bs-val accent">+{{ affinityLevel(w.id) - 1 }}%</b>
                    </div>
                  </div>
                  <div class="sheet-facts">
                    <div class="fact"><span>Element</span>
                      <b class="fact-el" [style.--el]="elementColor(w.element)">{{ elementLabel(w.element) }}</b>
                    </div>
                    <div class="fact"><span>Class</span><b>{{ classFor(w)?.name }}</b></div>
                    <div class="fact"><span>Ascension</span><b class="gold">A{{ ascension(w.id) }} / 6</b></div>
                    <div class="fact"><span>Echo Mastery</span><b>{{ masteryOf(w.id).points }} free pts</b></div>
                  </div>
                </section>

                <!-- trait + voice -->
                <section class="trait-card glass">
                  <span class="eyebrow trait-label">Trait · {{ w.trait.name }}</span>
                  <p>{{ w.trait.description }}</p>
                  <p class="personality">「 {{ w.personality }} 」</p>
                </section>

                <!-- affinity -->
                <section class="aff-card glass">
                  <div class="aff-head">
                    <span class="eyebrow">Affinity</span>
                    <span class="aff-lvl">{{ affinityLevel(w.id) }}<i> / {{ affinityMax() }}</i></span>
                  </div>
                  <div class="aff-bar"><div class="aff-fill" [style.width.%]="affinityPercent(w.id)"></div></div>
                  @if (affinityToNext(w.id) > 0) {
                    <span class="muted small">{{ affinityInto(w.id) }} / {{ affinityToNext(w.id) }} XP - play runs with her or give gifts</span>
                  } @else {
                    <span class="maxed small">Max affinity · +{{ affinityLevel(w.id) - 1 }}% ATK/HP in runs</span>
                  }
                </section>

                <!-- ascension -->
                <section class="asc-card glass">
                  <div class="asc-head">
                    <span class="eyebrow">Ascension</span>
                    <div class="asc-dots">
                      @for (i of [1,2,3,4,5,6]; track i) {
                        <span class="dot" [class.on]="ascension(w.id) >= i">◆</span>
                      }
                    </div>
                  </div>
                  <div class="asc-foot">
                    <span class="muted small">This Kaeli's shards: <b class="gold">{{ shards(w.id) }}</b></span>
                    @if (ascension(w.id) < 6) {
                      <button class="btn gold compact" [disabled]="busy() || shards(w.id) < ascCost(w.id)" (click)="ascend(w.id)">
                        Ascend - {{ ascCost(w.id) }} shards
                      </button>
                    } @else {
                      <span class="maxed">Max ascension!</span>
                    }
                  </div>
                </section>
              </div>
            }

            <!-- ═══ MASTERY ═══ -->
            @if (tab() === 'mastery') {
              <div class="tab-content">
                <div class="mastery-header glass">
                  <div>
                    <h3 style="margin:0">Echo Mastery</h3>
                    <span class="muted small">Victory +{{ pointsPerVictory() }} pt · defeat +{{ pointsPerDefeat() }} pt</span>
                  </div>
                  <div class="mastery-pts-badge">{{ masteryOf(w.id).points }}<span>pts</span></div>
                  @if (masteryOf(w.id).spent > 0) {
                    <button class="btn secondary compact" [disabled]="busy()" (click)="respec(w.id)">
                      Reset - {{ respecGold() }} gold
                    </button>
                  }
                </div>

                <div class="mastery-tree">
                  @for (branch of branches; track branch.id) {
                    <div class="branch">
                      <div class="branch-label">{{ branch.label }}</div>
                      @for (node of branchNodes(w.id, branch.id); track node.id; let last = $last) {
                        <div class="tree-node"
                             [class.unlocked]="nodeUnlocked(w.id, node)"
                             [class.available]="!nodeUnlocked(w.id, node) && nodeAvailable(w.id, node)"
                             [class.key-node]="node.order === 4"
                             [class.tree-last]="last">
                          <div class="node-dot">
                            @if (nodeUnlocked(w.id, node)) {
                              <span>✔</span>
                            } @else {
                              <span>{{ node.order }}</span>
                            }
                          </div>
                          <div class="node-info">
                            <b>{{ node.name }}</b>
                            <p>{{ node.description }}</p>
                            @if (nodeUnlocked(w.id, node)) {
                              <span class="node-done">unlocked</span>
                            } @else {
                              <div class="node-actions">
                                <span class="node-cost">{{ node.cost }} pt</span>
                                <button class="btn compact mini"
                                        [disabled]="busy() || !nodeAvailable(w.id, node)"
                                        (click)="unlockNode(w.id, node.id)">
                                  {{ nodeAvailable(w.id, node) ? 'Unlock' : nodeBlockReason(w.id, node) }}
                                </button>
                              </div>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>
            }

            <!-- ═══ EQUIPMENT ═══ -->
            @if (tab() === 'equipment') {
              <div class="tab-content">
                <div class="tier-bar">
                  <span class="eyebrow">Set by tier</span>
                  <div class="tier-seg">
                    @for (t of setTiers; track t) {
                      <button class="tier-tab" [class.active]="selectedTier() === t" (click)="selectTier(t)">T{{ t }}</button>
                    }
                  </div>
                  <button class="btn secondary compact auto-equip-btn" [disabled]="busy()" (click)="autoEquip(w.id)" title="Equips the best available item in every empty or weaker slot">
                    Auto Equip
                  </button>
                  <span class="muted small tier-hint">The dungeon uses the selected tier set.</span>
                </div>
                <!-- G-09: Echo material dropped by hunt chests (account growth) -->
                @if (gearMaterials().length > 0) {
                  <div class="materials">
                    <span class="eyebrow">Echo Materials</span>
                    <div class="mat-chips">
                      @for (m of gearMaterials(); track m.tier) {
                        <span class="mat-chip" [class.active]="m.tier === selectedTier()">T{{ m.tier }} <b>×{{ m.count }}</b></span>
                      }
                    </div>
                    <span class="muted small mat-hint">Loot from hunt chests (altars, cursed chests, and mimics). Gear forging arrives in a future update.</span>
                  </div>
                }
                @if (equipmentTotals(w.id).length > 0) {
                  <div class="equip-summary">
                    @for (stat of equipmentTotals(w.id); track stat.label) {
                      <div class="summary-stat">
                        <span>{{ stat.label }}</span>
                        <b>{{ stat.value }}</b>
                      </div>
                    }
                  </div>
                }
                <div class="paperdoll">
                  @for (slot of equipmentSlots; track slot.id) {
                    <button class="gear-slot" [class.active]="selectedEquipmentSlot() === slot.id"
                            [class.filled]="!!equippedItem(w.id, slot.id)"
                            (click)="selectedEquipmentSlot.set(slot.id)">
                      <span class="slot-name">{{ slot.label }}</span>
                      @if (equippedItem(w.id, slot.id); as item) {
                        <span class="slot-icon"><app-item-icon [itemId]="item.itemId" [size]="40" /></span>
                        <b>{{ item.name }}</b>
                        <small>{{ itemStats(item) }}</small>
                      } @else {
                        <span class="slot-icon empty">+</span>
                        <span class="empty-slot">empty</span>
                      }
                    </button>
                  }
                </div>
                @if (selectedEquipmentSlot(); as slot) {
                  <div class="gear-picker glass">
                    <div class="picker-title">
                      <b>{{ slotLabel(slot) }}</b>
                      @if (equippedItem(w.id, slot)) {
                        <button class="btn secondary compact" [disabled]="busy()" (click)="unequip(w.id, slot)">Unequip</button>
                      }
                    </div>
                    <div class="gear-options">
                      @for (item of equipmentCandidates(w.id, slot); track item.itemId) {
                        <button class="gear-option" [disabled]="busy() || !canEquip(w, item)"
                                [title]="itemRequirement(w, item)"
                                (click)="equip(w.id, slot, item.itemId)">
                          <app-item-icon [itemId]="item.itemId" [size]="38" />
                          <span>
                            <b>{{ item.name }}</b>
                            <small>{{ itemStats(item) }}</small>
                            @if (itemRequirement(w, item)) {
                              <small class="req-locked">{{ itemRequirement(w, item) }}</small>
                            }
                          </span>
                        </button>
                      } @empty {
                        <span class="muted">No items for this slot in the Backpack.</span>
                      }
                    </div>
                  </div>
                }
              </div>
            }

            <!-- ═══ INFO ═══ -->
            @if (tab() === 'info') {
              <div class="tab-content">
                <div class="info-layout">
                  <nav class="info-nav" aria-label="Info sections">
                    <button class="info-navitem" [class.active]="infoSection() === 'class'" (click)="infoSection.set('class')">
                      <span class="ina-label">Class</span>
                      <span class="ina-sub">{{ classFor(w)?.name }}</span>
                    </button>
                    <button class="info-navitem" [class.active]="infoSection() === 'echoes'" (click)="infoSection.set('echoes')">
                      <span class="ina-label">Memory Echoes</span>
                      <span class="ina-sub">{{ unlockedLoreCount(w) }} / {{ w.lore.length }} revealed</span>
                    </button>
                    <button class="info-navitem" [class.active]="infoSection() === 'gifts'" (click)="infoSection.set('gifts')">
                      <span class="ina-label">Gifts</span>
                      <span class="ina-sub">{{ giftsLeft(w.id) }} left today</span>
                    </button>
                  </nav>

                  <div class="info-panel glass">
                    @switch (infoSection()) {

                      @case ('class') {
                        @if (classFor(w); as cls) {
                          <div class="class-head">
                            <span class="eyebrow">Classe · {{ cls.name }}</span>
                            <div class="stances">
                              @for (stance of cls.stances; track stance.id) {
                                <button class="stance-tab" [class.active]="previewStanceId() === stance.id"
                                        (click)="previewStanceId.set(stance.id)">
                                  {{ elementLabel(stance.element) }}
                                </button>
                              }
                            </div>
                          </div>
                          <p class="muted small class-desc">{{ cls.description }}</p>
                          @for (s of kit(w); track s.id; let i = $index) {
                            <div class="skill">
                              <span class="key" [class.ult]="i === 4">{{ ['1','2','3','4','R'][i] }}</span>
                              <div class="skill-body">
                                <div class="skill-top">
                                  <b>{{ s.name }}</b>
                                  <span class="element-name">{{ elementLabel(s.element) }}</span>
                                  <span class="muted small skill-cd">{{ i === 4 ? 'Ultimate · gauge' : s.cooldownMs / 1000 + 's' }}</span>
                                </div>
                                <p>{{ s.description }}</p>
                              </div>
                            </div>
                          }
                        }
                      }

                      @case ('echoes') {
                        <span class="eyebrow">Memory Echoes</span>
                        <ol class="lore-list">
                          @for (fragment of w.lore; track $index) {
                            <li class="lore-entry" [class.locked]="!loreUnlocked(w.id, $index)">
                              <span class="lore-num">{{ $index + 1 }}</span>
                              @if (loreUnlocked(w.id, $index)) {
                                <p>{{ fragment }}</p>
                              } @else {
                                <p>🔒 Unlocks at affinity {{ loreLevelFor($index) }}.</p>
                              }
                            </li>
                          }
                        </ol>
                      }

                      @case ('gifts') {
                        <div class="gift-head">
                          <div>
                            <span class="eyebrow">Gifts</span>
                            <p class="gift-sub muted small">Raises affinity. Favorites grant XP ×{{ favoriteMultiplier() }}.</p>
                          </div>
                          <span class="gift-left" [class.spent]="giftsLeft(w.id) === 0">{{ giftsLeft(w.id) }}<i>/ day</i></span>
                        </div>

                        @if (w.favoriteGiftItemIds.length) {
                          <div class="fav-row">
                            @for (itemId of w.favoriteGiftItemIds; track itemId) {
                              <span class="fav-item" [title]="itemName(itemId) + ' (favorite)'">
                                <app-item-icon [itemId]="itemId" [size]="26" />
                                <span class="fav-heart">❤</span>
                              </span>
                            }
                          </div>
                        }

                        @if (giftsLeft(w.id) > 0) {
                          <div class="gift-options">
                            @for (item of giftCandidates(); track item.itemId) {
                              <button class="gift-option" [class.fav]="isFavorite(w, item.itemId)"
                                      [disabled]="busy()" (click)="gift(w.id, item.itemId)"
                                      [title]="'Gift ' + item.name">
                                <app-item-icon [itemId]="item.itemId" [size]="30" />
                                <span class="gift-meta">
                                  <b>{{ item.name }}</b>
                                  <small class="gift-xp">+{{ giftXpFor(w, item) }} XP @if (isFavorite(w, item.itemId)) {<i>❤</i>}</small>
                                </span>
                              </button>
                            } @empty {
                              <p class="empty-note muted">The Backpack is empty. Bring loot from runs to gift {{ w.name }}.</p>
                            }
                          </div>
                        } @else {
                          <p class="empty-note muted">{{ w.name }} has received enough gifts today. Come back tomorrow.</p>
                        }
                      }
                    }
                  </div>
                </div>
              </div>
            }

            <!-- ═══ SKINS ═══ -->
            @if (tab() === 'skins') {
              <div class="tab-content skins-tab">
                <div class="skins-carousel-wrap">
                  @if (w.skins.length > 3) {
                    <button class="carousel-arrow left" type="button" aria-label="Previous skins"
                            (click)="sc.scrollBy({ left: -340, behavior: 'smooth' })">‹</button>
                    <button class="carousel-arrow right" type="button" aria-label="Next skins"
                            (click)="sc.scrollBy({ left: 340, behavior: 'smooth' })">›</button>
                  }
                  <div class="skins-carousel" #sc>
                    @for (skin of w.skins; track skin.id) {
                      <div class="skin-card glass" [class.selected]="isSelectedSkin(w, skin)"
                           [class.locked]="!skinUnlocked(w, skin)">
                        @if (isSelectedSkin(w, skin)) { <span class="skin-pin">✓ In use</span> }
                        <div class="skin-art">
                          <app-outfit-preview [lookType]="skin.lookType" [head]="skin.head" [body]="skin.body"
                            [legs]="skin.legs" [feet]="skin.feet" [addons]="skin.addons ?? 0"
                            [mountLookType]="skin.mountLookType ?? 0" [size]="128" />
                        </div>
                        <div class="skin-body">
                          <div class="skin-top">
                            <b>{{ skin.name }}</b>
                            <span class="skin-badge" [class.gold]="skin.unlock === 'gold' || skin.unlock === 'kaeros'">{{ skinBadge(skin) }}</span>
                          </div>
                          <p class="skin-desc">{{ skin.description }}</p>
                          <div class="skin-cta">
                            @if (isSelectedSkin(w, skin)) {
                              <span class="skin-current">Equipped</span>
                            } @else if (skinUnlocked(w, skin)) {
                              <button class="btn secondary compact" [disabled]="busy()" (click)="selectSkin(w.id, skin.id)">Equip</button>
                            } @else if (skin.unlock === 'gold' || skin.unlock === 'kaeros') {
                              <button class="btn gold compact" [disabled]="busy() || !canAfford(skin)"
                                      (click)="buySkin(w.id, skin.id)">
                                Buy · {{ skin.unlockValue }} {{ skin.unlock === 'gold' ? 'gold' : 'Kaeros' }}
                              </button>
                            } @else {
                              <span class="skin-req muted small">🔒 Affinity {{ skin.unlockValue }}</span>
                            }
                          </div>
                        </div>
                      </div>
                    }
                  </div>
                </div>
                <p class="muted small skins-note">The equipped skin appears in the Hub, runs, and this page.</p>
              </div>
            }

          } @else {
            <!-- not recruited -->
            <div class="not-owned">
              <span class="eyebrow">Not recruited yet</span>
              <p class="muted">{{ selected()?.name }} waits in the banner. Try your luck in Recruit.</p>
              <div class="trait-card glass">
                <span class="eyebrow trait-label">Trait</span>
                <b>{{ w.trait.name }}</b>
                <p>{{ w.trait.description }}</p>
              </div>
            </div>
          }
        } @else {
          <p class="muted" style="padding:24px">Loading...</p>
        }
      </div>
    </div>
  `,
  styles: [`
    /* local accent mixes (deduped) */
    :host { display: block; }
    .atelier {
      display: grid;
      grid-template-columns: minmax(320px, 36%) 1fr;
      grid-template-rows: auto 1fr;
      grid-template-areas: "roster roster" "stage dossier";
      height: calc(100dvh - 53px); background: var(--bg-1);
      --af: color-mix(in srgb, var(--accent) 12%, var(--bg-2));
      --ae: color-mix(in srgb, var(--accent) 38%, transparent);
    }

    /* roster — horizontal strip; only art + rarity frame */
    .roster { grid-area: roster; display: flex; flex-direction: row; align-items: center; justify-content: safe center; gap: var(--sp-2); padding: var(--sp-3) var(--sp-4); overflow-x: auto; overflow-y: hidden; background: var(--bg-0); border-bottom: 1px solid var(--line); }
    .roster::-webkit-scrollbar { height: 5px; }
    .roster-item { position: relative; flex: 0 0 auto; padding: 0; color: var(--text); border-radius: var(--r-md); transition: transform var(--dur) var(--ease-out); }
    .roster-item:not(.owned) { filter: grayscale(0.85) brightness(0.5); }
    .roster-item:hover { transform: translateY(-2px); }
    .roster-item:hover .bust { border-color: var(--rc); }
    .roster-item.active { transform: translateY(-3px); }
    .roster-item.active .bust { border-color: var(--rc); box-shadow: 0 0 0 2px var(--bg-0), 0 0 0 4px var(--rc), 0 6px 18px rgba(0,0,0,0.55); }
    .roster-item:focus-visible { outline: none; }
    .roster-item:focus-visible .bust { outline: 2px solid var(--accent-bright); outline-offset: 2px; }
    .bust { position: relative; width: 58px; height: 58px; display: flex; align-items: center; justify-content: center; overflow: hidden; border-radius: var(--r-md); border: 2px solid color-mix(in srgb, var(--rc) 55%, transparent); background: var(--bg-2); box-shadow: var(--glass-edge); transition: border-color var(--dur) var(--ease-out), box-shadow var(--dur) var(--ease-out); }
    .roster-item:not(.owned) .bust { border-color: var(--line-strong); }
    .bust img { width: 100%; height: 100%; object-fit: cover; }
    .lock { position: absolute; top: 2px; right: 2px; font-size: 11px; filter: drop-shadow(0 1px 2px #000); }

    /* stage / art alcove */
    .stage { grid-area: stage; position: relative; overflow: hidden; isolation: isolate; }
    .bg { position: absolute; inset: 0; z-index: -2; }
    .bg-img { width: 100%; height: 100%; object-fit: cover; object-position: center top; }
    .bg-img.gradient { object-position: center; }
    .vignette { position: absolute; inset: 0; z-index: -1; pointer-events: none; background: linear-gradient(0deg, rgba(7,7,13,0.96) 2%, rgba(7,7,13,0.15) 45%, rgba(7,7,13,0.42) 100%); }
    .floor { position: absolute; left: 50%; bottom: 9%; z-index: -1; transform: translateX(-50%); width: 64%; height: 64px; border-radius: 50%; pointer-events: none; background: radial-gradient(ellipse at center, color-mix(in srgb, var(--el) 55%, transparent), transparent 70%); filter: blur(10px); opacity: 0.7; }
    .figure { position: absolute; inset: 0; padding-bottom: 4%; filter: drop-shadow(0 14px 34px rgba(0,0,0,0.55)); }
    .sprite-stand { position: absolute; inset: 0; display: flex; align-items: flex-end; justify-content: center; padding-bottom: 8%; filter: drop-shadow(0 18px 40px rgba(0,0,0,0.6)); }
    .identity { position: absolute; left: clamp(16px, 4%, 32px); right: 16px; bottom: clamp(18px, 4vh, 32px); z-index: 2; }
    .id-tags { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; }
    .el-tag { font-size: var(--fs-xs); font-weight: 700; text-transform: uppercase; letter-spacing: var(--tracking-eyebrow); color: var(--el); padding: 4px 11px; border-radius: var(--r-full); border: 1px solid color-mix(in srgb, var(--el) 50%, transparent); background: color-mix(in srgb, var(--el) 14%, transparent); }
    .kaeli-tag { font-size: var(--fs-xs); font-weight: 900; text-transform: uppercase; letter-spacing: var(--tracking-eyebrow); color: #2a1700; padding: 4px 11px; border-radius: var(--r-full); background: linear-gradient(180deg, var(--gold-bright), var(--gold-deep)); box-shadow: 0 6px 18px rgba(232,169,60,0.18); }
    .id-name { font-family: var(--font-display); font-weight: 900; font-size: clamp(1.9rem, 4vw, 2.9rem); line-height: 0.96; margin: 0; letter-spacing: -0.01em; text-shadow: 0 4px 30px rgba(0,0,0,0.7); }
    .id-title { font-family: var(--font-display); font-style: italic; color: var(--accent-bright); font-size: 1.1rem; margin: 4px 0 10px; }
    .id-class { display: flex; gap: 6px; flex-wrap: wrap; }
    .id-class .chip { font-size: var(--fs-xs); font-weight: 700; color: var(--text-dim); background: var(--glass-bg); border: 1px solid var(--line-strong); border-radius: var(--r-full); padding: 3px 10px; }
    .stage-empty { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; text-align: center; padding: var(--sp-5); }

    /* dossier */
    .dossier { grid-area: dossier; display: flex; flex-direction: column; overflow-y: auto; padding: var(--sp-5) var(--sp-5) var(--sp-7); }
    .tabs { position: sticky; top: calc(-1 * var(--sp-5)); z-index: 4; display: flex; gap: 2px; flex-wrap: wrap; padding: var(--sp-2) 0; margin-bottom: var(--sp-4); border-bottom: 1px solid var(--line); background: linear-gradient(180deg, var(--bg-1) 70%, transparent); }
    .tab { background: none; border: none; border-bottom: 2px solid transparent; color: var(--text-mute); padding: 8px 14px; font-size: var(--fs-sm); font-weight: 700; transition: all var(--dur) var(--ease-out); }
    .tab.active { color: var(--accent-bright); border-bottom-color: var(--accent); }
    .tab:hover:not(.active) { color: var(--text-dim); }
    .tab-content { display: flex; flex-direction: column; gap: var(--sp-4); }

    /* profile - character sheet */
    .sheet { padding: var(--sp-4); display: flex; flex-direction: column; gap: var(--sp-3); }
    .sheet-stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--sp-2); }
    .big-stat { display: flex; flex-direction: column; align-items: center; gap: 3px; padding: 12px 8px; background: var(--bg-2); border: 1px solid var(--line); border-radius: var(--r-md); text-align: center; }
    .bs-label { font-size: var(--fs-xs); color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.05em; line-height: 1.2; }
    .bs-val { font-family: var(--font-display); font-size: 1.7rem; font-weight: 700; line-height: 1; }
    .bs-val.accent { color: var(--accent-bright); }
    .sheet-facts { display: grid; grid-template-columns: 1fr 1fr; gap: 1px; background: var(--line); border: 1px solid var(--line); border-radius: var(--r-md); overflow: hidden; }
    .fact { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 9px 13px; background: var(--bg-2); }
    .fact span { font-size: var(--fs-sm); color: var(--text-mute); }
    .fact b { font-size: var(--fs-sm); font-weight: 700; }
    .fact b.gold { color: var(--gold-bright); }
    .fact-el { color: var(--el); }

    .trait-card { padding: 14px 16px; display: flex; flex-direction: column; gap: 6px; }
    .trait-label { color: var(--accent-bright); }
    .trait-card p { margin: 0; color: var(--text-dim); font-size: var(--fs-sm); line-height: 1.5; }
    .personality { color: var(--text-mute); font-style: italic; }

    .aff-card, .asc-card { padding: 14px 16px; display: flex; flex-direction: column; gap: 8px; }
    .aff-head, .asc-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .aff-lvl { font-family: var(--font-display); font-size: 1.3rem; font-weight: 700; color: var(--accent-bright); }
    .aff-lvl i { font-style: normal; font-size: 0.85rem; color: var(--text-mute); }
    .asc-dots { display: flex; gap: 6px; }
    .dot { color: var(--line-strong); font-size: 14px; }
    .dot.on { color: var(--gold); text-shadow: 0 0 8px var(--gold-glow); }
    .asc-foot { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
    .maxed { color: var(--gold-bright); font-weight: 700; }

    /* mastery */
    .mastery-header { display: flex; align-items: center; gap: var(--sp-4); flex-wrap: wrap; padding: 12px 16px; }
    .mastery-pts-badge { margin-left: auto; background: color-mix(in srgb, var(--gold) 12%, var(--bg-2)); border: 1px solid color-mix(in srgb, var(--gold) 35%, transparent); border-radius: var(--r-md); padding: 6px 14px; font-family: var(--font-display); font-size: 1.5rem; font-weight: 700; color: var(--gold-bright); display: flex; align-items: baseline; gap: 4px; }
    .mastery-pts-badge span { font-size: var(--fs-xs); color: var(--gold-deep); font-family: var(--font-ui); }
    .mastery-tree { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--sp-4); }
    .branch-label { font-size: var(--fs-xs); font-weight: 800; color: var(--accent-bright); text-transform: uppercase; letter-spacing: var(--tracking-eyebrow); text-align: center; margin-bottom: 14px; }
    .tree-node { position: relative; display: flex; gap: 10px; align-items: flex-start; padding-bottom: 20px; }
    .tree-node:not(.tree-last)::after { content: ''; position: absolute; left: 14px; top: 32px; bottom: 0; width: 2px; background: var(--line-strong); }
    .tree-node.unlocked::after { background: var(--accent-glow); }
    .node-dot { flex-shrink: 0; width: 30px; height: 30px; border-radius: 50%; border: 2px solid var(--line-strong); background: var(--bg-2); display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 800; color: var(--text-mute); position: relative; z-index: 1; }
    .tree-node.unlocked .node-dot { border-color: var(--accent); background: var(--af); color: var(--accent-bright); box-shadow: 0 0 10px var(--accent-glow); }
    .tree-node.available .node-dot { border-color: var(--ae); color: var(--accent-bright); }
    .tree-node.key-node .node-dot { width: 34px; height: 34px; border-radius: 6px; transform: rotate(45deg); }
    .tree-node.key-node .node-dot span { transform: rotate(-45deg); display: block; }
    .node-info { flex: 1; border-radius: var(--r-sm); padding: 8px 10px; background: var(--bg-2); border: 1px solid var(--line); }
    .tree-node.unlocked .node-info { border-color: var(--ae); background: var(--af); }
    .tree-node.available .node-info { border-color: var(--ae); }
    .node-info b { font-size: 12px; display: block; margin-bottom: 3px; }
    .node-info p { margin: 0 0 6px; color: var(--text-dim); font-size: 11px; line-height: 1.4; }
    .node-done { color: var(--accent-bright); font-size: 11px; font-weight: 800; }
    .node-actions { display: flex; align-items: center; gap: 8px; }
    .node-cost { color: var(--gold-bright); font-size: 11px; font-weight: 800; }

    /* equipment */
    .tier-bar { display: flex; align-items: center; gap: var(--sp-3); flex-wrap: wrap; }
    .tier-seg { display: inline-flex; gap: 2px; padding: 3px; background: var(--bg-2); border: 1px solid var(--line); border-radius: var(--r-md); }
    .tier-tab { border: none; background: none; color: var(--text-dim); padding: 5px 14px; font-size: 12px; font-weight: 800; border-radius: var(--r-sm); transition: all var(--dur) var(--ease-out); }
    .tier-tab:hover:not(.active) { color: var(--text); }
    .tier-tab.active { background: color-mix(in srgb, var(--gold) 16%, var(--bg-3)); color: var(--gold-bright); box-shadow: var(--glass-edge); }
    .auto-equip-btn { margin-left: auto; }
    .tier-hint { flex-basis: 100%; }
    /* G-09: Echo materials (chest loot) */
    .materials { display: flex; align-items: center; gap: var(--sp-2); flex-wrap: wrap; margin: var(--sp-1) 0 var(--sp-2); }
    .mat-chips { display: inline-flex; gap: 6px; flex-wrap: wrap; }
    .mat-chip { display: inline-flex; align-items: center; gap: 4px; padding: 4px 10px; font-size: 11px; font-weight: 800; color: var(--text-dim); background: var(--bg-2); border: 1px solid var(--line); border-radius: 999px; }
    .mat-chip b { color: #c47dff; }
    .mat-chip.active { border-color: color-mix(in srgb, #c47dff 50%, var(--line)); color: var(--text); box-shadow: var(--glass-edge); }
    .mat-hint { flex-basis: 100%; }
    .equip-summary { display: flex; gap: var(--sp-2); flex-wrap: wrap; }
    .summary-stat { flex: 1; min-width: 64px; display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 10px 8px; background: var(--bg-2); border: 1px solid var(--line); border-radius: var(--r-md); }
    .summary-stat span { font-size: 10px; color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.05em; }
    .summary-stat b { font-family: var(--font-display); font-size: 1.3rem; color: var(--accent-bright); }
    .paperdoll { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--sp-2); }
    .gear-slot { min-height: 124px; border-radius: var(--r-md); padding: 11px 8px; display: flex; flex-direction: column; align-items: center; justify-content: flex-start; gap: 4px; background: var(--bg-2); border: 1px solid var(--line); color: inherit; transition: all var(--dur) var(--ease-out); }
    .gear-slot:hover { border-color: var(--line-strong); transform: translateY(-1px); }
    .gear-slot.active { border-color: var(--accent); background: var(--af); box-shadow: var(--glass-edge), var(--sh-accent); }
    .slot-name { color: var(--text-mute); font-size: 10px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.06em; }
    .gear-slot.filled .slot-name, .gear-slot.active .slot-name { color: var(--accent-bright); }
    .slot-icon { width: 48px; height: 48px; display: flex; align-items: center; justify-content: center; margin: 2px 0; }
    .slot-icon.empty { font-size: 22px; color: var(--text-faint); border: 1px dashed var(--line-strong); border-radius: var(--r-sm); }
    .gear-slot b { font-size: 11px; text-align: center; line-height: 1.2; }
    .gear-slot small { color: var(--text-dim); font-size: 10px; text-align: center; line-height: 1.3; }
    .empty-slot { color: var(--text-faint); font-size: 11px; }
    .gear-picker { padding: 14px 16px; }
    .picker-title { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
    .picker-title b { font-family: var(--font-display); font-size: var(--fs-h3); }
    .gear-options { display: grid; grid-template-columns: repeat(auto-fill, minmax(168px, 1fr)); gap: var(--sp-2); max-height: 300px; overflow-y: auto; padding-right: 2px; }
    .gear-option { border-radius: var(--r-sm); padding: 8px 10px; display: flex; align-items: center; gap: 10px; text-align: left; background: var(--bg-2); border: 1px solid var(--line); color: inherit; transition: all var(--dur) var(--ease-out); }
    .gear-option:not([disabled]):hover { border-color: var(--accent); transform: translateY(-1px); }
    .gear-option[disabled] { opacity: 0.5; cursor: not-allowed; }
    .gear-option span { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
    .gear-option b { font-size: 12px; }
    .gear-option small { color: var(--text-dim); font-size: 10px; }
    .req-locked { color: var(--danger) !important; }

    /* info - vertical sub-nav + single panel */
    .info-layout { display: grid; grid-template-columns: 176px 1fr; gap: var(--sp-3); align-items: start; }
    .info-nav { display: flex; flex-direction: column; gap: var(--sp-2); }
    .info-navitem { display: flex; flex-direction: column; align-items: flex-start; gap: 2px; text-align: left; padding: 10px 13px; border-radius: var(--r-md); background: var(--bg-2); border: 1px solid var(--line); border-left: 3px solid transparent; color: var(--text); transition: all var(--dur) var(--ease-out); }
    .info-navitem:hover:not(.active) { border-color: var(--line-strong); }
    .info-navitem.active { border-color: var(--ae); border-left-color: var(--accent); background: var(--af); }
    .ina-label { font-size: 13px; font-weight: 700; }
    .info-navitem.active .ina-label { color: var(--accent-bright); }
    .ina-sub { font-size: 10px; color: var(--text-mute); }
    .info-panel { padding: 16px 18px; display: flex; flex-direction: column; gap: var(--sp-3); min-width: 0; }
    .aff-bar { height: 8px; background: var(--bg-3); border-radius: var(--r-full); overflow: hidden; }
    .aff-fill { height: 100%; background: linear-gradient(90deg, var(--accent-bright), var(--accent-dim)); border-radius: var(--r-full); transition: width var(--dur-slow) var(--ease-out); }

    /* gifts */
    .gift-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
    .gift-sub { margin: 4px 0 0; }
    .gift-left { font-family: var(--font-display); font-size: 1.5rem; font-weight: 700; color: var(--accent-bright); white-space: nowrap; line-height: 1; }
    .gift-left i { font-style: normal; font-size: 0.7rem; color: var(--text-mute); margin-left: 3px; }
    .gift-left.spent { color: var(--text-faint); }
    .fav-row { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .fav-item { position: relative; display: inline-flex; align-items: center; justify-content: center; width: 40px; height: 40px; border-radius: var(--r-sm); background: color-mix(in srgb, var(--gold) 10%, var(--bg-2)); border: 1px solid color-mix(in srgb, var(--gold) 40%, transparent); }
    .fav-heart { position: absolute; bottom: -4px; right: -4px; font-size: 10px; filter: drop-shadow(0 1px 1px #000); }
    .gift-options { display: grid; grid-template-columns: repeat(auto-fill, minmax(152px, 1fr)); gap: var(--sp-2); max-height: 268px; overflow-y: auto; padding-right: 2px; }
    .gift-option { border-radius: var(--r-sm); padding: 7px 9px; display: flex; align-items: center; gap: 9px; text-align: left; background: var(--bg-2); border: 1px solid var(--line); color: inherit; transition: all var(--dur) var(--ease-out); }
    .gift-option:not([disabled]):hover { border-color: var(--accent); transform: translateY(-1px); }
    .gift-option.fav { border-color: color-mix(in srgb, var(--gold) 45%, transparent); background: color-mix(in srgb, var(--gold) 7%, var(--bg-2)); }
    .gift-meta { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
    .gift-meta b { font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .gift-xp { color: var(--gold-bright); font-size: 10px; font-weight: 700; }
    .gift-xp i { font-style: normal; }
    .empty-note { margin: 0; font-size: var(--fs-sm); line-height: 1.5; }

    /* memory echoes */
    .lore-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--sp-3); }
    .lore-entry { display: flex; gap: 12px; align-items: flex-start; }
    .lore-num { flex-shrink: 0; width: 22px; height: 22px; border-radius: 50%; background: var(--af); border: 1px solid var(--ae); color: var(--accent-bright); font-size: 11px; font-weight: 800; display: flex; align-items: center; justify-content: center; }
    .lore-entry p { margin: 0; color: var(--text-dim); font-size: var(--fs-sm); line-height: 1.55; }
    .lore-entry.locked .lore-num { background: var(--bg-3); border-color: var(--line); color: var(--text-faint); }
    .lore-entry.locked p { color: var(--text-faint); }

    /* class kit */
    .class-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
    .class-desc { margin: 0; }
    .stances { display: flex; gap: 6px; flex-wrap: wrap; }
    .stance-tab { border: 1px solid var(--line-strong); border-radius: var(--r-sm); background: var(--bg-2); color: var(--text-dim); padding: 5px 11px; font-size: 12px; font-weight: 800; transition: all var(--dur) var(--ease-out); }
    .stance-tab.active { border-color: var(--accent); color: var(--accent-bright); background: var(--af); }
    .skill { display: flex; gap: 12px; align-items: flex-start; }
    .skill .key { background: var(--bg-3); border: 1px solid var(--line-strong); border-radius: var(--r-sm); width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-weight: 800; flex-shrink: 0; font-size: 12px; }
    .skill .key.ult { border-color: var(--gold); color: var(--gold-bright); }
    .skill-body { display: flex; flex-direction: column; gap: 2px; }
    .skill-top { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .skill-top b { font-size: 13px; }
    .skill p { margin: 0; color: var(--text-dim); font-size: 12px; line-height: 1.45; }
    .element-name { color: var(--accent-bright); font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .skill-cd { font-size: 11px; }

    /* skins - horizontal carousel that fills the area */
    .skins-tab { flex: 1; min-height: 0; }
    .skins-carousel-wrap { position: relative; flex: 1; min-height: 0; }
    .skins-carousel { display: flex; gap: var(--sp-3); height: 100%; overflow-x: auto; overflow-y: hidden; scroll-snap-type: x proximity; padding: 4px 2px 8px; scroll-padding: 0 44px; }
    .skin-card { position: relative; flex: 1 1 260px; min-width: 240px; max-width: 460px; height: 100%; scroll-snap-align: center; padding: 0; display: flex; flex-direction: column; overflow: hidden; transition: transform var(--dur) var(--ease-out), border-color var(--dur) var(--ease-out), box-shadow var(--dur) var(--ease-out); }
    .skin-card:hover { transform: translateY(-3px); }
    .skin-card.selected { border-color: var(--accent); box-shadow: var(--glass-edge), var(--sh-accent); }
    .skin-card.locked { opacity: 0.86; }
    .skin-pin { position: absolute; top: 10px; right: 10px; z-index: 2; background: var(--accent); color: #0b0820; font-size: 10px; font-weight: 800; padding: 4px 10px; border-radius: var(--r-full); box-shadow: var(--sh-1); }
    .skin-art { flex: 1; min-height: 160px; display: flex; align-items: center; justify-content: center; background: radial-gradient(ellipse at 50% 58%, color-mix(in srgb, var(--accent) 14%, transparent), transparent 70%), var(--bg-2); border-bottom: 1px solid var(--line); }
    .skin-body { padding: 14px 16px 16px; display: flex; flex-direction: column; gap: 7px; }
    .skin-top { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; }
    .skin-top b { font-size: 15px; font-family: var(--font-display); line-height: 1.15; }
    .skin-badge { font-size: 9px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.04em; color: var(--text-mute); white-space: nowrap; padding-top: 3px; }
    .skin-badge.gold { color: var(--gold-bright); }
    .skin-desc { color: var(--text-dim); font-size: 12px; margin: 0; line-height: 1.5; flex: 1; }
    .skin-cta { margin-top: 2px; }
    .skin-cta .btn { width: 100%; }
    .skin-current { display: block; text-align: center; color: var(--accent-bright); font-weight: 800; font-size: 12px; padding: 8px 0; border-top: 1px solid var(--line); }
    .skin-req { display: block; text-align: center; padding: 8px 0; }
    .carousel-arrow { position: absolute; top: 50%; transform: translateY(-50%); z-index: 4; width: 40px; height: 40px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 24px; line-height: 1; color: var(--text); background: var(--glass-bg-strong); -webkit-backdrop-filter: blur(var(--glass-blur)); backdrop-filter: blur(var(--glass-blur)); border: 1px solid var(--line-strong); box-shadow: var(--glass-edge), var(--sh-2); transition: all var(--dur) var(--ease-out); }
    .carousel-arrow:hover { border-color: var(--accent); transform: translateY(-50%) scale(1.06); }
    .carousel-arrow.left { left: -6px; }
    .carousel-arrow.right { right: -6px; }
    .skins-note { margin: 8px 0 0; }

    .not-owned { display: flex; flex-direction: column; gap: var(--sp-3); max-width: 480px; }
    .not-owned .trait-card { gap: 6px; }
    .btn.compact { padding: 7px 14px; font-size: 12px; }
    .btn.mini { padding: 4px 9px; font-size: 10px; border-radius: var(--r-sm); }
    .small { font-size: var(--fs-sm); }

    @media (max-width: 920px) {
      .atelier {
        grid-template-columns: 1fr;
        grid-template-rows: auto 44vh 1fr;
        grid-template-areas: "roster" "stage" "dossier";
        height: auto; min-height: calc(100dvh - 53px);
      }
      .roster-item:hover { transform: none; }
      .stage { min-height: 44vh; }
      .id-name { font-size: clamp(1.8rem, 9vw, 2.6rem); }
      .mastery-tree, .sheet-facts { grid-template-columns: 1fr; }
      .info-layout { grid-template-columns: 1fr; }
      .info-nav { flex-direction: row; overflow-x: auto; padding-bottom: 2px; }
      .info-navitem { flex: 0 0 auto; border-left: 1px solid var(--line); border-bottom: 3px solid transparent; }
      .info-navitem.active { border-left-color: var(--ae); border-bottom-color: var(--accent); }
      .skins-carousel { height: auto; }
      .skin-card { flex: 0 0 78%; height: auto; }
      .skin-art { min-height: 200px; }
      .tabs { top: 0; }
    }
  `],
})
export class KaelisPage implements OnDestroy {
  readonly allWaifus = computed(() => {
    const list = [...(this.api.catalog()?.waifus ?? [])];
    const owned = new Set(this.api.account()?.ownedWaifus ?? []);
    return list.sort((a, b) =>
      Number(owned.has(b.id)) - Number(owned.has(a.id))
      || b.rarity - a.rarity
      || a.name.localeCompare(b.name),
    );
  });
  readonly selected = signal<WaifuDef | null>(null);
  readonly tab = signal<KaeliTab>('profile');
  readonly infoSection = signal<'class' | 'echoes' | 'gifts'>('class');
  readonly previewStanceId = signal('');
  readonly selectedEquipmentSlot = signal<EquipmentSlot | null>(null);
  readonly selectedTier = signal(1);
  readonly setTiers = SET_TIERS;
  readonly busy = signal(false);
  readonly tabs: { id: KaeliTab; label: string }[] = [
    { id: 'profile', label: 'Profile' },
    { id: 'mastery', label: 'Mastery' },
    { id: 'equipment', label: 'Equipment' },
    { id: 'info', label: 'Info' },
    { id: 'skins', label: 'Skins' },
  ];
  readonly branches: { id: 'off' | 'def' | 'eco'; label: string }[] = [
    { id: 'off', label: 'Offense' },
    { id: 'def', label: 'Defense' },
    { id: 'eco', label: 'Eco' },
  ];
  readonly equipmentSlots: { id: EquipmentSlot; label: string }[] = [
    { id: 'helmet', label: 'Helmet' },
    { id: 'armor', label: 'Armor' },
    { id: 'weapon', label: 'Weapon' },
    { id: 'necklace', label: 'Necklace' },
    { id: 'ring', label: 'Ring' },
    { id: 'mount', label: 'Mount' },
  ];

  private readonly art = inject(KaeliArtService);

  private initTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
  ) {
    this.initTimer = setInterval(() => {
      if (this.selected()) { this.stopInit(); return; }
      const acc = this.api.account();
      const cat = this.api.catalog();
      if (acc && cat) {
        const requested = this.route.snapshot.queryParamMap.get('waifu');
        const waifu = cat.waifus.find((w) => w.id === requested)
          ?? cat.waifus.find((w) => w.id === acc.activeWaifuId)
          ?? cat.waifus[0]
          ?? null;
        if (waifu) this.select(waifu);
        this.stopInit();
      }
    }, 200);
  }

  private stopInit(): void {
    if (this.initTimer !== null) { clearInterval(this.initTimer); this.initTimer = null; }
  }

  ngOnDestroy(): void { this.stopInit(); }

  rarityColor(r: number): string { return RARITY_COLORS[r] ?? '#fff'; }
  elementLabel(e: string): string { return ELEMENT_LABELS[e] ?? e; }
  elementColor(el: string): string { return ELEMENT_PALETTE.has(el) ? `var(--el-${el})` : 'var(--accent)'; }

  // ---- authored art ----
  thumb(id: string): string | null { return this.art.thumb(id); }
  hasArt(id: string): boolean { return this.art.idles(id).length > 0; }
  bgPortrait(w: WaifuDef): string | null { return this.art.bgPortrait(w.id); }
  bgFallback(w: WaifuDef): string { return this.art.elementGradient(w.element); }

  select(w: WaifuDef): void {
    this.selected.set(w);
    this.tab.set('profile');
    this.infoSection.set('class');
    this.previewStanceId.set(this.initialStance(w)?.id ?? '');
    this.selectedEquipmentSlot.set(null);
  }

  unlockedLoreCount(w: WaifuDef): number {
    return w.lore.filter((_, i) => this.loreUnlocked(w.id, i)).length;
  }

  owned(id: string): boolean { return this.api.account()?.ownedWaifus.includes(id) ?? false; }
  ascension(id: string): number { return this.api.account()?.ascension?.[id] ?? 0; }
  shards(id: string): number { return this.api.account()?.shards?.[id] ?? 0; }

  ascCost(id: string): number {
    const costs = this.api.catalog()?.ascensionShardCost ?? [];
    return costs[this.ascension(id)] ?? 9999;
  }

  classFor(w: WaifuDef): ClassDef | undefined {
    return this.api.catalog()?.classes.find((c) => c.id === w.classId);
  }

  // ---- skins ----

  skinFor(w: WaifuDef): SkinDef {
    const selectedId = this.api.account()?.selectedSkins?.[w.id];
    return w.skins.find((s) => s.id === selectedId) ?? w.skins[0];
  }

  skinUnlocked(w: WaifuDef, skin: SkinDef): boolean {
    if (skin.unlock === 'default') return true;
    if (skin.unlock === 'affinity') return this.affinityLevel(w.id) >= skin.unlockValue;
    return this.api.account()?.ownedSkins?.includes(skin.id) ?? false;
  }

  isSelectedSkin(w: WaifuDef, skin: SkinDef): boolean { return this.skinFor(w).id === skin.id; }

  skinBadge(skin: SkinDef): string {
    switch (skin.unlock) {
      case 'default': return 'Default';
      case 'affinity': return `Affinity ${skin.unlockValue}`;
      case 'gold': return `${skin.unlockValue} gold`;
      default: return `${skin.unlockValue} Kaeros`;
    }
  }

  canAfford(skin: SkinDef): boolean {
    const acc = this.api.account();
    if (!acc) return false;
    return skin.unlock === 'gold' ? acc.gold >= skin.unlockValue : acc.kaeros >= skin.unlockValue;
  }

  // ---- affinity / gifts ----

  affinityMax(): number { return this.api.catalog()?.affinity.maxLevel ?? 10; }
  favoriteMultiplier(): number { return this.api.catalog()?.affinity.giftFavoriteMultiplier ?? 2; }
  affinityLevel(id: string): number { return this.api.account()?.affinity?.[id]?.level ?? 1; }
  affinityInto(id: string): number { return this.api.account()?.affinity?.[id]?.xpIntoLevel ?? 0; }
  affinityToNext(id: string): number {
    return this.api.account()?.affinity?.[id]?.xpToNext
      ?? this.api.catalog()?.affinity.xpPerLevel?.[0] ?? 0;
  }
  affinityPercent(id: string): number {
    const toNext = this.affinityToNext(id);
    return toNext <= 0 ? 100 : Math.min((this.affinityInto(id) / toNext) * 100, 100);
  }

  loreLevelFor(index: number): number { return this.api.catalog()?.affinity.loreLevels?.[index] ?? 99; }
  loreUnlocked(id: string, index: number): boolean { return this.affinityLevel(id) >= this.loreLevelFor(index); }

  giftsLeft(id: string): number {
    const perDay = this.api.catalog()?.affinity.giftsPerDay ?? 3;
    return Math.max(perDay - (this.api.account()?.giftsToday?.[id] ?? 0), 0);
  }

  isFavorite(w: WaifuDef, itemId: number): boolean { return w.favoriteGiftItemIds.includes(itemId); }

  itemName(itemId: number): string { return this.itemById(itemId)?.name ?? `item ${itemId}`; }

  giftCandidates(): ItemCatalogEntry[] {
    const w = this.selected();
    const inventory = this.api.account()?.inventory ?? [];
    return inventory
      .map((stack) => this.itemById(stack.itemId))
      .filter((item): item is ItemCatalogEntry => !!item)
      .sort((a, b) => {
        const favA = w ? +this.isFavorite(w, a.itemId) : 0;
        const favB = w ? +this.isFavorite(w, b.itemId) : 0;
        return favB - favA || b.salePrice - a.salePrice;
      });
  }

  giftXpFor(w: WaifuDef, item: ItemCatalogEntry): number {
    const cfg = this.api.catalog()?.affinity;
    if (!cfg) return 0;
    const xp = (cfg.giftBaseXp + item.salePrice * cfg.giftXpPerGold)
      * (this.isFavorite(w, item.itemId) ? cfg.giftFavoriteMultiplier : 1);
    return Math.floor(Math.min(xp, cfg.giftXpCap));
  }

  // ---- mastery ----

  masteryOf(id: string): MasteryState {
    return this.api.account()?.mastery?.[id] ?? { points: 0, spent: 0, nodes: [] };
  }
  respecGold(): number { return this.api.catalog()?.mastery.respecGold ?? 1000; }
  pointsPerVictory(): number { return this.api.catalog()?.mastery.pointsPerVictory ?? 3; }
  pointsPerDefeat(): number { return this.api.catalog()?.mastery.pointsPerDefeat ?? 1; }

  branchNodes(id: string, branch: 'off' | 'def' | 'eco'): MasteryNodeDef[] {
    return (this.api.catalog()?.masteryTrees?.[id] ?? [])
      .filter((n) => n.branch === branch)
      .sort((a, b) => a.order - b.order);
  }

  nodeUnlocked(id: string, node: MasteryNodeDef): boolean {
    return this.masteryOf(id).nodes.includes(node.id);
  }

  nodeAvailable(id: string, node: MasteryNodeDef): boolean {
    const mastery = this.masteryOf(id);
    if (mastery.nodes.includes(node.id) || mastery.points < node.cost) return false;
    if (node.order === 1) return true;
    const prev = this.branchNodes(id, node.branch).find((n) => n.order === node.order - 1);
    return !!prev && mastery.nodes.includes(prev.id);
  }

  nodeBlockReason(id: string, node: MasteryNodeDef): string {
    const mastery = this.masteryOf(id);
    if (node.order > 1) {
      const prev = this.branchNodes(id, node.branch).find((n) => n.order === node.order - 1);
      if (prev && !mastery.nodes.includes(prev.id)) return 'Requires previous node';
    }
    return `${node.cost - mastery.points} pt missing`;
  }

  // ---- equipment ----

  equipmentTotals(waifuId: string): { label: string; value: string }[] {
    const slots: EquipmentSlot[] = ['helmet', 'armor', 'weapon', 'necklace', 'ring', 'mount'];
    let atk = 0, arm = 0, def = 0, crit = 0;
    for (const slot of slots) {
      const item = this.equippedItem(waifuId, slot);
      if (item) {
        atk += item.attack ?? 0;
        arm += item.armor ?? 0;
        def += item.defense ?? 0;
        crit += item.critChance ?? 0;
      }
    }
    const result: { label: string; value: string }[] = [];
    if (atk) result.push({ label: 'ATK', value: `+${atk}` });
    if (arm) result.push({ label: 'ARM', value: `+${arm}` });
    if (def) result.push({ label: 'DEF', value: `+${def}` });
    if (crit) result.push({ label: 'CRIT', value: `+${Math.round(crit * 100)}%` });
    return result;
  }

  initialStance(w: WaifuDef): ClassStanceDef | undefined {
    const cls = this.classFor(w);
    return cls?.stances.find((s) => s.element === w.element)
      ?? cls?.stances.find((s) => s.id === cls.defaultStanceId)
      ?? cls?.stances[0];
  }

  previewStance(w: WaifuDef): ClassStanceDef | undefined {
    const cls = this.classFor(w);
    return cls?.stances.find((s) => s.id === this.previewStanceId()) ?? this.initialStance(w);
  }

  kit(w: WaifuDef): SkillDef[] {
    const skills = this.api.catalog()?.skills ?? [];
    const stance = this.previewStance(w);
    if (!stance) return [];
    return [...stance.slots, stance.ultimate]
      .map((id) => skills.find((s) => s.id === id))
      .filter((s): s is SkillDef => !!s);
  }

  itemById(itemId: number): ItemCatalogEntry | undefined {
    return this.api.catalog()?.items.find((item) => item.itemId === itemId);
  }

  selectTier(tier: number): void {
    this.selectedTier.set(tier);
    this.selectedEquipmentSlot.set(null);
  }

  /** G-09: Echo material by tier (read from account inventory; synthetic ids outside the catalog). */
  gearMaterials(): { tier: number; count: number }[] {
    return (this.api.account()?.inventory ?? [])
      .filter((stack) => isGearMaterial(stack.itemId))
      .map((stack) => ({ tier: stack.itemId - GEAR_MATERIAL_ID_BASE, count: stack.count }))
      .sort((a, b) => a.tier - b.tier);
  }

  equippedItem(waifuId: string, slot: EquipmentSlot): ItemCatalogEntry | undefined {
    const itemId = this.api.account()?.equipment?.[equipKey(waifuId, this.selectedTier())]?.[slot];
    return itemId === undefined ? undefined : this.itemById(itemId);
  }

  equipmentCandidates(waifuId: string, slot: EquipmentSlot): ItemCatalogEntry[] {
    const inventory = this.api.account()?.inventory ?? [];
    const tier = this.selectedTier();
    return inventory
      .map((stack) => this.itemById(stack.itemId))
      .filter((item): item is ItemCatalogEntry =>
        item?.slot === slot && (item.tier === 0 || item.tier === tier))
      .sort((a, b) =>
        Number(this.canEquipById(waifuId, b)) - Number(this.canEquipById(waifuId, a))
        || (b.attack + b.armor + b.defense + b.mountSpeed)
           - (a.attack + a.armor + a.defense + a.mountSpeed));
  }

  canEquip(waifu: WaifuDef, item: ItemCatalogEntry): boolean {
    return !this.itemRequirement(waifu, item);
  }

  itemRequirement(waifu: WaifuDef, item: ItemCatalogEntry): string {
    if (item.allowedClassIds.length && !item.allowedClassIds.includes(waifu.classId))
      return `Restricted to ${item.allowedClassIds.join(', ')}`;
    const mastery = this.api.account()?.mastery?.[waifu.id];
    const total = (mastery?.points ?? 0) + (mastery?.spent ?? 0);
    if (total < item.requiredMasteryPoints)
      return `Requires ${item.requiredMasteryPoints} mastery points`;
    return '';
  }

  private canEquipById(waifuId: string, item: ItemCatalogEntry): boolean {
    const waifu = this.allWaifus().find((entry) => entry.id === waifuId);
    return !!waifu && this.canEquip(waifu, item);
  }

  mountLookType(waifuId: string): number {
    return this.equippedItem(waifuId, 'mount')?.mountLookType ?? 0;
  }

  slotLabel(slot: EquipmentSlot): string {
    return this.equipmentSlots.find((entry) => entry.id === slot)?.label ?? slot;
  }

  itemStats(item: ItemCatalogEntry): string {
    return [
      item.attack ? `ATK ${item.attack}` : '',
      item.armor ? `ARM ${item.armor}` : '',
      item.defense ? `DEF ${item.defense}` : '',
      item.mountSpeed ? `SPD ${item.mountSpeed}` : '',
      item.elementDamage ? `${item.element.toUpperCase()} +${item.elementDamage}` : '',
      item.critChance ? `CRIT +${Math.round(item.critChance * 100)}%` : '',
      item.physicalResistance ? `PHY RES ${Math.round(item.physicalResistance * 100)}%` : '',
    ].filter(Boolean).join(' · ') || 'equippable';
  }

  // ---- actions ----

  async gift(waifuId: string, itemId: number): Promise<void> {
    this.busy.set(true);
    try {
      const res = await this.api.giftItem(waifuId, itemId);
      if (res.notes?.length) alert(res.notes.join('\n'));
    } catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async selectSkin(waifuId: string, skinId: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.selectSkin(waifuId, skinId); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async buySkin(waifuId: string, skinId: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.buySkin(waifuId, skinId); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async unlockNode(waifuId: string, nodeId: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.unlockMasteryNode(waifuId, nodeId); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async respec(waifuId: string): Promise<void> {
    if (!confirm(`Reset mastery for ${this.respecGold()} gold?`)) return;
    this.busy.set(true);
    try { await this.api.respecMastery(waifuId); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async autoEquip(waifuId: string): Promise<void> {
    const w = this.allWaifus().find((entry) => entry.id === waifuId);
    if (!w) return;
    this.busy.set(true);
    try {
      for (const slot of this.equipmentSlots) {
        const candidates = this.equipmentCandidates(waifuId, slot.id);
        const best = candidates.find((item) => this.canEquip(w, item));
        if (!best) continue;
        const current = this.equippedItem(waifuId, slot.id);
        if (current?.itemId === best.itemId) continue;
        await this.api.equipItem(waifuId, slot.id, best.itemId, this.selectedTier());
      }
    } catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async equip(waifuId: string, slot: EquipmentSlot, itemId: number): Promise<void> {
    this.busy.set(true);
    try { await this.api.equipItem(waifuId, slot, itemId, this.selectedTier()); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async unequip(waifuId: string, slot: EquipmentSlot): Promise<void> {
    this.busy.set(true);
    try { await this.api.unequipItem(waifuId, slot, this.selectedTier()); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async ascend(id: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.ascend(id); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

}

const ELEMENT_PALETTE = new Set([
  'physical', 'fire', 'ice', 'energy', 'earth', 'death', 'holy',
]);
