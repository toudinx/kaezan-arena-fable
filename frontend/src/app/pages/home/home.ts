import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { KaeliArtService } from '../../core/kaeli-art.service';
import { OutfitPreview } from '../../core/outfit-preview';
import { RarityStars } from '../../core/ui/rarity-stars';
import { BannerDef, ELEMENT_LABELS, RARITY_COLORS, SkinDef, WaifuDef } from '../../core/types';

interface NavItem { route: string; icon: string; title: string; sub: string; tone: 'gold' | 'iris'; }

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, OutfitPreview, RarityStars],
  template: `
    <div class="hub">
      <!-- showcase: pinned Kaeli wallpaper -->
      <div class="bg">
        @if (wallpaper(); as wp) {
          <img class="wallpaper" [src]="wp" alt="" decoding="async" fetchpriority="high" />
        } @else {
          <img class="wallpaper gradient" [src]="bgGradient()" alt="" decoding="async" />
        }
      </div>
      <div class="scrim"></div>

      @if (pinnedWaifu(); as w) {
        <!-- no dedicated wallpaper: the Kaeli is not "inside" the background, so show the sprite -->
        @if (!wallpaper() && pinnedSkin(); as skin) {
          <div class="sprite-stage">
            <app-outfit-preview
              [lookType]="skin.lookType" [head]="skin.head" [body]="skin.body"
              [legs]="skin.legs" [feet]="skin.feet" [addons]="pinnedAddons()"
              [mountLookType]="pinnedMount()" [size]="260" />
          </div>
        }

        <!-- Kaeli identity (lower-left corner) -->
        <section class="identity">
          <div class="tags">
            <span class="el-tag" [style.--el]="elementColor(w.element)">{{ elementLabel(w.element) }}</span>
            <rarity-stars [rarity]="w.rarity" [size]="18" />
          </div>
          <h1 class="name">{{ w.name }}</h1>
          <p class="title">{{ w.title }}</p>
          <p class="desc">{{ w.description }}</p>

          @if (owned().length > 1) {
            <div class="pin">
              <span class="eyebrow">Featured</span>
              <div class="pin-strip">
                @for (o of owned(); track o.id) {
                  <button class="pin-thumb" [class.active]="o.id === w.id"
                          [style.--rc]="rarityColor(o.rarity)" [title]="o.name"
                          [disabled]="busy()" (click)="pick(o.id)">
                    @if (thumb(o.id); as t) {
                      <img [src]="t" alt="" />
                    } @else {
                      <app-outfit-preview [lookType]="skinFor(o).lookType" [head]="skinFor(o).head"
                        [body]="skinFor(o).body" [legs]="skinFor(o).legs" [feet]="skinFor(o).feet"
                        [addons]="skinFor(o).addons ?? 0" [size]="40" [animate]="false" />
                    }
                  </button>
                }
              </div>
            </div>
          }

          <!-- active banner CTA -->
          @if (activeBanner(); as b) {
            <a class="banner-cta glass" routerLink="/recruit">
              <span class="flare">DROP RATE UP</span>
              <div class="cta-body">
                <span class="eyebrow">Active banner</span>
                <strong>{{ b.name }}</strong>
                @if (featuredName(b); as fn) { <span class="feat">Featured · {{ fn }}</span> }
              </div>
              <span class="cta-go">Summon →</span>
            </a>
          }
        </section>
      } @else {
        <section class="identity empty">
          <h1 class="name">Your arena awaits</h1>
          <p class="desc">Recruit a Kaeli from the banner to pin her here as your protagonist.</p>
          <a class="btn gold" routerLink="/recruit">Go to Recruit</a>
        </section>
      }

      <!-- vertical navigation rail (right) -->
      <nav class="rail">
        @for (it of navItems(); track it.route) {
          <a class="rail-item glass" [class.gold]="it.tone === 'gold'" [routerLink]="it.route">
            <span class="ri-icon">{{ it.icon }}</span>
            <span class="ri-text">
              <strong>{{ it.title }}</strong>
              <small>{{ it.sub }}</small>
            </span>
          </a>
        }
      </nav>

      <!-- daily contracts: corner drawer outside the showcase -->
      <button class="dailies-fab glass" (click)="drawerOpen.set(!drawerOpen())"
              [attr.aria-expanded]="drawerOpen()">
        <span>📜 Contracts</span>
        @if (claimable() > 0) { <span class="badge">{{ claimable() }}</span> }
      </button>

      <aside class="drawer glass-strong" [class.open]="drawerOpen()" aria-label="Daily contracts">
        <header class="drawer-hd">
          <h2>Daily Contracts</h2>
          <button class="x" (click)="drawerOpen.set(false)" aria-label="Close">✕</button>
        </header>
        <p class="reset eyebrow">Resets 00:00 UTC</p>
        @for (d of dailies(); track d.id) {
          <div class="contract" [class.done]="d.progress >= d.target">
            <span class="c-desc">{{ d.description }}</span>
            <div class="bar"><div class="fill" [style.width.%]="(100 * d.progress) / d.target"></div></div>
            <div class="c-foot">
              <span class="prog">{{ d.progress }} / {{ d.target }}</span>
              @if (d.claimed) {
                <span class="claimed">✓ Claimed</span>
              } @else if (d.progress >= d.target) {
                <button class="btn gold" (click)="claim(d.id)">Claim</button>
              } @else {
                <span class="reward muted">+100 ✦ · +150 🪙</span>
              }
            </div>
          </div>
        } @empty {
          <p class="muted">Loading contracts...</p>
        }
        @if (account(); as acc) {
          <div class="acct">
            <div class="acct-top">
              <span>Account · Level {{ acc.accountLevel }}</span>
              <span class="muted">{{ acc.runsWon }}/{{ acc.runsPlayed }} wins</span>
            </div>
            <div class="bar"><div class="fill xp" [style.width.%]="(100 * acc.accountXp) / acc.accountXpNext"></div></div>
            <span class="muted">{{ acc.accountXp }} / {{ acc.accountXpNext }} XP</span>
          </div>
        }
      </aside>

      @if (drawerOpen()) { <div class="drawer-scrim" (click)="drawerOpen.set(false)"></div> }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .hub {
      position: relative;
      min-height: calc(100dvh - 53px);
      overflow: hidden;
      isolation: isolate;
    }

    /* ---- showcase ---- */
    .bg { position: absolute; inset: 0; z-index: -2; }
    .wallpaper { width: 100%; height: 100%; object-fit: cover; object-position: center 20%; }
    .wallpaper.gradient { object-position: center; }
    .scrim {
      position: absolute; inset: 0; z-index: -1; pointer-events: none;
      background:
        linear-gradient(105deg, rgba(7,7,13,0.92) 0%, rgba(7,7,13,0.55) 32%, rgba(7,7,13,0) 60%),
        linear-gradient(0deg, rgba(7,7,13,0.85) 0%, rgba(7,7,13,0) 45%);
    }
    .sprite-stage {
      position: absolute; inset: 0; z-index: -1;
      display: flex; align-items: flex-end; justify-content: center;
      padding-bottom: 6vh;
      filter: drop-shadow(0 24px 48px rgba(0,0,0,0.6));
    }

    /* ---- identity ---- */
    .identity {
      position: absolute; left: clamp(24px, 5vw, 72px); bottom: clamp(28px, 7vh, 72px);
      max-width: min(560px, 56vw); z-index: 2;
    }
    .identity.empty { top: 50%; transform: translateY(-50%); bottom: auto; }
    .tags { display: flex; align-items: center; gap: 14px; margin-bottom: 10px; }
    .el-tag {
      font-size: var(--fs-xs); font-weight: 700; text-transform: uppercase;
      letter-spacing: var(--tracking-eyebrow);
      color: var(--el); padding: 4px 12px; border-radius: var(--r-full);
      border: 1px solid color-mix(in srgb, var(--el) 50%, transparent);
      background: color-mix(in srgb, var(--el) 14%, transparent);
    }
    .name {
      font-family: var(--font-display); font-weight: 900;
      font-size: var(--fs-display); line-height: 0.95; margin: 0;
      letter-spacing: -0.01em; text-shadow: 0 4px 30px rgba(0,0,0,0.6);
    }
    .title {
      font-family: var(--font-display); font-style: italic; font-weight: 400;
      color: var(--accent-bright); font-size: 1.3rem; margin: 6px 0 14px;
    }
    .desc { color: var(--text-dim); line-height: var(--lh-body); margin: 0 0 18px; max-width: 46ch; }

    .pin { margin-bottom: 18px; }
    .pin-strip { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 8px; }
    .pin-thumb {
      width: 50px; height: 50px; padding: 0; border-radius: var(--r-md); overflow: hidden;
      background: var(--glass-bg); border: 2px solid var(--line-strong);
      display: flex; align-items: center; justify-content: center;
      transition: border-color var(--dur) var(--ease-out), transform var(--dur) var(--ease-out);
    }
    .pin-thumb img { width: 100%; height: 100%; object-fit: cover; }
    .pin-thumb:hover:not(:disabled) { border-color: var(--rc); transform: translateY(-2px); }
    .pin-thumb.active { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent), var(--sh-accent); }
    .pin-thumb:disabled { cursor: default; }

    .banner-cta {
      display: flex; align-items: center; gap: 16px; padding: 14px 18px;
      border-radius: var(--r-lg); text-decoration: none; color: var(--text);
      max-width: 460px; position: relative; overflow: hidden;
      transition: transform var(--dur) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .banner-cta:hover { transform: translateY(-2px); box-shadow: var(--glass-edge), var(--sh-gold); }
    .flare {
      position: absolute; top: 0; right: 0;
      font-size: 9px; font-weight: 800; letter-spacing: 0.12em;
      color: #2a1700; background: linear-gradient(180deg, var(--gold-bright), var(--gold-deep));
      padding: 3px 10px; border-bottom-left-radius: var(--r-md);
    }
    .cta-body { display: flex; flex-direction: column; gap: 2px; flex: 1; }
    .cta-body strong { font-family: var(--font-display); font-size: 1.05rem; }
    .cta-body .feat { font-size: var(--fs-sm); color: var(--gold-bright); }
    .cta-go { font-weight: 700; color: var(--gold-bright); white-space: nowrap; }

    /* ---- rail ---- */
    .rail {
      position: absolute; right: clamp(16px, 2.5vw, 32px); top: 50%; transform: translateY(-50%);
      display: flex; flex-direction: column; gap: 10px; z-index: 2; width: 248px;
    }
    .rail-item {
      display: flex; align-items: center; gap: 14px; padding: 12px 16px;
      border-radius: var(--r-md); text-decoration: none; color: var(--text);
      transition: transform var(--dur) var(--ease-out), border-color var(--dur) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .rail-item:hover { transform: translateX(-6px); border-color: var(--accent); box-shadow: var(--glass-edge), var(--sh-accent); }
    .rail-item.gold:hover { border-color: var(--gold); box-shadow: var(--glass-edge), var(--sh-gold); }
    .ri-icon {
      font-size: 20px; width: 40px; height: 40px; flex-shrink: 0;
      display: flex; align-items: center; justify-content: center;
      background: rgba(255,255,255,0.05); border-radius: var(--r-sm);
    }
    .rail-item.gold .ri-icon { color: var(--gold-bright); }
    .ri-text { display: flex; flex-direction: column; line-height: 1.2; }
    .ri-text strong { font-size: 1rem; }
    .ri-text small { color: var(--text-mute); font-size: var(--fs-sm); }

    /* ---- dailies drawer ---- */
    .dailies-fab {
      position: absolute; right: clamp(16px, 2.5vw, 32px); bottom: 24px; z-index: 3;
      display: inline-flex; align-items: center; gap: 8px;
      padding: 10px 16px; border-radius: var(--r-full); color: var(--text);
      font-weight: 600; font-size: var(--fs-sm); cursor: pointer;
      transition: transform var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .dailies-fab:hover { transform: translateY(-1px); box-shadow: var(--glass-edge), var(--sh-accent); }
    .badge {
      min-width: 20px; height: 20px; padding: 0 6px; border-radius: var(--r-full);
      background: var(--gold); color: #2a1700; font-weight: 800; font-size: 12px;
      display: inline-flex; align-items: center; justify-content: center;
    }
    .drawer-scrim { position: absolute; inset: 0; z-index: 4; background: rgba(7,7,13,0.5); }
    .drawer {
      position: absolute; top: 0; right: 0; bottom: 0; width: min(400px, 92vw); z-index: 5;
      padding: var(--sp-5); overflow-y: auto;
      transform: translateX(105%); transition: transform var(--dur-slow) var(--ease-out);
      border-radius: 0;
    }
    .drawer.open { transform: translateX(0); }
    .drawer-hd { display: flex; align-items: center; justify-content: space-between; }
    .drawer-hd h2 { margin: 0; }
    .x { background: none; border: none; color: var(--text-mute); font-size: 18px; cursor: pointer; }
    .x:hover { color: var(--text); }
    .reset { display: block; margin: 4px 0 18px; }
    .contract {
      padding: 12px 14px; border-radius: var(--r-md); margin-bottom: 10px;
      background: rgba(255,255,255,0.03); border: 1px solid var(--line);
    }
    .contract.done { border-color: color-mix(in srgb, var(--gold) 45%, transparent); }
    .c-desc { font-weight: 600; font-size: var(--fs-sm); }
    .bar { height: 6px; background: var(--bg-2); border-radius: var(--r-full); margin: 8px 0; overflow: hidden; }
    .fill { height: 100%; background: linear-gradient(90deg, var(--accent-bright), var(--accent-dim)); border-radius: var(--r-full); }
    .fill.xp { background: linear-gradient(90deg, var(--gold-bright), var(--gold-deep)); }
    .c-foot { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
    .c-foot .btn { padding: 6px 14px; }
    .prog { font-size: var(--fs-sm); color: var(--text-dim); }
    .claimed { color: var(--gold-bright); font-weight: 700; font-size: var(--fs-sm); }
    .acct { margin-top: 18px; padding-top: 16px; border-top: 1px solid var(--line); }
    .acct-top { display: flex; justify-content: space-between; font-size: var(--fs-sm); margin-bottom: 6px; }

    /* ---- responsive ---- */
    @media (max-width: 900px) {
      .hub { min-height: calc(100dvh - 53px); padding-bottom: 80px; }
      .identity { left: 20px; right: 20px; bottom: 84px; max-width: none; }
      .identity.empty { top: 40%; bottom: auto; }
      .name { font-size: clamp(2.2rem, 11vw, 3rem); }
      .desc { max-width: none; }
      .sprite-stage { padding-bottom: 14vh; }
      .scrim { background:
        linear-gradient(0deg, rgba(7,7,13,0.94) 0%, rgba(7,7,13,0.4) 38%, rgba(7,7,13,0) 62%); }
      .rail {
        position: fixed; right: 0; left: 0; bottom: 0; top: auto; transform: none;
        flex-direction: row; width: auto; gap: 6px; padding: 8px;
        background: var(--glass-bg-strong); -webkit-backdrop-filter: blur(20px); backdrop-filter: blur(20px);
        border-top: 1px solid var(--line-strong); overflow-x: auto; z-index: 40;
      }
      .rail-item { flex-direction: column; gap: 4px; padding: 8px 12px; border: none; box-shadow: none; min-width: 76px; text-align: center; }
      .rail-item:hover { transform: none; box-shadow: none; }
      .rail-item .ri-text small { display: none; }
      .ri-icon { width: 28px; height: 28px; font-size: 16px; }
      .dailies-fab { bottom: 88px; }
    }
  `],
})
export class HomePage {
  readonly account = computed(() => this.api.account());
  readonly dailies = computed(() => this.api.account()?.dailies ?? []);
  readonly claimable = computed(
    () => this.dailies().filter((d) => !d.claimed && d.progress >= d.target).length,
  );
  readonly drawerOpen = signal(false);

