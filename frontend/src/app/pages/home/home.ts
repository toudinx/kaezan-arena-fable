import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { OutfitPreview } from '../../core/outfit-preview';
import { RARITY_COLORS, SkinDef, WaifuDef } from '../../core/types';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, OutfitPreview],
  template: `
    <div class="hub">
      <section class="showcase panel">
        @if (pinnedWaifu(); as w) {
          <div class="stage">
            @if (pinnedSkin(); as skin) {
              <app-outfit-preview
                [lookType]="skin.lookType" [head]="skin.head" [body]="skin.body"
                [legs]="skin.legs" [feet]="skin.feet" [addons]="pinnedAddons()"
                [mountLookType]="pinnedMount()" [size]="180" />
            }
          </div>
          <div class="who">
            <div class="stars" [style.color]="rarityColor(w.rarity)">{{ '★'.repeat(w.rarity) }}</div>
            <h1>{{ w.name }}</h1>
            <p class="title">{{ w.title }}</p>
            <p class="desc">{{ w.description }}</p>
            @if (owned().length > 1) {
              <div class="pin-picker">
                <span class="pin-label">★ Destaque</span>
                <div class="pin-strip">
                  @for (o of owned(); track o.id) {
                    <button class="pin-thumb" [class.active]="o.id === w.id"
                            [style.--rc]="rarityColor(o.rarity)" [title]="o.name"
                            [disabled]="busy()" (click)="pick(o.id)">
                      <app-outfit-preview [lookType]="skinFor(o).lookType" [head]="skinFor(o).head"
                        [body]="skinFor(o).body" [legs]="skinFor(o).legs" [feet]="skinFor(o).feet"
                        [addons]="skinFor(o).addons ?? 0" [size]="38" [animate]="false" />
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        } @else {
          <p>Recrute uma Kaeli no banner para vê-la aqui.</p>
        }
      </section>

      <section class="rail">
        <a routerLink="/hunt" class="action hunt">
          <h2>⚔ Caçada</h2>
          <p>Dungeons procedurais com monstros de Tibia</p>
        </a>
        <a routerLink="/kaelis" class="action kaelis">
          <h2>👥 Kaelis</h2>
          <p>Sua coleção de caçadoras</p>
        </a>
        <a routerLink="/recruit" class="action recruit">
          <h2>✦ Recrutar</h2>
          <p>Convocação — novo banner ativo!</p>
        </a>
        <a routerLink="/backpack" class="action backpack">
          <h2>🎒 Mochila</h2>
          <p>Saque das suas expedições</p>
        </a>
      </section>

      <section class="dailies panel">
        <h2>Contratos Diários <span class="reset">reseta 00:00 UTC</span></h2>
        @for (d of dailies(); track d.id) {
          <div class="contract" [class.done]="d.progress >= d.target">
            <div class="info">
              <span class="desc">{{ d.description }}</span>
              <div class="bar"><div class="fill" [style.width.%]="(100 * d.progress) / d.target"></div></div>
              <span class="prog">{{ d.progress }} / {{ d.target }}</span>
            </div>
            @if (d.claimed) {
              <span class="claimed">✓ Resgatado</span>
            } @else if (d.progress >= d.target) {
              <button class="btn gold" (click)="claim(d.id)">Resgatar</button>
            } @else {
              <span class="reward">+100 ✦ · +150 🪙</span>
            }
          </div>
        } @empty {
          <p class="muted">Carregando contratos...</p>
        }
        @if (account(); as acc) {
          <div class="account-progress">
            <span>Conta Lv. {{ acc.accountLevel }}</span>
            <div class="bar"><div class="fill xp" [style.width.%]="(100 * acc.accountXp) / acc.accountXpNext"></div></div>
            <span class="muted">{{ acc.accountXp }} / {{ acc.accountXpNext }} XP · {{ acc.runsWon }}/{{ acc.runsPlayed }} vitórias</span>
          </div>
        }
      </section>
    </div>
  `,
  styles: [`
    .hub {
      display: grid; grid-template-columns: 1.2fr 0.9fr; gap: 20px;
      max-width: 1200px; margin: 0 auto; padding: 24px;
    }
    .showcase { display: flex; align-items: center; gap: 24px; min-height: 280px;
      background: radial-gradient(ellipse at 30% 20%, #1d1d33 0%, rgba(20,20,30,0.92) 70%); }
    .stage { flex-shrink: 0; padding: 12px; background: radial-gradient(circle, #232338 0%, transparent 70%); border-radius: 50%; }
    .who h1 { margin: 4px 0; font-size: 34px; }
    .who .title { color: #2dd4bf; font-weight: 700; margin: 0 0 10px; }
    .who .desc { color: #9c9ab0; line-height: 1.5; }
    .pin-picker { margin-top: 16px; }
    .pin-label { font-size: 10px; font-weight: 800; color: #707088; text-transform: uppercase; letter-spacing: 1px; }
    .pin-strip { display: flex; gap: 6px; flex-wrap: wrap; margin-top: 6px; }
    .pin-thumb {
      width: 46px; height: 46px; padding: 0; border-radius: 9px; cursor: pointer;
      background: #13131e; border: 2px solid #2a2a3e; overflow: hidden;
      display: flex; align-items: center; justify-content: center;
    }
    .pin-thumb:hover:not(:disabled) { border-color: var(--rc); }
    .pin-thumb.active { border-color: #2dd4bf; box-shadow: 0 0 8px rgba(45,212,191,0.4); }
    .pin-thumb:disabled { cursor: default; }
    .stars { font-size: 18px; letter-spacing: 2px; }
    .rail { display: flex; flex-direction: column; gap: 12px; }
    .action {
      display: block; text-decoration: none; color: inherit;
      background: #15151f; border: 1px solid #2c2c3e; border-radius: 12px; padding: 16px 20px;
      transition: transform 0.12s, border-color 0.12s;
    }
    .action:hover { transform: translateX(-4px); border-color: #2dd4bf; }
    .action h2 { margin: 0 0 4px; font-size: 19px; }
    .action p { margin: 0; color: #9c9ab0; font-size: 13px; }
    .action.recruit:hover { border-color: #e8a93c; }
    .dailies { grid-column: 1 / -1; }
    .dailies h2 { margin-top: 0; }
    .reset { font-size: 12px; color: #707088; font-weight: 400; margin-left: 8px; }
    .contract {
      display: flex; align-items: center; gap: 16px; padding: 10px 14px;
      background: #15151f; border-radius: 10px; margin-bottom: 8px;
    }
    .contract.done { border-left: 3px solid #2dd4bf; }
    .contract .info { flex: 1; }
    .contract .desc { font-size: 14px; font-weight: 600; }
    .bar { height: 6px; background: #23232f; border-radius: 3px; margin: 6px 0 2px; overflow: hidden; }
    .fill { height: 100%; background: linear-gradient(90deg, #2dd4bf, #0d9488); border-radius: 3px; }
    .fill.xp { background: linear-gradient(90deg, #7df0ff, #38bdf8); }
    .prog { font-size: 12px; color: #9c9ab0; }
    .claimed { color: #2dd4bf; font-weight: 700; }
    .reward { color: #9c9ab0; font-size: 13px; }
    .account-progress { margin-top: 16px; padding-top: 12px; border-top: 1px solid #26263a; }
    .muted { color: #707088; font-size: 13px; }
    @media (max-width: 900px) { .hub { grid-template-columns: 1fr; } }
  `],
})
export class HomePage {
  readonly account = computed(() => this.api.account());
  readonly dailies = computed(() => this.api.account()?.dailies ?? []);
  // Protagonista do Início = Kaeli fixada (favorita) pelo jogador; senão a primeira possuída.
  readonly pinnedWaifu = computed(() => {
    const acc = this.api.account();
    const cat = this.api.catalog();
    if (!acc || !cat) return null;
    const owned = cat.waifus.filter((w) => acc.ownedWaifus.includes(w.id));
    return owned.find((w) => w.id === acc.activeWaifuId) ?? owned[0] ?? null;
  });
  readonly pinnedSkin = computed(() => {
    const w = this.pinnedWaifu();
    if (!w) return null;
    const selectedId = this.api.account()?.selectedSkins?.[w.id];
    return w.skins.find((s) => s.id === selectedId) ?? w.skins[0] ?? null;
  });
  // Os addons exibidos vêm exclusivamente da skin selecionada (0 = nenhum); a ascensão não os força.
  readonly pinnedAddons = computed(() => this.pinnedSkin()?.addons ?? 0);
  readonly pinnedMount = computed(() => this.pinnedSkin()?.mountLookType ?? 0);
  readonly owned = computed(() => {
    const acc = this.api.account();
    const cat = this.api.catalog();
    if (!acc || !cat) return [];
    return cat.waifus
      .filter((w) => acc.ownedWaifus.includes(w.id))
      .sort((a, b) => b.rarity - a.rarity || a.name.localeCompare(b.name));
  });
  readonly busy = signal(false);

  constructor(private readonly api: ApiService) {}

  rarityColor(r: number): string {
    return RARITY_COLORS[r] ?? '#fff';
  }

  skinFor(w: WaifuDef): SkinDef {
    const selectedId = this.api.account()?.selectedSkins?.[w.id];
    return w.skins.find((s) => s.id === selectedId) ?? w.skins[0];
  }

  async pick(waifuId: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await this.api.pinWaifu(waifuId);
    } catch (err) {
      console.error(err);
    } finally {
      this.busy.set(false);
    }
  }

  async claim(id: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await this.api.claimDaily(id);
    } catch (err) {
      console.error(err);
    } finally {
      this.busy.set(false);
    }
  }
}
