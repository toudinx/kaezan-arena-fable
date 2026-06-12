import { Component, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { OutfitPreview } from '../../core/outfit-preview';
import {
  ClassDef, ClassStanceDef, ELEMENT_LABELS, RARITY_COLORS, SkillDef, WEAPON_LABELS, WaifuDef,
} from '../../core/types';

@Component({
  selector: 'app-kaelis',
  standalone: true,
  imports: [OutfitPreview],
  template: `
    <div class="page">
      <h1>Kaelis</h1>
      <p class="sub">Sua coleção. Ascensões desbloqueiam os addons do outfit (A2 e A4) e +8% de stats por nível.</p>
      <div class="layout">
        <div class="roster">
          @for (w of allWaifus(); track w.id) {
            <button class="slot" [class.owned]="owned(w.id)" [class.selected]="selected()?.id === w.id"
                    [style.--rc]="rarityColor(w.rarity)" (click)="select(w)">
              <app-outfit-preview [lookType]="w.lookType" [head]="w.head" [body]="w.body"
                [legs]="w.legs" [feet]="w.feet" [addons]="addons(w.id)" [size]="64" [animate]="false" />
              <span class="nm">{{ w.name }}</span>
              <span class="st" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</span>
              @if (!owned(w.id)) { <span class="lock">🔒</span> }
              @if (isActive(w.id)) { <span class="active-tag">ATIVA</span> }
            </button>
          }
        </div>

        @if (selected(); as w) {
          <div class="detail panel">
            <div class="hero">
              <app-outfit-preview [lookType]="w.lookType" [head]="w.head" [body]="w.body"
                [legs]="w.legs" [feet]="w.feet" [addons]="addons(w.id)" [size]="160" />
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
            } @else {
              <p class="muted">Você ainda não recrutou esta Kaeli. Tente a sorte no banner!</p>
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
    .ascension { margin-top: 18px; padding-top: 14px; border-top: 1px solid #26263a; display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
    .ascension h3 { margin: 0; }
    .asc-dots { display: flex; gap: 6px; }
    .dot { color: #33334a; font-size: 18px; }
    .dot.on { color: #e8a93c; }
    .maxed { color: #e8a93c; font-weight: 800; }
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
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
  `],
})
export class KaelisPage {
  readonly allWaifus = computed(() => {
    const list = [...(this.api.catalog()?.waifus ?? [])];
    return list.sort((a, b) => b.rarity - a.rarity || a.name.localeCompare(b.name));
  });
  readonly selected = signal<WaifuDef | null>(null);
  readonly previewStanceId = signal('');
  readonly busy = signal(false);

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
    this.previewStanceId.set(this.initialStance(w)?.id ?? '');
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

  async ascend(id: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.ascend(id); } catch (e) { alert((e as Error).message); } finally { this.busy.set(false); }
  }

  async setActive(id: string): Promise<void> {
    this.busy.set(true);
    try { await this.api.setActiveWaifu(id); } catch (e) { alert((e as Error).message); } finally { this.busy.set(false); }
  }
}
