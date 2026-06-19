import { Component, computed, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { KaeliArtService } from '../../core/kaeli-art.service';
import { ItemIcon } from '../../core/item-icon';
import { OutfitPreview } from '../../core/outfit-preview';
import { KaeliIdle } from '../../core/ui/kaeli-idle';
import { RarityStars } from '../../core/ui/rarity-stars';
import {
  ClassDef, ClassStanceDef, ELEMENT_LABELS, EquipmentSlot, ItemCatalogEntry,
  MasteryNodeDef, MasteryState, RARITY_COLORS, SET_TIERS, SkillDef, SkinDef, WEAPON_LABELS, WaifuDef, equipKey,
} from '../../core/types';

type KaeliTab = 'perfil' | 'skins' | 'maestria' | 'equipamento' | 'informacao';

@Component({
  selector: 'app-kaelis',
  standalone: true,
  imports: [OutfitPreview, ItemIcon, KaeliIdle, RarityStars],
  template: `
    <div class="atelier">

      <!-- ── ROSTER RAIL ── -->
      <nav class="roster" aria-label="Selecionar Kaeli">
        @for (w of allWaifus(); track w.id) {
          <button class="roster-item" [class.owned]="owned(w.id)" [class.active]="selected()?.id === w.id"
                  [style.--rc]="rarityColor(w.rarity)" [title]="w.name" (click)="select(w)">
            <span class="bust">
              @if (thumb(w.id); as t) {
                <img [src]="t" alt="" decoding="async" />
              } @else {
                <app-outfit-preview [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
                  [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
                  [addons]="skinFor(w).addons ?? 0" [size]="44" [animate]="false" />
              }
              @if (!owned(w.id)) { <span class="lock">🔒</span> }
            </span>
            <span class="bust-name">{{ w.name }}</span>
            <rarity-stars [rarity]="w.rarity" [size]="9" />
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

          <button class="skin-fab glass" title="Trocar skin" (click)="tab.set('skins')">👕</button>

          <div class="identity">
            <div class="id-tags">
              <span class="el-tag" [style.--el]="elementColor(w.element)">{{ elementLabel(w.element) }}</span>
              <rarity-stars [rarity]="w.rarity" [size]="16" />
            </div>
            <h1 class="id-name">{{ w.name }}</h1>
            <p class="id-title">{{ w.title }}</p>
            <div class="id-class">
              <span class="chip">{{ classFor(w)?.name }}</span>
              <span class="chip">{{ weaponLabel(w.weapon) }}</span>
            </div>
          </div>
        } @else {
          <div class="stage-empty">
            <p class="muted">Selecione uma Kaeli na barra lateral.</p>
          </div>
        }
      </div>

      <!-- ── DOSSIER ── -->
      <div class="dossier">
        @if (selected(); as w) {
          @if (owned(w.id)) {
            <!-- stat ribbon -->
            <div class="ribbon">
              <div class="rib-stat"><span>ATK</span><b>{{ w.baseAtk }}</b></div>
              <div class="rib-stat"><span>HP</span><b>{{ w.baseHp }}</b></div>
              <div class="rib-stat"><span>Afinidade</span><b>{{ affinityLevel(w.id) }}</b></div>
              <div class="rib-stat gold"><span>Ascensão</span><b>A{{ ascension(w.id) }}</b></div>
            </div>

            <div class="tabs">
              @for (t of tabs; track t.id) {
                <button class="tab" [class.active]="tab() === t.id" (click)="tab.set(t.id)">{{ t.label }}</button>
              }
            </div>

            <!-- ═══ PERFIL ═══ -->
            @if (tab() === 'perfil') {
              <div class="tab-content">
                <div class="trait-card glass">
                  <span class="eyebrow trait-label">Trait</span>
                  <b>{{ w.trait.name }}</b>
                  <p>{{ w.trait.description }}</p>
                </div>
                <p class="personality">「 {{ w.personality }} 」</p>

                <div class="overview-stats">
                  <div class="ov-stat">
                    <span class="ov-label">Afinidade</span>
                    <span class="ov-val">{{ affinityLevel(w.id) }} / {{ affinityMax() }}</span>
                  </div>
                  <div class="ov-stat">
                    <span class="ov-label">Ascensão</span>
                    <span class="ov-val">A{{ ascension(w.id) }} / 6</span>
                  </div>
                  <div class="ov-stat">
                    <span class="ov-label">Pts Maestria</span>
                    <span class="ov-val">{{ masteryOf(w.id).points }} livres</span>
                  </div>
                  <div class="ov-stat">
                    <span class="ov-label">Shards</span>
                    <span class="ov-val">{{ shards(w.id) }}</span>
                  </div>
                </div>

                <div class="asc-row">
                  <div class="asc-dots">
                    @for (i of [1,2,3,4,5,6]; track i) {
                      <span class="dot" [class.on]="ascension(w.id) >= i">●</span>
                    }
                  </div>
                  @if (ascension(w.id) < 6) {
                    <button class="btn gold compact" [disabled]="busy() || shards(w.id) < ascCost(w.id)" (click)="ascend(w.id)">
                      Ascender — {{ ascCost(w.id) }} shards
                    </button>
                  } @else {
                    <span class="maxed">Ascensão máxima!</span>
                  }
                </div>
              </div>
            }

            <!-- ═══ MAESTRIA ═══ -->
            @if (tab() === 'maestria') {
              <div class="tab-content">
                <div class="mastery-header glass">
                  <div>
                    <h3 style="margin:0">Maestria de Eco</h3>
                    <span class="muted small">Vitória +{{ pointsPerVictory() }} pt · derrota +{{ pointsPerDefeat() }} pt</span>
                  </div>
                  <div class="mastery-pts-badge">{{ masteryOf(w.id).points }}<span>pts</span></div>
                  @if (masteryOf(w.id).spent > 0) {
                    <button class="btn secondary compact" [disabled]="busy()" (click)="respec(w.id)">
                      Resetar — {{ respecGold() }} ouro
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
                              <span class="node-done">destravado</span>
                            } @else {
                              <div class="node-actions">
                                <span class="node-cost">{{ node.cost }} pt</span>
                                <button class="btn compact mini"
                                        [disabled]="busy() || !nodeAvailable(w.id, node)"
                                        (click)="unlockNode(w.id, node.id)">
                                  {{ nodeAvailable(w.id, node) ? 'Destravar' : nodeBlockReason(w.id, node) }}
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

            <!-- ═══ EQUIPAMENTO ═══ -->
            @if (tab() === 'equipamento') {
              <div class="tab-content">
                <div class="tier-tabs">
                  <span class="tier-tabs-lbl">Set por tier</span>
                  @for (t of setTiers; track t) {
                    <button class="tier-tab" [class.active]="selectedTier() === t" (click)="selectTier(t)">T{{ t }}</button>
                  }
                  <span class="muted small tier-hint">A dungeon usa o set do seu tier. Itens são travados por tier.</span>
                </div>
                @if (equipmentTotals(w.id).length > 0) {
                  <div class="equip-summary glass">
                    <h4>Atributos do Equipamento</h4>
                    <div class="summary-row">
                      @for (stat of equipmentTotals(w.id); track stat.label) {
                        <div class="summary-stat">
                          <span>{{ stat.label }}</span>
                          <b>{{ stat.value }}</b>
                        </div>
                      }
                    </div>
                  </div>
                }
                <div class="paperdoll">
                  @for (slot of equipmentSlots; track slot.id) {
                    <button class="gear-slot" [class.active]="selectedEquipmentSlot() === slot.id"
                            (click)="selectedEquipmentSlot.set(slot.id)">
                      <span class="slot-name">{{ slot.label }}</span>
                      @if (equippedItem(w.id, slot.id); as item) {
                        <app-item-icon [itemId]="item.itemId" [size]="42" />
                        <b>{{ item.name }}</b>
                        <small>{{ itemStats(item) }}</small>
                      } @else {
                        <span class="empty-slot">vazio</span>
                      }
                    </button>
                  }
                </div>
                @if (selectedEquipmentSlot(); as slot) {
                  <div class="gear-picker glass">
                    <div class="picker-title">
                      <b>{{ slotLabel(slot) }}</b>
                      @if (equippedItem(w.id, slot)) {
                        <button class="btn secondary compact" [disabled]="busy()" (click)="unequip(w.id, slot)">Desequipar</button>
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
                        <span class="muted">Nenhum item deste slot na Mochila.</span>
                      }
                    </div>
                  </div>
                }
              </div>
            }

            <!-- ═══ INFORMAÇÃO ═══ -->
            @if (tab() === 'informacao') {
              <div class="tab-content">
                <!-- Afinidade -->
                <div class="info-section glass">
                  <h3 class="section-title">Afinidade <span class="aff-level-badge">{{ affinityLevel(w.id) }}</span>
                    <span class="muted small"> / {{ affinityMax() }} · +{{ affinityLevel(w.id) - 1 }}% ATK/HP na run</span>
                  </h3>
                  <div class="aff-bar"><div class="aff-fill" [style.width.%]="affinityPercent(w.id)"></div></div>
                  @if (affinityToNext(w.id) > 0) {
                    <span class="muted small">{{ affinityInto(w.id) }} / {{ affinityToNext(w.id) }} XP — jogue runs com ela ou dê presentes</span>
                  } @else {
                    <span class="maxed">Afinidade máxima!</span>
                  }
                </div>

                <!-- Presentes -->
                <div class="info-section glass">
                  <h3 class="section-title">Presentes <span class="muted small">· {{ giftsLeft(w.id) }} restante(s) hoje</span></h3>
                  <div class="fav-row">
                    <span class="muted small">Favoritos (XP ×{{ favoriteMultiplier() }}):</span>
                    @for (itemId of w.favoriteGiftItemIds; track itemId) {
                      <span class="fav-item" [title]="itemName(itemId)">
                        <app-item-icon [itemId]="itemId" [size]="28" />❤
                      </span>
                    }
                  </div>
                  @if (giftsLeft(w.id) > 0) {
                    <div class="gift-options">
                      @for (item of giftCandidates(); track item.itemId) {
                        <button class="gift-option" [class.fav]="isFavorite(w, item.itemId)"
                                [disabled]="busy()" (click)="gift(w.id, item.itemId)">
                          <app-item-icon [itemId]="item.itemId" [size]="32" />
                          <span>
                            <b>{{ item.name }}</b>
                            <small>+{{ giftXpFor(w, item) }} XP{{ isFavorite(w, item.itemId) ? ' ❤' : '' }}</small>
                          </span>
                        </button>
                      } @empty {
                        <span class="muted">Nenhum item na Mochila para presentear. Traga loot das runs!</span>
                      }
                    </div>
                  } @else {
                    <span class="muted">{{ w.name }} já ganhou presentes demais hoje. Volte amanhã!</span>
                  }
                </div>

                <!-- Ecos de Memória -->
                <div class="info-section glass">
                  <h3 class="section-title">Ecos de Memória</h3>
                  @for (fragment of w.lore; track $index) {
                    @if (loreUnlocked(w.id, $index)) {
                      <div class="lore-entry">
                        <span class="lore-num">Eco {{ $index + 1 }}</span>
                        <p>{{ fragment }}</p>
                      </div>
                    } @else {
                      <div class="lore-entry locked">
                        <span class="lore-num">Eco {{ $index + 1 }}</span>
                        <p>🔒 Desbloqueia na afinidade {{ loreLevelFor($index) }}.</p>
                      </div>
                    }
                  }
                </div>

                <!-- Kit de Classe -->
                @if (classFor(w); as cls) {
                  <div class="info-section glass">
                    <h3 class="section-title">{{ cls.name }} <span class="muted small">· kit de classe</span></h3>
                    <p class="muted small" style="margin:0 0 12px">{{ cls.description }}</p>
                    <div class="stances">
                      @for (stance of cls.stances; track stance.id) {
                        <button class="stance-tab" [class.active]="previewStanceId() === stance.id"
                                (click)="previewStanceId.set(stance.id)">
                          {{ elementLabel(stance.element) }}
                        </button>
                      }
                    </div>
                    @for (s of kit(w); track s.id; let i = $index) {
                      <div class="skill">
                        <span class="key">{{ ['1','2','3','4','R'][i] }}</span>
                        <div>
                          <b>{{ s.name }}</b>
                          <span class="element-name">{{ elementLabel(s.element) }}</span>
                          <span class="muted small">{{ i === 4 ? '(Ultimate · gauge)' : s.cooldownMs / 1000 + 's' }}</span>
                          <p>{{ s.description }}</p>
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            }

            <!-- ═══ SKINS ═══ -->
            @if (tab() === 'skins') {
              <div class="tab-content">
                <div class="skins-grid">
                  @for (skin of w.skins; track skin.id) {
                    <div class="skin-card glass" [class.selected]="isSelectedSkin(w, skin)"
                         [class.locked]="!skinUnlocked(w, skin)">
                      <app-outfit-preview [lookType]="skin.lookType" [head]="skin.head" [body]="skin.body"
                        [legs]="skin.legs" [feet]="skin.feet" [addons]="skin.addons ?? 0"
                        [mountLookType]="skin.mountLookType ?? 0" [size]="96" />
                      <b>{{ skin.name }}</b>
                      <p class="skin-desc">{{ skin.description }}</p>
                      <span class="skin-badge">{{ skinBadge(skin) }}</span>
                      @if (isSelectedSkin(w, skin)) {
                        <span class="skin-active">EM USO</span>
                      } @else if (skinUnlocked(w, skin)) {
                        <button class="btn compact" [disabled]="busy()" (click)="selectSkin(w.id, skin.id)">Usar</button>
                      } @else if (skin.unlock === 'gold' || skin.unlock === 'kaeros') {
                        <button class="btn gold compact" [disabled]="busy() || !canAfford(skin)"
                                (click)="buySkin(w.id, skin.id)">
                          Comprar — {{ skin.unlockValue }} {{ skin.unlock === 'gold' ? 'ouro' : 'Kaeros' }}
                        </button>
                      } @else {
                        <span class="muted small">Afinidade {{ skin.unlockValue }} desbloqueia</span>
                      }
                    </div>
                  }
                </div>
                <p class="muted small" style="margin-top:12px">A skin em uso aparece no Hub, nas runs e nesta página. Os addons exibidos são os definidos na skin.</p>
              </div>
            }

          } @else {
            <!-- não recrutada -->
            <div class="not-owned">
              <span class="eyebrow">Ainda não recrutada</span>
              <p class="muted">{{ selected()?.name }} aguarda no banner. Tente a sorte no recrutamento.</p>
              <div class="trait-card glass">
                <span class="eyebrow trait-label">Trait</span>
                <b>{{ w.trait.name }}</b>
                <p>{{ w.trait.description }}</p>
              </div>
            </div>
          }
        } @else {
          <p class="muted" style="padding:24px">Carregando…</p>
        }
      </div>
    </div>
  `,
  styles: [`
    /* local accent mixes (deduped) */
    :host { display: block; }
    .atelier {
      display: grid; grid-template-columns: 96px minmax(300px, 30%) 1fr;
      height: calc(100dvh - 53px); background: var(--bg-1);
      --af: color-mix(in srgb, var(--accent) 12%, var(--bg-2));
      --ae: color-mix(in srgb, var(--accent) 38%, transparent);
    }

    /* roster */
    .roster { display: flex; flex-direction: column; gap: var(--sp-2); padding: var(--sp-3) var(--sp-2); overflow-y: auto; background: var(--bg-0); border-right: 1px solid var(--line); }
    .roster::-webkit-scrollbar { width: 4px; }
    .roster-item { position: relative; display: flex; flex-direction: column; align-items: center; gap: 3px; padding: var(--sp-2) 4px; color: var(--text); border-radius: var(--r-md); transition: all var(--dur) var(--ease-out); }
    .roster-item.owned { border-color: color-mix(in srgb, var(--rc) 45%, transparent); }
    .roster-item:not(.owned) { filter: grayscale(0.85) brightness(0.5); }
    .roster-item:hover { transform: translateX(2px); border-color: var(--rc); }
    .roster-item.active { background: var(--bg-3); border-color: var(--accent); box-shadow: var(--glass-edge), 0 0 0 1px var(--accent); }
    .bust { position: relative; width: 46px; height: 46px; display: flex; align-items: center; justify-content: center; }
    .bust img { width: 100%; height: 100%; object-fit: cover; border-radius: var(--r-sm); }
    .lock { position: absolute; top: -2px; right: -2px; font-size: 10px; }
    .bust-name { font-size: 9.5px; font-weight: 700; text-align: center; line-height: 1.1; color: var(--text-dim); }

    /* stage / art alcove */
    .stage { position: relative; overflow: hidden; isolation: isolate; }
    .bg { position: absolute; inset: 0; z-index: -2; }
    .bg-img { width: 100%; height: 100%; object-fit: cover; object-position: center top; }
    .bg-img.gradient { object-position: center; }
    .vignette { position: absolute; inset: 0; z-index: -1; pointer-events: none; background: linear-gradient(0deg, rgba(7,7,13,0.96) 2%, rgba(7,7,13,0.15) 45%, rgba(7,7,13,0.42) 100%); }
    .floor { position: absolute; left: 50%; bottom: 9%; z-index: -1; transform: translateX(-50%); width: 64%; height: 64px; border-radius: 50%; pointer-events: none; background: radial-gradient(ellipse at center, color-mix(in srgb, var(--el) 55%, transparent), transparent 70%); filter: blur(10px); opacity: 0.7; }
    .figure { position: absolute; inset: 0; padding-bottom: 4%; filter: drop-shadow(0 14px 34px rgba(0,0,0,0.55)); }
    .sprite-stand { position: absolute; inset: 0; display: flex; align-items: flex-end; justify-content: center; padding-bottom: 8%; filter: drop-shadow(0 18px 40px rgba(0,0,0,0.6)); }
    .skin-fab { position: absolute; top: var(--sp-4); right: var(--sp-4); z-index: 3; width: 40px; height: 40px; border-radius: var(--r-md); font-size: 18px; display: flex; align-items: center; justify-content: center; color: var(--text); transition: all var(--dur) var(--ease-out); }
    .skin-fab:hover { transform: translateY(-1px); border-color: var(--accent); }
    .identity { position: absolute; left: clamp(16px, 4%, 32px); right: 16px; bottom: clamp(18px, 4vh, 32px); z-index: 2; }
    .id-tags { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; }
    .el-tag { font-size: var(--fs-xs); font-weight: 700; text-transform: uppercase; letter-spacing: var(--tracking-eyebrow); color: var(--el); padding: 4px 11px; border-radius: var(--r-full); border: 1px solid color-mix(in srgb, var(--el) 50%, transparent); background: color-mix(in srgb, var(--el) 14%, transparent); }
    .id-name { font-family: var(--font-display); font-weight: 900; font-size: clamp(1.9rem, 4vw, 2.9rem); line-height: 0.96; margin: 0; letter-spacing: -0.01em; text-shadow: 0 4px 30px rgba(0,0,0,0.7); }
    .id-title { font-family: var(--font-display); font-style: italic; color: var(--accent-bright); font-size: 1.1rem; margin: 4px 0 10px; }
    .id-class { display: flex; gap: 6px; flex-wrap: wrap; }
    .id-class .chip { font-size: var(--fs-xs); font-weight: 700; color: var(--text-dim); background: var(--glass-bg); border: 1px solid var(--line-strong); border-radius: var(--r-full); padding: 3px 10px; }
    .stage-empty { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; text-align: center; padding: var(--sp-5); }

    /* dossier */
    .dossier { overflow-y: auto; padding: var(--sp-5) var(--sp-5) var(--sp-7); }
    .ribbon { display: grid; grid-template-columns: repeat(4, 1fr); gap: var(--sp-2); margin-bottom: var(--sp-4); }
    .rib-stat { display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 10px 8px; }
    .rib-stat span { font-size: var(--fs-xs); color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.06em; }
    .rib-stat b { font-family: var(--font-display); font-size: 1.25rem; }
    .rib-stat.gold b { color: var(--gold-bright); }
    .tabs { position: sticky; top: calc(-1 * var(--sp-5)); z-index: 4; display: flex; gap: 2px; flex-wrap: wrap; padding: var(--sp-2) 0; margin-bottom: var(--sp-4); border-bottom: 1px solid var(--line); background: linear-gradient(180deg, var(--bg-1) 70%, transparent); }
    .tab { background: none; border: none; border-bottom: 2px solid transparent; color: var(--text-mute); padding: 8px 14px; font-size: var(--fs-sm); font-weight: 700; transition: all var(--dur) var(--ease-out); }
    .tab.active { color: var(--accent-bright); border-bottom-color: var(--accent); }
    .tab:hover:not(.active) { color: var(--text-dim); }
    .tab-content { display: flex; flex-direction: column; gap: var(--sp-4); }

    /* shared inset surfaces */
    .roster-item, .rib-stat, .ov-stat, .node-info, .gear-slot, .gear-option, .gift-option { background: var(--bg-2); border: 1px solid var(--line); }
    .rib-stat, .ov-stat { border-radius: var(--r-md); }

    /* perfil */
    .trait-card { padding: 12px 14px; }
    .trait-label { color: var(--accent-bright); display: block; margin-bottom: 4px; }
    .trait-card b { color: var(--text); }
    .trait-card p { margin: 4px 0 0; color: var(--text-dim); font-size: var(--fs-sm); }
    .personality { color: var(--text-mute); font-style: italic; margin: 0; font-size: var(--fs-sm); }
    .overview-stats { display: grid; grid-template-columns: 1fr 1fr; gap: var(--sp-2); }
    .ov-stat { padding: 10px 14px; display: flex; justify-content: space-between; align-items: center; }
    .ov-label { font-size: var(--fs-sm); color: var(--text-mute); }
    .ov-val { font-size: var(--fs-sm); font-weight: 700; }
    .asc-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .asc-dots { display: flex; gap: 5px; }
    .dot { color: var(--line-strong); font-size: 18px; }
    .dot.on { color: var(--gold); }
    .maxed { color: var(--gold-bright); font-weight: 700; }

    /* maestria */
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
    .node-info { flex: 1; border-radius: var(--r-sm); padding: 8px 10px; }
    .tree-node.unlocked .node-info { border-color: var(--ae); background: var(--af); }
    .tree-node.available .node-info { border-color: var(--ae); }
    .node-info b { font-size: 12px; display: block; margin-bottom: 3px; }
    .node-info p { margin: 0 0 6px; color: var(--text-dim); font-size: 11px; line-height: 1.4; }
    .node-done { color: var(--accent-bright); font-size: 11px; font-weight: 800; }
    .node-actions { display: flex; align-items: center; gap: 8px; }
    .node-cost { color: var(--gold-bright); font-size: 11px; font-weight: 800; }

    /* equipamento */
    .tier-tabs { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .tier-tabs-lbl { font-size: var(--fs-xs); font-weight: 800; color: var(--accent-bright); text-transform: uppercase; letter-spacing: var(--tracking-eyebrow); margin-right: 4px; }
    .tier-tab, .stance-tab { border: 1px solid var(--line-strong); border-radius: var(--r-sm); background: var(--bg-2); color: var(--text-dim); padding: 5px 12px; font-size: 12px; font-weight: 800; }
    .tier-tab.active { border-color: var(--gold); color: var(--gold-bright); background: color-mix(in srgb, var(--gold) 10%, var(--bg-2)); }
    .tier-hint { flex-basis: 100%; margin-top: 2px; }
    .equip-summary { padding: 12px 16px; }
    .equip-summary h4 { margin: 0 0 10px; font-size: 13px; color: var(--accent-bright); font-weight: 800; text-transform: uppercase; letter-spacing: 0.05em; }
    .summary-row { display: flex; gap: var(--sp-4); flex-wrap: wrap; }
    .summary-stat { display: flex; flex-direction: column; align-items: center; gap: 2px; min-width: 60px; }
    .summary-stat span { font-size: 10px; color: var(--text-mute); text-transform: uppercase; letter-spacing: 0.05em; }
    .summary-stat b { font-family: var(--font-display); font-size: 18px; color: var(--accent-bright); }
    .paperdoll { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--sp-2); }
    .gear-slot, .gear-option, .gift-option { color: inherit; transition: all var(--dur) var(--ease-out); }
    .gear-slot { min-height: 100px; border-radius: var(--r-md); padding: 8px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 3px; }
    .gear-option, .gift-option { border-radius: var(--r-sm); padding: 6px 8px; display: flex; align-items: center; gap: 8px; text-align: left; }
    .gear-slot:hover, .gear-slot.active, .gear-option:not([disabled]):hover, .gift-option:not([disabled]):hover { border-color: var(--accent); }
    .gear-slot.active { background: var(--af); }
    .gear-slot b { font-size: 11px; text-align: center; }
    .gear-slot small { color: var(--text-dim); font-size: 10px; text-align: center; }
    .slot-name { color: var(--accent-bright); font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .empty-slot { color: var(--text-faint); font-size: 12px; }
    .gear-picker { padding: 12px; }
    .picker-title { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
    .gear-options { display: flex; flex-wrap: wrap; gap: var(--sp-2); }
    .gear-option span, .gift-option span { display: flex; flex-direction: column; }
    .gear-option small { color: var(--text-dim); font-size: 10px; }
    .req-locked { color: var(--danger) !important; }

    /* informação */
    .info-section { padding: 14px 16px; }
    .section-title { margin: 0 0 10px; font-size: var(--fs-h3); }
    .aff-level-badge { color: var(--accent-bright); font-size: 16px; font-weight: 800; margin-left: 6px; }
    .aff-bar { height: 8px; background: var(--bg-3); border-radius: var(--r-full); overflow: hidden; margin: 8px 0 6px; }
    .aff-fill { height: 100%; background: linear-gradient(90deg, var(--accent-bright), var(--accent-dim)); transition: width var(--dur-slow) var(--ease-out); }
    .fav-row { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; flex-wrap: wrap; }
    .fav-item { display: inline-flex; align-items: center; gap: 2px; background: var(--af); border-radius: var(--r-sm); padding: 2px 6px; font-size: 11px; }
    .gift-options { display: flex; flex-wrap: wrap; gap: var(--sp-2); max-height: 220px; overflow-y: auto; }
    .gift-option.fav { border-color: color-mix(in srgb, var(--gold) 55%, transparent); }
    .gift-option small { color: var(--gold-bright); font-size: 10px; }
    .lore-entry { display: flex; gap: 12px; margin-bottom: 10px; }
    .lore-entry:last-child { margin-bottom: 0; }
    .lore-entry p { margin: 0; color: var(--text-dim); font-size: var(--fs-sm); line-height: 1.55; }
    .lore-entry.locked p { color: var(--text-faint); }
    .lore-num { color: var(--accent-bright); font-size: 10px; font-weight: 800; white-space: nowrap; padding-top: 3px; }
    .stances { display: flex; gap: 6px; margin-bottom: 12px; flex-wrap: wrap; }
    .stance-tab { padding: 5px 10px; }
    .stance-tab.active { border-color: var(--accent); color: var(--accent-bright); background: var(--af); }
    .skill { display: flex; gap: 12px; margin-bottom: 10px; align-items: flex-start; }
    .skill:last-child { margin-bottom: 0; }
    .skill .key { background: var(--bg-3); border: 1px solid var(--line-strong); border-radius: var(--r-sm); width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-weight: 800; flex-shrink: 0; font-size: 12px; }
    .skill p { margin: 2px 0 0; color: var(--text-dim); font-size: 12px; }
    .element-name { margin-left: 8px; color: var(--accent-bright); font-size: 10px; font-weight: 800; text-transform: uppercase; }

    /* skins */
    .skins-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(155px, 1fr)); gap: var(--sp-3); }
    .skin-card { padding: 12px; display: flex; flex-direction: column; align-items: center; gap: 6px; text-align: center; transition: all var(--dur) var(--ease-out); }
    .skin-card:hover { transform: translateY(-2px); }
    .skin-card.selected { border-color: var(--accent); box-shadow: var(--glass-edge), var(--sh-accent); }
    .skin-card.locked { opacity: 0.7; }
    .skin-desc { color: var(--text-dim); font-size: 11px; margin: 0; line-height: 1.4; }
    .skin-badge { color: var(--gold-bright); font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .skin-active { color: var(--accent-bright); font-weight: 800; font-size: 12px; }

    .not-owned { display: flex; flex-direction: column; gap: var(--sp-3); max-width: 480px; }
    .btn.compact { padding: 7px 14px; font-size: 12px; }
    .btn.mini { padding: 4px 9px; font-size: 10px; border-radius: var(--r-sm); }
    .small { font-size: var(--fs-sm); }

    @media (max-width: 920px) {
      .atelier {
        grid-template-columns: 1fr;
        grid-template-rows: auto 42vh 1fr;
        height: auto; min-height: calc(100dvh - 53px);
      }
      .roster {
        flex-direction: row; overflow-x: auto; overflow-y: hidden;
        border-right: none; border-bottom: 1px solid var(--line);
      }
      .roster-item { flex: 0 0 64px; }
      .roster-item:hover { transform: none; }
      .stage { min-height: 42vh; }
      .id-name { font-size: clamp(1.8rem, 9vw, 2.6rem); }
      .mastery-tree { grid-template-columns: 1fr; }
      .tabs { top: 0; }
    }
  `],
})
export class KaelisPage {
  readonly allWaifus = computed(() => {
    const list = [...(this.api.catalog()?.waifus ?? [])];
    return list.sort((a, b) => b.rarity - a.rarity || a.name.localeCompare(b.name));
  });
  readonly selected = signal<WaifuDef | null>(null);
  readonly tab = signal<KaeliTab>('perfil');
  readonly previewStanceId = signal('');
  readonly selectedEquipmentSlot = signal<EquipmentSlot | null>(null);
  readonly selectedTier = signal(1);
  readonly setTiers = SET_TIERS;
  readonly busy = signal(false);
  readonly tabs: { id: KaeliTab; label: string }[] = [
    { id: 'perfil', label: 'Perfil' },
    { id: 'maestria', label: 'Maestria' },
    { id: 'equipamento', label: 'Equipamento' },
    { id: 'informacao', label: 'Informação' },
    { id: 'skins', label: 'Skins' },
  ];
  readonly branches: { id: 'off' | 'def' | 'eco'; label: string }[] = [
    { id: 'off', label: 'Ofensiva' },
    { id: 'def', label: 'Defensiva' },
    { id: 'eco', label: 'Eco' },
  ];
  readonly equipmentSlots: { id: EquipmentSlot; label: string }[] = [
    { id: 'helmet', label: 'Capacete' },
    { id: 'armor', label: 'Armadura' },
    { id: 'weapon', label: 'Arma' },
    { id: 'necklace', label: 'Colar' },
    { id: 'ring', label: 'Anel' },
    { id: 'mount', label: 'Montaria' },
  ];

  private readonly art = inject(KaeliArtService);

  constructor(private readonly api: ApiService) {
    const tryInit = setInterval(() => {
      if (this.selected()) { clearInterval(tryInit); return; }
      const acc = this.api.account();
      const cat = this.api.catalog();
      if (acc && cat) {
        const waifu = cat.waifus.find((w) => w.id === acc.activeWaifuId) ?? cat.waifus[0] ?? null;
        if (waifu) this.select(waifu);
        clearInterval(tryInit);
      }
    }, 200);
  }

  rarityColor(r: number): string { return RARITY_COLORS[r] ?? '#fff'; }
  elementLabel(e: string): string { return ELEMENT_LABELS[e] ?? e; }
  weaponLabel(w: string): string { return WEAPON_LABELS[w] ?? w; }
  elementColor(el: string): string { return ELEMENT_PALETTE.has(el) ? `var(--el-${el})` : 'var(--accent)'; }

  // ---- arte autoral ----
  thumb(id: string): string | null { return this.art.thumb(id); }
  hasArt(id: string): boolean { return this.art.idles(id).length > 0; }
  bgPortrait(w: WaifuDef): string | null { return this.art.bgPortrait(w.id); }
  bgFallback(w: WaifuDef): string { return this.art.elementGradient(w.element); }

  select(w: WaifuDef): void {
    this.selected.set(w);
    this.tab.set('perfil');
    this.previewStanceId.set(this.initialStance(w)?.id ?? '');
    this.selectedEquipmentSlot.set(null);
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
      case 'default': return 'Padrão';
      case 'affinity': return `Afinidade ${skin.unlockValue}`;
      case 'gold': return `${skin.unlockValue} ouro`;
      default: return `${skin.unlockValue} Kaeros`;
    }
  }

  canAfford(skin: SkinDef): boolean {
    const acc = this.api.account();
    if (!acc) return false;
    return skin.unlock === 'gold' ? acc.gold >= skin.unlockValue : acc.kaeros >= skin.unlockValue;
  }

  // ---- afinidade / presentes ----

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

  // ---- maestria ----

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
      if (prev && !mastery.nodes.includes(prev.id)) return 'Requer node anterior';
    }
    return `Faltam ${node.cost - mastery.points} pt`;
  }

  // ---- equipamento ----

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
      return `Restrito a ${item.allowedClassIds.join(', ')}`;
    const mastery = this.api.account()?.mastery?.[waifu.id];
    const total = (mastery?.points ?? 0) + (mastery?.spent ?? 0);
    if (total < item.requiredMasteryPoints)
      return `Requer ${item.requiredMasteryPoints} pontos de maestria`;
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
      item.mountSpeed ? `VEL ${item.mountSpeed}` : '',
      item.elementDamage ? `${item.element.toUpperCase()} +${item.elementDamage}` : '',
      item.critChance ? `CRIT +${Math.round(item.critChance * 100)}%` : '',
      item.physicalResistance ? `RES FIS ${Math.round(item.physicalResistance * 100)}%` : '',
    ].filter(Boolean).join(' · ') || 'equipável';
  }

  // ---- ações ----

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
    if (!confirm(`Resetar a maestria por ${this.respecGold()} de ouro?`)) return;
    this.busy.set(true);
    try { await this.api.respecMastery(waifuId); }
    catch (e) { alert((e as Error).message); }
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
