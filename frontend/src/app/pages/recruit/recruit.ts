import { Component, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { OutfitPreview } from '../../core/outfit-preview';
import { PullResult, RARITY_COLORS } from '../../core/types';

@Component({
  selector: 'app-recruit',
  standalone: true,
  imports: [OutfitPreview],
  template: `
    <div class="page">
      <h1>Recrutar</h1>
      <div class="banners">
        @for (b of banners(); track b.id) {
          <div class="banner panel" [class.featured]="b.featuredWaifuId">
            <div class="art">
              @if (featuredWaifu(b.featuredWaifuId); as fw) {
                <app-outfit-preview [lookType]="fw.lookType" [head]="fw.head" [body]="fw.body"
                  [legs]="fw.legs" [feet]="fw.feet" [addons]="fw.skins[0].addons ?? 0"
                  [mountLookType]="fw.skins[0].mountLookType ?? 0" [size]="140" />
                <div class="feat-name" [style.color]="rarityColor(5)">{{ fw.name }} ★★★★★</div>
              } @else {
                <div class="standard-art">✦</div>
              }
            </div>
            <h2>{{ b.name }}</h2>
            <p class="desc">{{ b.description }}</p>
            <div class="pity">
              <span>5★ pity: <b>{{ pity(b.id).pullsSinceFiveStar }}</b>/80</span>
              <span>4★ pity: <b>{{ pity(b.id).pullsSinceFourStar }}</b>/10</span>
              @if (pity(b.id).featuredGuaranteed) { <span class="guaranteed">próximo 5★ garantido!</span> }
            </div>
            <div class="actions">
              <button class="btn" [disabled]="busy() || kaeros() < pullCost()" (click)="pull(b.id, 1)">
                Convocar ×1 <span class="cost">{{ pullCost() }} ✦</span>
              </button>
              <button class="btn gold" [disabled]="busy() || kaeros() < pullCost() * 10" (click)="pull(b.id, 10)">
                Convocar ×10 <span class="cost">{{ pullCost() * 10 }} ✦</span>
              </button>
            </div>
          </div>
        }
      </div>

      <div class="rates panel">
        <h3>Taxas</h3>
        <p><b style="color:#e8a93c">5★</b> 0.8% (soft pity a partir de 65, garantido em 80 — banner promocional: 50% de ser a destaque, perdeu = próxima garantida)</p>
        <p><b style="color:#a06bd6">4★</b> 6% (garantido a cada 10)</p>
        <p>Duplicatas viram <b>Echo Shards</b> da personagem — use para Ascensão (+8% stats por nível; os addons do outfit são definidos por skin no Outfit Studio).</p>
      </div>
    </div>

    @if (results(); as res) {
      <div class="overlay" (click)="dismissIfDone()">
        <div class="reveal">
          @for (r of res; track $index) {
            <div class="card" [class.revealed]="$index < revealed()"
                 [style.--rc]="rarityColor(r.rarity)">
              <div class="inner">
                @if (waifu(r.waifuId); as w) {
                  <app-outfit-preview [lookType]="w.lookType" [head]="w.head" [body]="w.body"
                    [legs]="w.legs" [feet]="w.feet" [addons]="w.skins[0].addons ?? 0"
                    [mountLookType]="w.skins[0].mountLookType ?? 0" [size]="80" [animate]="false" />
                }
                <div class="stars" [style.color]="rarityColor(r.rarity)">{{ '★'.repeat(r.rarity) }}</div>
                <div class="name">{{ r.name }}</div>
                @if (r.isNew) { <div class="new">NOVA!</div> }
                @else { <div class="shards">+{{ r.shardsGained }} shards</div> }
                @if (r.wasFeatured) { <div class="feat">DESTAQUE</div> }
              </div>
            </div>
          }
        </div>
        <p class="hint">{{ revealed() >= res.length ? 'Clique para fechar' : 'Revelando...' }}</p>
      </div>
    }
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: 24px; }
    .banners { display: grid; grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); gap: 20px; }
    .banner { display: flex; flex-direction: column; align-items: center; text-align: center; }
    .banner.featured { border-color: #e8a93c; background: radial-gradient(ellipse at top, #2a2138 0%, rgba(20,20,30,0.92) 65%); }
    .art { min-height: 160px; display: flex; flex-direction: column; align-items: center; justify-content: center; }
    .standard-art { font-size: 80px; color: #c084fc; }
    .feat-name { font-weight: 800; margin-top: 4px; }
    h2 { margin: 8px 0 4px; }
    .desc { color: #9c9ab0; font-size: 13px; min-height: 36px; }
    .pity { display: flex; gap: 16px; font-size: 13px; color: #b8b6c8; margin-bottom: 12px; flex-wrap: wrap; justify-content: center; }
    .guaranteed { color: #e8a93c; font-weight: 700; }
    .actions { display: flex; gap: 12px; }
    .cost { font-weight: 400; opacity: 0.8; margin-left: 6px; }
    .rates { margin-top: 24px; font-size: 14px; color: #b8b6c8; }
    .rates h3 { margin-top: 0; }
    .overlay {
      position: fixed; inset: 0; background: rgba(5, 5, 10, 0.94); z-index: 100;
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 24px;
    }
    .reveal { display: flex; flex-wrap: wrap; gap: 14px; justify-content: center; max-width: 900px; }
    .card {
      width: 150px; height: 200px; perspective: 600px;
      opacity: 0; transform: translateY(30px) scale(0.8);
      transition: opacity 0.3s, transform 0.3s;
    }
    .card.revealed { opacity: 1; transform: none; }
    .inner {
      height: 100%; border-radius: 12px; border: 2px solid var(--rc);
      background: linear-gradient(180deg, #1c1c2a, #101018);
      box-shadow: 0 0 18px color-mix(in srgb, var(--rc) 45%, transparent);
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
    }
    .stars { font-size: 13px; }
    .name { font-weight: 800; font-size: 15px; }
    .new { color: #2dd4bf; font-weight: 800; font-size: 12px; }
    .shards { color: #9c9ab0; font-size: 12px; }
    .feat { color: #e8a93c; font-weight: 800; font-size: 11px; }
    .hint { color: #707088; }
  `],
})
export class RecruitPage {
  readonly banners = computed(() => this.api.catalog()?.banners ?? []);
  readonly pullCost = computed(() => this.api.catalog()?.pullCost ?? 160);
  readonly kaeros = computed(() => this.api.account()?.kaeros ?? 0);
  readonly busy = signal(false);
  readonly results = signal<PullResult[] | null>(null);
  readonly revealed = signal(0);

  constructor(private readonly api: ApiService) {}

  rarityColor(r: number): string {
    return RARITY_COLORS[r] ?? '#fff';
  }

  pity(bannerId: string) {
    return this.api.account()?.pity?.[bannerId]
      ?? { pullsSinceFiveStar: 0, pullsSinceFourStar: 0, featuredGuaranteed: false, totalPulls: 0 };
  }

  featuredWaifu(id: string | null) {
    if (!id) return null;
    return this.api.catalog()?.waifus.find((w) => w.id === id) ?? null;
  }

  waifu(id: string) {
    return this.api.catalog()?.waifus.find((w) => w.id === id) ?? null;
  }

  async pull(bannerId: string, count: number): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      const res = await this.api.pull(bannerId, count);
      this.results.set(res.results);
      this.revealed.set(0);
      const interval = setInterval(() => {
        this.revealed.update((r) => r + 1);
        if (this.revealed() >= res.results.length) clearInterval(interval);
      }, count === 1 ? 250 : 320);
    } catch (err) {
      alert((err as Error).message);
    } finally {
      this.busy.set(false);
    }
  }

  dismissIfDone(): void {
    if (this.revealed() >= (this.results()?.length ?? 0)) this.results.set(null);
  }
}
