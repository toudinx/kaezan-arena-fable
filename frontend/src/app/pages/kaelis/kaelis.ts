import { Component, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { ItemIcon } from '../../core/item-icon';
import { OutfitPreview } from '../../core/outfit-preview';
import {
  ClassDef, ClassStanceDef, ELEMENT_LABELS, EquipmentSlot, ItemCatalogEntry,
  MasteryNodeDef, MasteryState, RARITY_COLORS, SkillDef, SkinDef, WEAPON_LABELS, WaifuDef,
} from '../../core/types';

type KaeliTab = 'perfil' | 'skins' | 'maestria' | 'equipamento' | 'informacao';

@Component({
  selector: 'app-kaelis',
  standalone: true,
  imports: [OutfitPreview, ItemIcon],
  template: `
    <div class="page">
      <div class="main">

        <!-- ── HERO COLUMN ── -->
        <div class="hero-col">
          @if (selected(); as w) {
            <div class="hero-art">
              <app-outfit-preview
                [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
                [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
                [addons]="skinFor(w).addons ?? 0" [mountLookType]="mountLookType(w.id)" [size]="180" />
              <button class="skin-shortcut" title="Trocar skin" (click)="tab.set('skins')">👕</button>
            </div>
            <div class="stars" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</div>
            <h2 class="hero-name">{{ w.name }}</h2>
            <p class="hero-title">— {{ w.title }}</p>
            <div class="hero-tags">
              <span class="tag class-tag">{{ classFor(w)?.name }}</span>
              <span class="tag">{{ elementLabel(w.element) }}</span>
              <span class="tag">{{ weaponLabel(w.weapon) }}</span>
            </div>
            <div class="hero-stats">
              <div class="stat-pill"><span>ATK</span><b>{{ w.baseAtk }}</b></div>
              <div class="stat-pill"><span>HP</span><b>{{ w.baseHp }}</b></div>
              <div class="stat-pill"><span>Afinidade</span><b>{{ affinityLevel(w.id) }}</b></div>
              <div class="stat-pill"><span>Ascensão</span><b>A{{ ascension(w.id) }}</b></div>
            </div>
            @if (isActive(w.id)) {
              <span class="active-badge">ATIVA</span>
            } @else if (owned(w.id)) {
              <button class="btn secondary compact" [disabled]="busy()" (click)="setActive(w.id)">Tornar ativa</button>
            }
          } @else {
            <p class="muted" style="padding:24px;text-align:center">Selecione uma Kaeli abaixo.</p>
          }
        </div>

        <!-- ── CONTENT COLUMN ── -->
        <div class="content-col">
          <h1 class="page-title">Kaelis</h1>
          <p class="sub">Nove Kaelis, cada uma um projeto: afinidade destrava ecos de memória e skins; a maestria molda o kit.</p>

          @if (selected(); as w) {
            @if (owned(w.id)) {
              <div class="tabs">
                @for (t of tabs; track t.id) {
                  <button class="tab" [class.active]="tab() === t.id" (click)="tab.set(t.id)">{{ t.label }}</button>
                }
              </div>

              <!-- ═══ PERFIL ═══ -->
              @if (tab() === 'perfil') {
                <div class="tab-content">
                  <div class="trait-card">
                    <span class="trait-label">TRAIT</span>
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
                      <button class="btn compact" [disabled]="busy() || shards(w.id) < ascCost(w.id)" (click)="ascend(w.id)">
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
                  <div class="mastery-header">
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
                  @if (equipmentTotals(w.id).length > 0) {
                    <div class="equip-summary">
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
                    <div class="gear-picker">
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
                  <div class="info-section">
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
                  <div class="info-section">
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
                  <div class="info-section">
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
                    <div class="info-section">
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
                      <div class="skin-card" [class.selected]="isSelectedSkin(w, skin)"
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
                          <button class="btn compact" [disabled]="busy() || !canAfford(skin)"
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
              <div class="tab-content" style="margin-top:8px">
                <p class="muted">Você ainda não recrutou esta Kaeli. Tente a sorte no banner!</p>
                <div class="trait-card" style="margin-top:12px">
                  <span class="trait-label">TRAIT</span>
                  <b>{{ w.trait.name }}</b>
                  <p>{{ w.trait.description }}</p>
                </div>
              </div>
            }
          }
        </div>
      </div>

      <!-- ── CAROUSEL ── -->
      <div class="carousel">
        @for (w of allWaifus(); track w.id) {
          <button class="cs-slot" [class.owned]="owned(w.id)" [class.selected]="selected()?.id === w.id"
                  [style.--rc]="rarityColor(w.rarity)" (click)="select(w)">
            <app-outfit-preview [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
              [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
              [addons]="skinFor(w).addons ?? 0" [size]="54" [animate]="false" />
            <span class="cs-name">{{ w.name }}</span>
            <span class="cs-stars" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</span>
            @if (isActive(w.id)) { <span class="cs-active">ATIVA</span> }
            @if (!owned(w.id)) { <span class="cs-lock">🔒</span> }
          </button>
        }
      </div>
    </div>
  `,
  styles: [`
    /* ── LAYOUT ── */
    .page { display: flex; flex-direction: column; max-width: 1280px; margin: 0 auto; }
    .main { display: grid; grid-template-columns: 256px 1fr; min-height: 0; }

    /* ── HERO COLUMN ── */
    .hero-col {
      background: linear-gradient(180deg, #10101a 0%, #0c0c14 100%);
      border-right: 1px solid #1a1a28;
      padding: 24px 14px 20px;
      display: flex; flex-direction: column; align-items: center; gap: 10px;
    }
    .hero-art { position: relative; }
    .skin-shortcut {
      position: absolute; bottom: 0; right: -10px;
      background: #1e1e2c; border: 1px solid #3a3a52; border-radius: 8px;
      padding: 6px 8px; font-size: 16px; cursor: pointer; line-height: 1;
    }
    .skin-shortcut:hover { background: #2c2c40; }
    .stars { letter-spacing: 2px; font-size: 14px; }
    .hero-name { margin: 0; font-size: 19px; text-align: center; }
    .hero-title { margin: 0; color: #2dd4bf; font-size: 13px; text-align: center; }
    .hero-tags { display: flex; gap: 5px; flex-wrap: wrap; justify-content: center; }
    .tag { background: #1e1e2c; border-radius: 6px; padding: 3px 8px; font-size: 11px; font-weight: 700; }
    .class-tag { color: #8bfff1; border: 1px solid #2d6060; }
    .hero-stats { display: grid; grid-template-columns: 1fr 1fr; gap: 6px; width: 100%; }
    .stat-pill {
      background: #13131e; border: 1px solid #2c2c3e; border-radius: 8px;
      padding: 6px 8px; display: flex; flex-direction: column; align-items: center; gap: 2px;
    }
    .stat-pill span { font-size: 9px; color: #60607a; text-transform: uppercase; letter-spacing: 0.5px; }
    .stat-pill b { font-size: 15px; }
    .active-badge { background: #2dd4bf; color: #04211d; font-size: 10px; font-weight: 800; border-radius: 6px; padding: 3px 10px; }

    /* ── CONTENT COLUMN ── */
    .content-col { padding: 20px 24px; overflow-y: auto; }
    .page-title { margin: 0 0 2px; font-size: 22px; }
    .sub { color: #9c9ab0; margin: 0 0 14px; font-size: 13px; }

    .tabs { display: flex; gap: 2px; border-bottom: 1px solid #22223a; margin-bottom: 18px; }
    .tab {
      background: none; border: none; border-bottom: 2px solid transparent;
      color: #9c9ab0; padding: 8px 14px; font-size: 13px; font-weight: 800; cursor: pointer;
    }
    .tab.active { color: #8bfff1; border-bottom-color: #2dd4bf; }
    .tab:hover:not(.active) { color: #c0c0d8; }

    .tab-content { display: flex; flex-direction: column; gap: 16px; }

    /* ── PERFIL ── */
    .trait-card {
      padding: 12px 14px; border: 1px solid #3d2d5c; border-radius: 10px;
      background: linear-gradient(135deg, #1a1626, #13131e);
    }
    .trait-label { color: #b18cff; font-size: 10px; font-weight: 800; letter-spacing: 1px; display: block; margin-bottom: 4px; }
    .trait-card b { color: #d9c7ff; }
    .trait-card p { margin: 4px 0 0; color: #9c9ab0; font-size: 13px; }
    .personality { color: #606078; font-style: italic; margin: 0; font-size: 13px; }

    .overview-stats { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
    .ov-stat {
      background: #13131e; border: 1px solid #2c2c3e; border-radius: 8px;
      padding: 10px 14px; display: flex; justify-content: space-between; align-items: center;
    }
    .ov-label { font-size: 12px; color: #60607a; }
    .ov-val { font-size: 14px; font-weight: 700; }

    .asc-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .asc-dots { display: flex; gap: 5px; }
    .dot { color: #2c2c3e; font-size: 18px; }
    .dot.on { color: #e8a93c; }
    .maxed { color: #e8a93c; font-weight: 800; }

    /* ── MAESTRIA TREE ── */
    .mastery-header {
      display: flex; align-items: center; gap: 16px; flex-wrap: wrap;
      padding: 12px 16px; background: #10101a; border: 1px solid #2c2c3e; border-radius: 10px;
    }
    .mastery-pts-badge {
      margin-left: auto; background: #1c180a; border: 1px solid #4a3c10; border-radius: 8px;
      padding: 6px 14px; font-size: 22px; font-weight: 800; color: #e8a93c; display: flex; align-items: baseline; gap: 4px;
    }
    .mastery-pts-badge span { font-size: 11px; color: #906820; }

    .mastery-tree { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
    .branch-label { font-size: 11px; font-weight: 800; color: #8bfff1; text-transform: uppercase; letter-spacing: 1px; text-align: center; margin-bottom: 14px; }

    .tree-node { position: relative; display: flex; gap: 10px; align-items: flex-start; padding-bottom: 20px; }
    .tree-node:not(.tree-last)::after {
      content: ''; position: absolute; left: 14px; top: 32px; bottom: 0; width: 2px; background: #2a2a3e;
    }
    .tree-node.unlocked::after { background: rgba(45,212,191,0.35); }

    .node-dot {
      flex-shrink: 0; width: 30px; height: 30px; border-radius: 50%;
      border: 2px solid #2a2a3e; background: #10101a;
      display: flex; align-items: center; justify-content: center;
      font-size: 11px; font-weight: 800; color: #404058; position: relative; z-index: 1;
    }
    .tree-node.unlocked .node-dot {
      border-color: #2dd4bf; background: #0a1c18; color: #2dd4bf;
      box-shadow: 0 0 10px rgba(45,212,191,0.3);
    }
    .tree-node.available .node-dot { border-color: #3a5a6a; color: #6aaabb; }
    .tree-node.key-node .node-dot {
      width: 34px; height: 34px; border-radius: 6px; transform: rotate(45deg);
    }
    .tree-node.key-node .node-dot span { transform: rotate(-45deg); display: block; }

    .node-info {
      flex: 1; background: #10101a; border: 1px solid #2a2a3e; border-radius: 8px; padding: 8px 10px;
    }
    .tree-node.unlocked .node-info { border-color: rgba(45,212,191,0.2); background: #0a1c18; }
    .tree-node.available .node-info { border-color: #2a3c44; }
    .node-info b { font-size: 12px; display: block; margin-bottom: 3px; }
    .node-info p { margin: 0 0 6px; color: #9c9ab0; font-size: 11px; line-height: 1.4; }
    .node-done { color: #2dd4bf; font-size: 11px; font-weight: 800; }
    .node-actions { display: flex; align-items: center; gap: 8px; }
    .node-cost { color: #e8a93c; font-size: 11px; font-weight: 800; }

    /* ── EQUIPAMENTO ── */
    .equip-summary {
      background: #10101a; border: 1px solid #2c2c3e; border-radius: 10px; padding: 12px 16px;
    }
    .equip-summary h4 { margin: 0 0 10px; font-size: 13px; color: #8bfff1; font-weight: 800; text-transform: uppercase; letter-spacing: 0.5px; }
    .summary-row { display: flex; gap: 16px; flex-wrap: wrap; }
    .summary-stat { display: flex; flex-direction: column; align-items: center; gap: 2px; min-width: 60px; }
    .summary-stat span { font-size: 10px; color: #60607a; text-transform: uppercase; letter-spacing: 0.5px; }
    .summary-stat b { font-size: 18px; color: #2dd4bf; }

    .paperdoll { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
    .gear-slot {
      min-height: 100px; border: 1px solid #2a2a3e; border-radius: 10px; background: #10101a;
      color: inherit; padding: 8px; display: flex; flex-direction: column;
      align-items: center; justify-content: center; gap: 3px; cursor: pointer;
    }
    .gear-slot:hover { border-color: #3a3a52; }
    .gear-slot.active { border-color: #2dd4bf; background: #0a1c18; }
    .gear-slot b { font-size: 11px; text-align: center; }
    .gear-slot small { color: #8f8da3; font-size: 10px; text-align: center; }
    .slot-name { color: #2dd4bf; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .empty-slot { color: #383850; font-size: 12px; }
    .gear-picker { padding: 12px; border: 1px solid #2a2a3e; border-radius: 10px; background: #0d0d16; }
    .picker-title { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
    .gear-options { display: flex; flex-wrap: wrap; gap: 8px; }
    .gear-option {
      border: 1px solid #2a2a3e; border-radius: 8px; background: #13131e;
      color: inherit; padding: 6px 8px; display: flex; align-items: center; gap: 8px; text-align: left; cursor: pointer;
    }
    .gear-option:not([disabled]):hover { border-color: #3a3a52; }
    .gear-option span { display: flex; flex-direction: column; }
    .gear-option small { color: #8f8da3; font-size: 10px; }
    .req-locked { color: #e28a98 !important; }

    /* ── INFORMAÇÃO ── */
    .info-section { background: #10101a; border: 1px solid #2c2c3e; border-radius: 10px; padding: 14px 16px; }
    .section-title { margin: 0 0 10px; font-size: 15px; }
    .aff-level-badge { color: #f08fb6; font-size: 16px; font-weight: 800; margin-left: 6px; }
    .aff-bar { height: 8px; background: #1c1c2c; border-radius: 6px; overflow: hidden; margin: 8px 0 6px; }
    .aff-fill { height: 100%; background: linear-gradient(90deg, #f08fb6, #e84393); transition: width .3s; }
    .fav-row { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; flex-wrap: wrap; }
    .fav-item { display: inline-flex; align-items: center; gap: 2px; background: #261626; border-radius: 6px; padding: 2px 6px; font-size: 11px; }
    .gift-options { display: flex; flex-wrap: wrap; gap: 8px; max-height: 180px; overflow-y: auto; }
    .gift-option {
      border: 1px solid #2a2a3e; border-radius: 8px; background: #13131e;
      color: inherit; padding: 6px 8px; display: flex; align-items: center; gap: 8px; text-align: left; cursor: pointer;
    }
    .gift-option.fav { border-color: #e84393; }
    .gift-option span { display: flex; flex-direction: column; }
    .gift-option small { color: #f08fb6; font-size: 10px; }
    .lore-entry { display: flex; gap: 12px; margin-bottom: 10px; }
    .lore-entry:last-child { margin-bottom: 0; }
    .lore-entry p { margin: 0; color: #c9c7d8; font-size: 13px; line-height: 1.55; }
    .lore-entry.locked p { color: #484860; }
    .lore-num { color: #2dd4bf; font-size: 10px; font-weight: 800; white-space: nowrap; padding-top: 3px; }
    .stances { display: flex; gap: 6px; margin-bottom: 12px; flex-wrap: wrap; }
    .stance-tab {
      border: 1px solid #3a3a52; border-radius: 7px; background: #181824; color: #9c9ab0;
      padding: 5px 10px; font-size: 12px; font-weight: 800; cursor: pointer;
    }
    .stance-tab.active { border-color: #2dd4bf; color: #8bfff1; background: #0a1c18; }
    .skill { display: flex; gap: 12px; margin-bottom: 10px; align-items: flex-start; }
    .skill:last-child { margin-bottom: 0; }
    .skill .key {
      background: #1e1e2c; border: 1px solid #3a3a52; border-radius: 6px;
      width: 28px; height: 28px; display: flex; align-items: center; justify-content: center;
      font-weight: 800; flex-shrink: 0; font-size: 12px;
    }
    .skill p { margin: 2px 0 0; color: #9c9ab0; font-size: 12px; }
    .element-name { margin-left: 8px; color: #2dd4bf; font-size: 10px; font-weight: 800; text-transform: uppercase; }

    /* ── SKINS ── */
    .skins-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(155px, 1fr)); gap: 12px; }
    .skin-card {
      border: 1px solid #2a2a3e; border-radius: 12px; background: #10101a; padding: 12px;
      display: flex; flex-direction: column; align-items: center; gap: 6px; text-align: center;
    }
    .skin-card.selected { border-color: #2dd4bf; background: #0a1c18; }
    .skin-card.locked { opacity: 0.7; }
    .skin-desc { color: #8f8da3; font-size: 11px; margin: 0; line-height: 1.4; }
    .skin-badge { color: #b18cff; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .skin-active { color: #2dd4bf; font-weight: 800; font-size: 12px; }

    /* ── CAROUSEL ── */
    .carousel {
      display: flex; gap: 8px; padding: 10px 16px;
      overflow-x: auto; border-top: 1px solid #1a1a28; background: #0a0a12;
      flex-shrink: 0; min-height: 100px; align-items: center;
    }
    .carousel::-webkit-scrollbar { height: 4px; }
    .carousel::-webkit-scrollbar-thumb { background: #2a2a3e; border-radius: 2px; }
    .cs-slot {
      flex: 0 0 78px; position: relative;
      background: #13131e; border: 2px solid #2a2a3e; border-radius: 10px;
      padding: 6px 4px 5px; display: flex; flex-direction: column; align-items: center; gap: 2px;
      color: inherit; cursor: pointer;
    }
    .cs-slot.owned { border-color: var(--rc); }
    .cs-slot:not(.owned) { filter: grayscale(0.85) brightness(0.45); }
    .cs-slot.selected { outline: 2px solid #2dd4bf; outline-offset: 2px; }
    .cs-slot:hover { opacity: 0.85; }
    .cs-name { font-size: 10px; font-weight: 700; text-align: center; line-height: 1.2; }
    .cs-stars { font-size: 8px; }
    .cs-lock { position: absolute; top: 4px; right: 4px; font-size: 10px; }
    .cs-active { position: absolute; top: 3px; left: 3px; background: #2dd4bf; color: #04211d; font-size: 7px; font-weight: 800; border-radius: 3px; padding: 1px 3px; }

    /* ── UTILS ── */
    .btn {
      background: #2dd4bf; color: #04211d; border: none; border-radius: 7px;
      padding: 8px 16px; font-size: 13px; font-weight: 800; cursor: pointer;
    }
    .btn:disabled { opacity: 0.4; cursor: not-allowed; }
    .btn.secondary { background: #1e1e2c; color: #c9c7d8; border: 1px solid #3a3a52; }
    .btn.compact { padding: 5px 10px; font-size: 12px; }
    .btn.mini { padding: 3px 7px; font-size: 10px; border-radius: 5px; }
    .muted { color: #60607a; }
    .small { font-size: 11px; }

    @media (max-width: 860px) {
      .main { grid-template-columns: 1fr; }
      .hero-col { flex-direction: row; flex-wrap: wrap; justify-content: center; padding: 16px; }
      .mastery-tree { grid-template-columns: 1fr; }
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

  select(w: WaifuDef): void {
    this.selected.set(w);
    this.tab.set('perfil');
    this.previewStanceId.set(this.initialStance(w)?.id ?? '');
    this.selectedEquipmentSlot.set(null);
  }

  owned(id: string): boolean { return this.api.account()?.ownedWaifus.includes(id) ?? false; }
  isActive(id: string): boolean { return this.api.account()?.activeWaifuId === id; }
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

  equippedItem(waifuId: string, slot: EquipmentSlot): ItemCatalogEntry | undefined {
    const itemId = this.api.account()?.equipment?.[waifuId]?.[slot];
    return itemId === undefined ? undefined : this.itemById(itemId);
  }

  equipmentCandidates(waifuId: string, slot: EquipmentSlot): ItemCatalogEntry[] {
    const inventory = this.api.account()?.inventory ?? [];
    return inventory
      .map((stack) => this.itemById(stack.itemId))
      .filter((item): item is ItemCatalogEntry => item?.slot === slot)
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
    try { await this.api.equipItem(waifuId, slot, itemId); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async unequip(waifuId: string, slot: EquipmentSlot): Promise<void> {
    this.busy.set(true);
    try { await this.api.unequipItem(waifuId, slot); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async ascend(id: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.ascend(id); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async setActive(id: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.setActiveWaifu(id); }
    catch (e) { alert((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
