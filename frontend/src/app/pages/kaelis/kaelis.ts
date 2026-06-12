import { Component, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { ItemIcon } from '../../core/item-icon';
import { OutfitPreview } from '../../core/outfit-preview';
import {
  ClassDef, ClassStanceDef, ELEMENT_LABELS, EquipmentSlot, ItemCatalogEntry,
  MasteryNodeDef, MasteryState, RARITY_COLORS, SkillDef, SkinDef, WEAPON_LABELS, WaifuDef,
} from '../../core/types';

type KaeliTab = 'perfil' | 'skins' | 'maestria' | 'equipamento';

@Component({
  selector: 'app-kaelis',
  standalone: true,
  imports: [OutfitPreview, ItemIcon],
  template: `
    <div class="page">
      <h1>Kaelis</h1>
      <p class="sub">Nove Kaelis, cada uma um projeto: afinidade destrava ecos de memória e skins; a maestria molda o kit.</p>
      <div class="layout">
        <div class="roster">
          @for (w of allWaifus(); track w.id) {
            <button class="slot" [class.owned]="owned(w.id)" [class.selected]="selected()?.id === w.id"
                    [style.--rc]="rarityColor(w.rarity)" (click)="select(w)">
              <app-outfit-preview [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
                [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
                [addons]="addons(w.id)" [size]="64" [animate]="false" />
              <span class="nm">{{ w.name }}</span>
              <span class="st" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</span>
              @if (owned(w.id)) { <span class="aff-chip">❤ {{ affinityLevel(w.id) }}</span> }
              @if (!owned(w.id)) { <span class="lock">🔒</span> }
              @if (isActive(w.id)) { <span class="active-tag">ATIVA</span> }
            </button>
          }
        </div>

        @if (selected(); as w) {
          <div class="detail panel">
            <div class="hero">
              <app-outfit-preview [lookType]="skinFor(w).lookType" [head]="skinFor(w).head"
                [body]="skinFor(w).body" [legs]="skinFor(w).legs" [feet]="skinFor(w).feet"
                [addons]="addons(w.id)" [mountLookType]="mountLookType(w.id)" [size]="160" />
              <div>
                <div class="stars" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</div>
                <h2>{{ w.name }} <span class="title">— {{ w.title }}</span></h2>
                <p class="desc">{{ w.description }}</p>
                <div class="tags">
                  <span class="tag class-tag">{{ classFor(w)?.name }}</span>
                  <span class="tag">{{ elementLabel(w.element) }}</span>
                  <span class="tag">{{ weaponLabel(w.weapon) }}</span>
                  <span class="tag">ATK {{ w.baseAtk }}</span>
                  <span class="tag">HP {{ w.baseHp }}</span>
                </div>
              </div>
            </div>

            @if (owned(w.id)) {
              <div class="tabs">
                @for (t of tabs; track t.id) {
                  <button class="tab" [class.active]="tab() === t.id" (click)="tab.set(t.id)">{{ t.label }}</button>
                }
              </div>

              <!-- ================= PERFIL ================= -->
              @if (tab() === 'perfil') {
                <div class="trait-card">
                  <span class="trait-label">TRAIT</span>
                  <b>{{ w.trait.name }}</b>
                  <p>{{ w.trait.description }}</p>
                </div>
                <p class="personality">「 {{ w.personality }} 」</p>

                <div class="affinity">
                  <h3>Afinidade {{ affinityLevel(w.id) }} <span class="muted">/ {{ affinityMax() }}
                    · +{{ affinityLevel(w.id) - 1 }}% ATK/HP na run</span></h3>
                  <div class="aff-bar">
                    <div class="aff-fill" [style.width.%]="affinityPercent(w.id)"></div>
                  </div>
                  @if (affinityToNext(w.id) > 0) {
                    <span class="muted small">{{ affinityInto(w.id) }} / {{ affinityToNext(w.id) }} XP
                      — jogue runs com ela ou dê presentes</span>
                  } @else { <span class="maxed">Afinidade máxima!</span> }
                </div>

                <div class="gifts">
                  <h3>Presentes <span class="muted">· {{ giftsLeft(w.id) }} restante(s) hoje</span></h3>
                  <div class="fav-row">
                    <span class="muted small">Favoritos (XP ×{{ favoriteMultiplier() }}):</span>
                    @for (itemId of w.favoriteGiftItemIds; track itemId) {
                      <span class="fav-item" [title]="itemName(itemId)">
                        <app-item-icon [itemId]="itemId" [size]="30" /> ❤
                      </span>
                    }
                  </div>
                  @if (giftsLeft(w.id) > 0) {
                    <div class="gift-options">
                      @for (item of giftCandidates(); track item.itemId) {
                        <button class="gift-option" [class.fav]="isFavorite(w, item.itemId)"
                                [disabled]="busy()" (click)="gift(w.id, item.itemId)">
                          <app-item-icon [itemId]="item.itemId" [size]="34" />
                          <span><b>{{ item.name }}</b>
                            <small>+{{ giftXpFor(w, item) }} XP{{ isFavorite(w, item.itemId) ? ' ❤' : '' }}</small></span>
                        </button>
                      } @empty {
                        <span class="muted">Nenhum item na Mochila para presentear. Traga loot das runs!</span>
                      }
                    </div>
                  } @else {
                    <span class="muted">{{ w.name }} já ganhou presentes demais hoje. Volte amanhã!</span>
                  }
                </div>

                <div class="lore">
                  <h3>Ecos de Memória</h3>
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

                <div class="ascension">
                  <h3>Ascensão A{{ ascension(w.id) }} <span class="muted">· {{ shards(w.id) }} shards</span></h3>
                  <div class="asc-dots">
                    @for (i of [1,2,3,4,5,6]; track i) {
                      <span class="dot" [class.on]="ascension(w.id) >= i"
                            [title]="i === 2 ? 'Addon 1 do outfit' : i === 4 ? 'Addon 2 do outfit' : '+8% stats'">
                        {{ i === 2 || i === 4 ? '✦' : '●' }}
                      </span>
                    }
                  </div>
                  @if (ascension(w.id) < 6) {
                    <button class="btn" [disabled]="busy() || shards(w.id) < ascCost(w.id)" (click)="ascend(w.id)">
                      Ascender — {{ ascCost(w.id) }} shards
                    </button>
                  } @else {
                    <span class="maxed">Ascensão máxima!</span>
                  }
                  @if (!isActive(w.id)) {
                    <button class="btn secondary" [disabled]="busy()" (click)="setActive(w.id)">Tornar ativa</button>
                  }
                </div>
              }

              <!-- ================= SKINS ================= -->
              @if (tab() === 'skins') {
                <div class="skins-grid">
                  @for (skin of w.skins; track skin.id) {
                    <div class="skin-card" [class.selected]="isSelectedSkin(w, skin)"
                         [class.locked]="!skinUnlocked(w, skin)">
                      <app-outfit-preview [lookType]="skin.lookType" [head]="skin.head" [body]="skin.body"
                        [legs]="skin.legs" [feet]="skin.feet" [addons]="addons(w.id)" [size]="96" />
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
                <p class="muted small">A skin em uso aparece no Hub, nas runs e nesta página. Addons de ascensão aplicam-se a qualquer skin.</p>
              }

              <!-- ================= MAESTRIA ================= -->
              @if (tab() === 'maestria') {
                <div class="mastery-header">
                  <h3>Maestria de Eco <span class="muted">· {{ masteryOf(w.id).points }} ponto(s) disponível(is)</span></h3>
                  <span class="muted small">Vitória +{{ pointsPerVictory() }} · derrota +{{ pointsPerDefeat() }} (com ela ativa)</span>
                  @if (masteryOf(w.id).spent > 0) {
                    <button class="btn secondary compact" [disabled]="busy()" (click)="respec(w.id)">
                      Resetar — {{ respecGold() }} ouro
                    </button>
                  }
                </div>
                <div class="mastery-branches">
                  @for (branch of branches; track branch.id) {
                    <div class="branch">
                      <h4>{{ branch.label }}</h4>
                      @for (node of branchNodes(w.id, branch.id); track node.id) {
                        <div class="node" [class.unlocked]="nodeUnlocked(w.id, node)"
                             [class.available]="nodeAvailable(w.id, node)"
                             [class.key-node]="node.order === 4">
                          <div class="node-head">
                            <b>{{ node.name }}</b>
                            <span class="node-cost">{{ node.cost }} pt</span>
                          </div>
                          <p>{{ node.description }}</p>
                          @if (!nodeUnlocked(w.id, node)) {
                            <button class="btn compact" [disabled]="busy() || !nodeAvailable(w.id, node)"
                                    (click)="unlockNode(w.id, node.id)">
                              {{ nodeAvailable(w.id, node) ? 'Destravar' : nodeBlockReason(w.id, node) }}
                            </button>
                          } @else { <span class="node-done">✔ destravado</span> }
                        </div>
                      }
                    </div>
                  }
                </div>
              }

              <!-- ================= EQUIPAMENTO ================= -->
              @if (tab() === 'equipamento') {
                <div class="equipment">
                  <h3>Equipamento <span class="muted">· por Kaeli</span></h3>
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
                          <button class="btn secondary compact" [disabled]="busy()"
                                  (click)="unequip(w.id, slot)">Desequipar</button>
                        }
                      </div>
                      <div class="gear-options">
                        @for (item of equipmentCandidates(slot); track item.itemId) {
                          <button class="gear-option" [disabled]="busy()" (click)="equip(w.id, slot, item.itemId)">
                            <app-item-icon [itemId]="item.itemId" [size]="38" />
                            <span><b>{{ item.name }}</b><small>{{ itemStats(item) }}</small></span>
                          </button>
                        } @empty {
                          <span class="muted">Nenhum item deste slot na Mochila.</span>
                        }
                      </div>
                    </div>
                  }
                </div>

                <div class="kit">
                  @if (classFor(w); as cls) {
                    <h3>{{ cls.name }} <span class="muted">· kit de classe</span></h3>
                    <p class="class-desc">{{ cls.description }}</p>
                    <div class="stances">
                      @for (stance of cls.stances; track stance.id) {
                        <button class="stance-tab" [class.active]="previewStanceId() === stance.id"
                                (click)="previewStanceId.set(stance.id)">
                          {{ elementLabel(stance.element) }}
                        </button>
                      }
                    </div>
                  }
                  @for (s of kit(w); track s.id; let i = $index) {
                    <div class="skill">
                      <span class="key">{{ ['1','2','3','4','R'][i] }}</span>
                      <div>
                        <b>{{ s.name }}</b>
                        <span class="element-name">{{ elementLabel(s.element) }}</span>
                        <span class="muted">{{ i === 4 ? '(Ultimate · gauge)' : s.cooldownMs / 1000 + 's' }}</span>
                        <p>{{ s.description }}</p>
                      </div>
                    </div>
                  }
                </div>
              }
            } @else {
              <p class="muted">Você ainda não recrutou esta Kaeli. Tente a sorte no banner!</p>
              <div class="trait-card">
                <span class="trait-label">TRAIT</span>
                <b>{{ w.trait.name }}</b>
                <p>{{ w.trait.description }}</p>
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1200px; margin: 0 auto; padding: 24px; }
    .sub { color: #9c9ab0; }
    .layout { display: grid; grid-template-columns: 380px 1fr; gap: 20px; margin-top: 16px; }
    .roster { display: grid; grid-template-columns: repeat(auto-fill, minmax(105px, 1fr)); gap: 10px; align-content: start; }
    .slot {
      position: relative; background: #15151f; border: 2px solid #2c2c3e; border-radius: 10px;
      padding: 8px 4px 6px; display: flex; flex-direction: column; align-items: center; gap: 2px;
      color: inherit;
    }
    .slot.owned { border-color: var(--rc); }
    .slot:not(.owned) { filter: grayscale(0.9) brightness(0.55); }
    .slot.selected { outline: 2px solid #2dd4bf; }
    .nm { font-size: 12px; font-weight: 700; }
    .st { font-size: 10px; }
    .lock { position: absolute; top: 6px; right: 6px; }
    .aff-chip { position: absolute; top: 4px; right: 4px; background: #2a1a26; color: #f08fb6; font-size: 9px; font-weight: 800; border-radius: 4px; padding: 1px 4px; }
    .active-tag { position: absolute; top: 4px; left: 4px; background: #2dd4bf; color: #04211d; font-size: 9px; font-weight: 800; border-radius: 4px; padding: 1px 4px; }
    .detail { align-self: start; }
    .hero { display: flex; gap: 20px; align-items: center; }
    .hero h2 { margin: 2px 0; }
    .title { color: #2dd4bf; font-size: 15px; }
    .desc { color: #9c9ab0; }
    .stars { letter-spacing: 2px; }
    .tags { display: flex; gap: 8px; margin-top: 8px; flex-wrap: wrap; }
    .tag { background: #23232f; border-radius: 6px; padding: 4px 10px; font-size: 12px; font-weight: 700; }
    .class-tag { color: #8bfff1; border: 1px solid #2d6b66; }

    .tabs { display: flex; gap: 6px; margin-top: 18px; border-bottom: 1px solid #26263a; }
    .tab {
      background: none; border: none; border-bottom: 2px solid transparent; color: #9c9ab0;
      padding: 8px 14px; font-size: 13px; font-weight: 800; cursor: pointer;
    }
    .tab.active { color: #8bfff1; border-bottom-color: #2dd4bf; }

    .trait-card {
      margin-top: 14px; padding: 12px 14px; border: 1px solid #3d2d5c; border-radius: 10px;
      background: linear-gradient(135deg, #1a1626, #15151f);
    }
    .trait-label { color: #b18cff; font-size: 10px; font-weight: 800; letter-spacing: 1px; display: block; }
    .trait-card b { color: #d9c7ff; }
    .trait-card p { margin: 4px 0 0; color: #9c9ab0; font-size: 13px; }
    .personality { color: #707088; font-style: italic; margin: 10px 0 0; }

    .affinity { margin-top: 18px; padding-top: 14px; border-top: 1px solid #26263a; }
    .affinity h3 { margin: 0 0 8px; }
    .aff-bar { height: 10px; background: #23232f; border-radius: 6px; overflow: hidden; }
    .aff-fill { height: 100%; background: linear-gradient(90deg, #f08fb6, #e84393); transition: width .3s; }
    .small { font-size: 11px; }

    .gifts { margin-top: 18px; padding-top: 14px; border-top: 1px solid #26263a; }
    .gifts h3 { margin: 0 0 8px; }
    .fav-row { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; flex-wrap: wrap; }
    .fav-item { display: inline-flex; align-items: center; gap: 2px; background: #2a1a26; border-radius: 6px; padding: 2px 6px; font-size: 11px; }
    .gift-options { display: flex; flex-wrap: wrap; gap: 8px; max-height: 220px; overflow-y: auto; }
    .gift-option {
      border: 1px solid #343447; border-radius: 8px; background: #1b1b27; color: inherit;
      padding: 6px 8px; display: flex; align-items: center; gap: 8px; text-align: left;
    }
    .gift-option.fav { border-color: #e84393; }
    .gift-option span { display: flex; flex-direction: column; }
    .gift-option small { color: #f08fb6; font-size: 10px; }

    .lore { margin-top: 18px; padding-top: 14px; border-top: 1px solid #26263a; }
    .lore h3 { margin: 0 0 10px; }
    .lore-entry { display: flex; gap: 12px; margin-bottom: 12px; }
    .lore-entry p { margin: 0; color: #c9c7d8; font-size: 13px; line-height: 1.55; }
    .lore-entry.locked p { color: #55556a; }
    .lore-num { color: #2dd4bf; font-size: 10px; font-weight: 800; white-space: nowrap; padding-top: 3px; }

    .ascension { margin-top: 18px; padding-top: 14px; border-top: 1px solid #26263a; display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
    .ascension h3 { margin: 0; }
    .asc-dots { display: flex; gap: 6px; }
    .dot { color: #33334a; font-size: 18px; }
    .dot.on { color: #e8a93c; }
    .maxed { color: #e8a93c; font-weight: 800; }

    .skins-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 12px; margin-top: 16px; }
    .skin-card {
      border: 1px solid #343447; border-radius: 12px; background: #171721; padding: 12px;
      display: flex; flex-direction: column; align-items: center; gap: 6px; text-align: center;
    }
    .skin-card.selected { border-color: #2dd4bf; background: #102526; }
    .skin-card.locked { opacity: 0.75; }
    .skin-desc { color: #8f8da3; font-size: 11px; margin: 0; line-height: 1.4; }
    .skin-badge { color: #b18cff; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .skin-active { color: #2dd4bf; font-weight: 800; font-size: 12px; }

    .mastery-header { display: flex; align-items: center; gap: 14px; margin-top: 16px; flex-wrap: wrap; }
    .mastery-header h3 { margin: 0; }
    .mastery-branches { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; margin-top: 14px; }
    .branch h4 { margin: 0 0 8px; color: #8bfff1; font-size: 13px; text-transform: uppercase; letter-spacing: 1px; }
    .node {
      border: 1px solid #2c2c3e; border-radius: 10px; background: #15151f; padding: 10px;
      margin-bottom: 8px; opacity: 0.6;
    }
    .node.available { opacity: 1; border-color: #3a5a6a; }
    .node.unlocked { opacity: 1; border-color: #2dd4bf; background: #102526; }
    .node.key-node { border-style: double; border-width: 3px; }
    .node-head { display: flex; justify-content: space-between; gap: 8px; align-items: baseline; }
    .node-head b { font-size: 12px; }
    .node-cost { color: #e8a93c; font-size: 11px; font-weight: 800; white-space: nowrap; }
    .node p { margin: 4px 0 8px; color: #9c9ab0; font-size: 11px; line-height: 1.4; }
    .node-done { color: #2dd4bf; font-size: 11px; font-weight: 800; }

    .equipment { margin-top: 16px; }
    .equipment h3 { margin-top: 0; }
    .paperdoll { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
    .gear-slot {
      min-height: 104px; border: 1px solid #343447; border-radius: 9px; background: #171721;
      color: inherit; padding: 8px; display: flex; flex-direction: column; align-items: center;
      justify-content: center; gap: 3px;
    }
    .gear-slot.active { border-color: #2dd4bf; background: #102526; }
    .gear-slot b { font-size: 11px; }
    .gear-slot small, .gear-option small { color: #8f8da3; font-size: 10px; }
    .slot-name { color: #2dd4bf; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .empty-slot { color: #55556a; font-size: 12px; }
    .gear-picker { margin-top: 10px; padding: 10px; border: 1px solid #2c2c3e; border-radius: 9px; background: #12121b; }
    .picker-title { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    .compact { padding: 4px 9px; font-size: 11px; }
    .gear-options { display: flex; flex-wrap: wrap; gap: 8px; }
    .gear-option {
      border: 1px solid #343447; border-radius: 8px; background: #1b1b27; color: inherit;
      padding: 6px 8px; display: flex; align-items: center; gap: 8px; text-align: left;
    }
    .gear-option span { display: flex; flex-direction: column; }
    .kit { margin-top: 18px; padding-top: 14px; border-top: 1px solid #26263a; }
    .kit h3 { margin-top: 0; }
    .class-desc { color: #9c9ab0; font-size: 13px; margin: -6px 0 10px; }
    .stances { display: flex; gap: 8px; margin-bottom: 14px; }
    .stance-tab {
      border: 1px solid #3a3a4c; border-radius: 7px; background: #181822; color: #9c9ab0;
      padding: 6px 12px; font-size: 12px; font-weight: 800;
    }
    .stance-tab.active { border-color: #2dd4bf; color: #8bfff1; background: #102526; }
    .skill { display: flex; gap: 12px; margin-bottom: 10px; align-items: flex-start; }
    .skill .key {
      background: #23232f; border: 1px solid #3a3a4c; border-radius: 6px; width: 30px; height: 30px;
      display: flex; align-items: center; justify-content: center; font-weight: 800; flex-shrink: 0;
    }
    .skill p { margin: 2px 0 0; color: #9c9ab0; font-size: 13px; }
    .element-name { margin-left: 8px; color: #2dd4bf; font-size: 10px; font-weight: 800; text-transform: uppercase; }
    .muted { color: #707088; font-size: 13px; font-weight: 400; }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } .mastery-branches { grid-template-columns: 1fr; } }
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
    { id: 'skins', label: 'Skins' },
    { id: 'maestria', label: 'Maestria' },
    { id: 'equipamento', label: 'Equipamento' },
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
    // pre-select active waifu once data is in
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

  addons(id: string): number {
    const cat = this.api.catalog();
    if (!cat) return 0;
    const asc = this.ascension(id);
    return asc >= cat.addonAscensions[1] ? 3 : asc >= cat.addonAscensions[0] ? 1 : 0;
  }

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

  // ---- equipamento (inalterado) ----

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

  equipmentCandidates(slot: EquipmentSlot): ItemCatalogEntry[] {
    const inventory = this.api.account()?.inventory ?? [];
    return inventory
      .map((stack) => this.itemById(stack.itemId))
      .filter((item): item is ItemCatalogEntry => item?.slot === slot)
      .sort((a, b) =>
        (b.attack + b.armor + b.defense + b.mountSpeed)
        - (a.attack + a.armor + a.defense + a.mountSpeed));
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
    try { await this.api.ascend(id); } catch (e) { alert((e as Error).message); } finally { this.busy.set(false); }
  }

  async setActive(id: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.setActiveWaifu(id); } catch (e) { alert((e as Error).message); } finally { this.busy.set(false); }
  }
}