  // Home protagonist = pinned (favorite) Kaeli; otherwise the first owned one.
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
  readonly pinnedAddons = computed(() => this.pinnedSkin()?.addons ?? 0);
  readonly pinnedMount = computed(() => this.pinnedSkin()?.mountLookType ?? 0);

  // Showcase background: dedicated Kaeli wallpaper; otherwise the element gradient.
  readonly wallpaper = computed(() => {
    const w = this.pinnedWaifu();
    return w ? this.art.wallpaper(w.id) : null;
  });
  readonly bgGradient = computed(() => {
    const w = this.pinnedWaifu();
    if (!w) return this.art.elementGradient('physical');
    return this.art.bgLandscape(w.id) ?? this.art.elementGradient(w.element);
  });

  readonly owned = computed(() => {
    const acc = this.api.account();
    const cat = this.api.catalog();
    if (!acc || !cat) return [];
    return cat.waifus
      .filter((w) => acc.ownedWaifus.includes(w.id))
      .sort((a, b) => b.rarity - a.rarity || a.name.localeCompare(b.name));
  });

  readonly activeBanner = computed<BannerDef | null>(() => {
    const banners = this.api.catalog()?.banners ?? [];
    return banners.find((b) => b.featuredWaifuId) ?? banners[0] ?? null;
  });

  readonly navItems = computed<NavItem[]>(() => {
    const cat = this.api.catalog();
    const acc = this.api.account();
    return [
      { route: '/hunt', icon: '⚔', title: 'Hunt', sub: `${cat?.tiers.length ?? 0} dungeons`, tone: 'iris' },
      { route: '/kaelis', icon: '👥', title: 'Kaelis', sub: `${this.owned().length} hunters`, tone: 'iris' },
      { route: '/recruit', icon: '✦', title: 'Recruit', sub: 'Active banner', tone: 'gold' },
      { route: '/backpack', icon: '🎒', title: 'Backpack', sub: `${acc?.inventory.length ?? 0} item types`, tone: 'iris' },
      { route: '/bestiary', icon: '📖', title: 'Bestiary', sub: `${cat?.monsters.length ?? 0} creatures`, tone: 'iris' },
    ];
  });

  readonly busy = signal(false);

  constructor(
    private readonly api: ApiService,
    private readonly art: KaeliArtService,
  ) {}

  rarityColor(r: number): string { return RARITY_COLORS[r] ?? 'var(--text)'; }
  elementLabel(el: string): string { return ELEMENT_LABELS[el] ?? el; }
  elementColor(el: string): string {
    return ELEMENT_PALETTE.has(el) ? `var(--el-${el})` : 'var(--accent)';
  }
  thumb(id: string): string | null { return this.art.thumb(id); }
  skinFor(w: WaifuDef): SkinDef {
    const selectedId = this.api.account()?.selectedSkins?.[w.id];
    return w.skins.find((s) => s.id === selectedId) ?? w.skins[0];
  }
  featuredName(b: BannerDef): string | null {
    if (!b.featuredWaifuId) return null;
    return this.api.catalog()?.waifus.find((w) => w.id === b.featuredWaifuId)?.name ?? null;
  }

  async pick(waifuId: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try { await this.api.pinWaifu(waifuId); }
    catch (err) { console.error(err); }
    finally { this.busy.set(false); }
  }

  async claim(id: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try { await this.api.claimDaily(id); }
    catch (err) { console.error(err); }
    finally { this.busy.set(false); }
  }
}

const ELEMENT_PALETTE = new Set([
  'physical', 'fire', 'ice', 'energy', 'earth', 'death', 'holy',
]);
