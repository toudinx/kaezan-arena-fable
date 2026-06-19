import { Component, OnDestroy, computed, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { KaeliArtService } from '../../core/kaeli-art.service';
import { OutfitPreview } from '../../core/outfit-preview';
import { RarityStars } from '../../core/ui/rarity-stars';
import { UiButton } from '../../core/ui/ui-button';
import { BannerDef, ELEMENT_LABELS, PullResult, RARITY_COLORS } from '../../core/types';

@Component({
  selector: 'app-recruit',
  standalone: true,
  imports: [OutfitPreview, RarityStars, UiButton],
  template: `
    <div class="summon">
      <div class="backdrop">
        @if (activeFeatured(); as fw) {
          @if (bannerArt(fw.id); as img) {
            <img class="hero-art" [src]="img" alt="" decoding="async" fetchpriority="high" />
          } @else {
            <img class="hero-art gradient" [src]="elementGradient(fw.element)" alt="" decoding="async" />
          }
        } @else {
          <img class="hero-art gradient" [src]="elementGradient('holy')" alt="" decoding="async" />
        }
      </div>
      <div class="scrim"></div>
      <div class="content-divider" aria-hidden="true"></div>

      <aside class="banner-rail" aria-label="Banners disponiveis">
        <span class="eyebrow">Convocacao</span>
        @for (b of banners(); track b.id) {
          <button class="banner-tab" [class.active]="b.id === activeBanner()?.id"
                  [style.--rc]="bannerRarityColor(b)"
                  [attr.aria-label]="b.name + ' - ' + featuredLabel(b)"
                  [title]="b.name"
                  (click)="selectedBannerId.set(b.id)">
            <span class="tab-thumb">
              @if (featuredWaifu(b.featuredWaifuId); as fw) {
                @if (thumb(fw.id); as t) {
                  <img [src]="t" alt="" />
                } @else {
                  <app-outfit-preview [lookType]="fw.lookType" [head]="fw.head" [body]="fw.body"
                    [legs]="fw.legs" [feet]="fw.feet" [addons]="fw.skins[0].addons ?? 0"
                    [mountLookType]="fw.skins[0].mountLookType ?? 0" [size]="44" [animate]="false" />
                }
              } @else {
                <span class="tab-glyph">✦</span>
              }
              <span class="tab-badge">{{ b.featuredWaifuId ? 'EVENT' : 'PADRAO' }}</span>
            </span>
          </button>
        }
      </aside>

      <div class="utility-row" aria-label="Ferramentas do banner">
        <button class="utility-btn" type="button" (click)="ratesOpen.set(true)" aria-label="Informacoes do banner">
          i
        </button>
      </div>

      @if (activeBanner(); as b) {
        <main class="banner-copy">
          <span class="eyebrow">Banner ativo</span>
          <h1>{{ b.name }}</h1>

          <section class="pity-strip" aria-label="Progresso de garantia">
            <div class="pity-title">
              <span>Garantia 5★ em</span>
              <strong>{{ pullsUntilFiveStar(b.id) }}</strong>
              <span>convocacoes</span>
              @if (pity(b.id).featuredGuaranteed) {
                <span class="guaranteed">Destaque garantida</span>
              }
            </div>
            <div class="guarantee-bar">
              <div class="fill" [style.width.%]="fiveStarPercent(b.id)"></div>
            </div>
            <div class="pity-meta">
              <span>{{ pity(b.id).pullsSinceFiveStar }}/80 para 5★</span>
              <span>4★ em {{ pullsUntilFourStar(b.id) }}</span>
            </div>
          </section>
        </main>

        <section class="summon-actions">
          <div class="action-row">
            <ui-button variant="ghost" [loading]="busy()" [disabled]="kaeros() < pullCost()"
                       (act)="pull(b.id, 1)">
              Convocar x1 <span class="cost">{{ pullCost() }} ✦</span>
            </ui-button>
            <ui-button variant="gold" [loading]="busy()" [disabled]="kaeros() < pullCost() * 10"
                       (act)="pull(b.id, 10)">
              Convocar x10 <span class="cost">{{ pullCost() * 10 }} ✦</span>
            </ui-button>
          </div>
        </section>
      }

      @if (activeFeatured(); as fw) {
        <aside class="featured-callout" [style.--el]="elementColor(fw.element)" aria-label="Personagem em destaque">
          <span class="featured-element">{{ elementLabel(fw.element) }}</span>
          <div class="featured-name">
            <strong>{{ fw.name }}</strong>
            <rarity-stars [rarity]="fw.rarity" [size]="16" />
          </div>
          <span class="featured-up">UP!</span>
        </aside>
      }

      @if (ratesOpen()) {
        <div class="rates-scrim" (click)="ratesOpen.set(false)"></div>
        <aside class="rates-modal glass-strong" role="dialog" aria-modal="true" aria-label="Taxas do banner">
          <header class="rates-head">
            <div>
              <span class="eyebrow">Informacoes</span>
              <h2>Taxas do banner</h2>
            </div>
            <button class="close-btn" type="button" (click)="ratesOpen.set(false)" aria-label="Fechar">x</button>
          </header>
          <div class="rate-block">
            <span class="eyebrow">Raridade</span>
            <p><b class="five">5★</b> 0.8% · soft pity a partir de 65 · garantido em 80</p>
            <p><b class="four">4★</b> 6% · garantido a cada 10</p>
          </div>
          <div class="rate-block">
            <span class="eyebrow">Destaque</span>
            <p>Banner promocional: 50% de chance da 5★ ser a personagem em destaque. Se perder, a proxima 5★ promocional fica garantida.</p>
          </div>
          <div class="rate-block">
            <span class="eyebrow">Duplicatas</span>
            <p>Duplicatas viram <b>Echo Shards</b> da personagem para Ascensao.</p>
          </div>
        </aside>
      }
    </div>

    @if (results(); as res) {
      <div class="overlay"
           (click)="onOverlay()" role="dialog" aria-modal="true"
           [attr.aria-label]="phase() === 'charge' ? 'Convocando' : 'Resultado da convocacao'">

        @if (phase() === 'charge') {
          <div class="charge" [class.intense]="topRarity() >= 5"
               [style.--rc]="rarityColor(topRarity())">
            <div class="orb-stage" aria-hidden="true">
              <span class="rays"></span>
              <span class="orb"></span>
            </div>
            <span class="charge-label eyebrow">Convocando</span>
          </div>
        } @else {
          <div class="reveal" [class.single]="!isBatch()" [class.batch]="isBatch()">
            @for (r of res; track $index) {
              <div class="card" [class.revealed]="$index < revealed()"
                   [class.top]="$index === topCardIndex()"
                   [style.--rc]="rarityColor(r.rarity)"
                   [style.--d]="($index * 70) + 'ms'">
                <span class="burst" aria-hidden="true"></span>
                <div class="inner">
                  <div class="art">
                    @if (thumb(r.waifuId); as t) {
                      <img class="portrait" [src]="t" alt="" decoding="async" />
                    } @else if (waifu(r.waifuId); as w) {
                      <app-outfit-preview [lookType]="w.lookType" [head]="w.head" [body]="w.body"
                        [legs]="w.legs" [feet]="w.feet" [addons]="w.skins[0].addons ?? 0"
                        [mountLookType]="w.skins[0].mountLookType ?? 0" [size]="spriteSize()" [animate]="false" />
                    }
                  </div>
                  <div class="meta">
                    <div class="stars" [style.color]="rarityColor(r.rarity)">{{ '★'.repeat(r.rarity) }}</div>
                    <div class="name">{{ r.name }}</div>
                    <div class="tags">
                      @if (r.isNew) { <span class="tag new">NOVA!</span> }
                      @else { <span class="tag shards">+{{ r.shardsGained }} shards</span> }
                      @if (r.wasFeatured && !isBatch()) { <span class="tag feat">DESTAQUE</span> }
                    </div>
                  </div>
                </div>
              </div>
            }
          </div>
        }

        <div class="reveal-controls" (click)="$event.stopPropagation()">
          @if (allRevealed()) {
            <button class="ctrl primary" type="button" (click)="dismiss()">Fechar</button>
          } @else {
            <button class="ctrl" type="button" (click)="skip()">Pular animacao</button>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    :host { display: block; }
    .summon {
      position: relative;
      min-height: calc(100dvh - 53px);
      overflow: hidden;
      isolation: isolate;
      padding: clamp(18px, 3vw, 36px);
    }
    .backdrop { position: absolute; inset: 0; z-index: -3; background: var(--bg-0); }
    .hero-art {
      width: 100%; height: 100%; object-fit: cover; object-position: center right;
      transform: scale(1.012);
    }
    .hero-art.gradient { object-position: center; opacity: 0.86; }
    .scrim {
      position: absolute; inset: 0; z-index: -2; pointer-events: none;
      background:
        radial-gradient(circle at 76% 44%, rgba(232,169,60,0.08), transparent 25%),
        linear-gradient(100deg, rgba(7,7,13,0.95) 0%, rgba(7,7,13,0.76) 33%, rgba(7,7,13,0.06) 72%),
        linear-gradient(0deg, rgba(7,7,13,0.78) 0%, rgba(7,7,13,0) 48%);
    }
    .content-divider {
      position: absolute;
      left: clamp(340px, 25vw, 400px);
      top: 0;
      bottom: 0;
      width: 1px;
      z-index: 1;
      pointer-events: none;
      background: linear-gradient(
        180deg,
        transparent 0%,
        rgba(255,255,255,0.08) 12%,
        rgba(255,255,255,0.24) 48%,
        rgba(232,169,60,0.18) 72%,
        transparent 100%
      );
      box-shadow: 1px 0 20px rgba(0,0,0,0.35);
    }

    .banner-rail {
      position: absolute; left: clamp(16px, 2.5vw, 32px); top: clamp(78px, 12vh, 132px);
      width: min(290px, 24vw);
      max-height: calc(100dvh - 180px);
      overflow-y: auto;
      padding-right: 6px;
      display: flex; flex-direction: column; gap: 8px; z-index: 2;
    }
    .banner-rail::-webkit-scrollbar { width: 4px; }
    .banner-rail::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.22); border-radius: var(--r-full); }
    .banner-rail > .eyebrow { margin-left: 8px; }
    .banner-tab {
      width: 100%; height: 138px; color: var(--text);
      display: flex; align-items: stretch; padding: 0;
      text-align: left; cursor: pointer;
      background: rgba(12,12,21,0.18);
      border: 1px solid transparent;
      border-radius: 3px;
      overflow: hidden;
      box-shadow: none;
      transition: transform var(--dur) var(--ease-out), border-color var(--dur) var(--ease-out), background var(--dur) var(--ease-out);
    }
    .banner-tab:hover { transform: translateX(4px); border-color: color-mix(in srgb, var(--rc) 45%, transparent); }
    .banner-tab.active {
      border-color: var(--rc);
      background: rgba(20,18,28,0.55);
      box-shadow: 0 0 0 1px color-mix(in srgb, var(--rc) 30%, transparent), 0 18px 40px rgba(0,0,0,0.32);
    }
    .tab-thumb {
      position: relative;
      width: 100%; height: 100%; flex-shrink: 0; border-radius: 2px; overflow: hidden;
      display: flex; align-items: center; justify-content: center;
      background: rgba(255,255,255,0.05);
      border: 1px solid rgba(255,255,255,0.22);
    }
    .tab-thumb img { width: 100%; height: 100%; object-fit: cover; object-position: center 24%; }
    .tab-glyph { font-size: 28px; color: var(--gold-bright); }
    .tab-badge {
      position: absolute;
      top: 8px;
      left: 8px;
      width: fit-content;
      color: #1a1000;
      background: linear-gradient(180deg, var(--gold-bright), var(--gold-deep));
      border-radius: 3px;
      padding: 1px 6px;
      font-size: 10px;
      font-weight: 900;
      letter-spacing: 0.08em;
    }

    .utility-row {
      position: absolute; left: clamp(24px, 2.8vw, 48px); bottom: clamp(22px, 5vh, 50px);
      display: flex; gap: 12px; z-index: 3;
    }
    .utility-btn {
      width: 44px; height: 44px; border-radius: 50%;
      border: 1px solid rgba(255,255,255,0.45);
      background: rgba(255,255,255,0.9);
      color: #14141f;
      font-weight: 900;
      font-size: 20px;
      cursor: pointer;
      box-shadow: 0 10px 30px rgba(0,0,0,0.35);
      transition: transform var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
    }
    .utility-btn:hover { transform: translateY(-2px); background: #fff; }

    .banner-copy {
      width: min(620px, 48vw);
      min-height: calc(100dvh - 53px - clamp(36px, 6vw, 72px));
      margin-left: clamp(330px, 29vw, 420px);
      display: flex; flex-direction: column; justify-content: center;
      padding-bottom: 116px;
      position: relative; z-index: 1;
    }
    h1 {
      font-family: var(--font-display); font-size: clamp(3rem, 7.2vw, 6.6rem);
      line-height: 0.92; margin: 10px 0 14px;
      text-shadow: 0 10px 48px rgba(0,0,0,0.7);
    }
    .pity-strip { width: min(500px, 100%); }
    .pity-title {
      display: flex; align-items: baseline; flex-wrap: wrap; gap: 8px;
      color: var(--text-dim);
      font-size: 1.03rem;
      text-shadow: 0 2px 14px rgba(0,0,0,0.55);
    }
    .pity-title strong {
      color: var(--text);
      font-family: var(--font-display);
      font-size: 2rem;
      line-height: 1;
    }
    .guaranteed {
      margin-left: 6px;
      color: #2a1700; background: linear-gradient(180deg, var(--gold-bright), var(--gold-deep));
      border-radius: var(--r-full); padding: 5px 10px;
      font-size: var(--fs-xs); font-weight: 900; white-space: nowrap;
    }
    .guarantee-bar {
      height: 7px;
      background: rgba(255,255,255,0.09);
      border-radius: var(--r-full);
      overflow: hidden;
      margin: 12px 0 8px;
      box-shadow: inset 0 0 0 1px rgba(255,255,255,0.05);
    }
    .fill {
      height: 100%;
      background: linear-gradient(90deg, var(--gold-bright), var(--gold-deep));
      border-radius: inherit;
      box-shadow: 0 0 16px color-mix(in srgb, var(--gold) 38%, transparent);
    }
    .pity-meta {
      display: flex; justify-content: space-between; gap: 18px;
      color: var(--text-mute);
      font-size: var(--fs-xs);
    }

    .summon-actions {
      position: absolute; right: clamp(24px, 3.5vw, 64px); bottom: clamp(28px, 5vh, 54px);
      width: min(520px, 43vw);
      z-index: 3;
    }
    .action-row { display: grid; grid-template-columns: 1fr 1.15fr; gap: 12px; }
    .cost { opacity: 0.8; font-weight: 600; margin-left: 4px; }

    .featured-callout {
      position: absolute;
      right: clamp(24px, 3.5vw, 64px);
      bottom: clamp(128px, 19vh, 210px);
      z-index: 3;
      display: flex;
      align-items: center;
      gap: 12px;
      min-width: 250px;
      padding: 10px 16px 10px 10px;
      color: var(--text);
      background: linear-gradient(90deg, rgba(7,7,13,0.22), rgba(7,7,13,0.74));
      border-left: 5px solid var(--el);
      box-shadow: 0 12px 34px rgba(0,0,0,0.32);
      text-shadow: 0 2px 10px rgba(0,0,0,0.7);
    }
    .featured-element {
      width: 42px;
      height: 42px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      color: var(--el);
      border: 1px solid color-mix(in srgb, var(--el) 48%, transparent);
      background: color-mix(in srgb, var(--el) 18%, rgba(7,7,13,0.64));
      border-radius: 4px;
      font-size: 10px;
      font-weight: 900;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    .featured-name {
      display: flex;
      flex-direction: column;
      gap: 5px;
      line-height: 1;
    }
    .featured-name strong {
      font-size: 1.55rem;
      font-weight: 900;
    }
    .featured-up {
      align-self: flex-start;
      color: var(--gold-bright);
      font-family: var(--font-display);
      font-size: 1.1rem;
      font-weight: 900;
      transform: rotate(-4deg);
    }

    .rates-scrim {
      position: fixed; inset: 0; z-index: 80;
      background: rgba(7,7,13,0.58);
    }
    .rates-modal {
      position: fixed; left: 50%; top: 50%; transform: translate(-50%, -50%);
      width: min(440px, calc(100vw - 32px));
      padding: 20px;
      border-radius: var(--r-lg);
      z-index: 81;
      color: var(--text-dim);
      box-shadow: var(--glass-edge), var(--sh-3);
    }
    .rates-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 18px; }
    .rates-head h2 { margin: 2px 0 0; }
    .close-btn {
      width: 34px; height: 34px; border-radius: 50%;
      border: 1px solid var(--line-strong);
      background: rgba(255,255,255,0.04);
      color: var(--text);
      cursor: pointer;
      font-weight: 800;
    }
    .close-btn:hover { border-color: var(--accent); }
    .rate-block { padding: 14px 0; border-top: 1px solid var(--line); }
    .rate-block p { margin: 6px 0 0; line-height: 1.5; }
    .five { color: var(--rarity-5); }
    .four { color: var(--rarity-4); }

    .overlay {
      position: fixed; inset: 0; z-index: 100;
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      gap: clamp(22px, 4vh, 44px);
      background: radial-gradient(circle at 50% 42%, rgba(8,8,14,0.9), rgba(3,3,7,0.98) 72%);
      animation: overlayIn var(--dur) var(--ease-out);
    }
    @keyframes overlayIn { from { opacity: 0; } to { opacity: 1; } }

    /* ---- charge: orbe que antecipa a raridade ---- */
    .charge { display: flex; flex-direction: column; align-items: center; gap: 26px; animation: chargeIn var(--dur) var(--ease-out); }
    @keyframes chargeIn { from { opacity: 0; transform: scale(0.96); } to { opacity: 1; transform: none; } }
    .orb-stage { position: relative; width: min(56vmin, 380px); height: min(56vmin, 380px); display: grid; place-items: center; }
    .rays {
      position: absolute; inset: 0; border-radius: 50%; filter: blur(3px); opacity: 0.55;
      background: conic-gradient(from 0deg,
        transparent 0deg, color-mix(in srgb, var(--rc) 60%, transparent) 14deg, transparent 40deg,
        transparent 180deg, color-mix(in srgb, var(--rc) 60%, transparent) 194deg, transparent 220deg);
      animation: spin 9s linear infinite;
    }
    .orb {
      position: relative; width: 38%; height: 38%; border-radius: 50%;
      background: radial-gradient(circle at 50% 40%, #fff 0%, color-mix(in srgb, var(--rc) 80%, #fff) 26%, var(--rc) 58%, transparent 76%);
      box-shadow: 0 0 50px 12px color-mix(in srgb, var(--rc) 60%, transparent), 0 0 140px 44px color-mix(in srgb, var(--rc) 30%, transparent);
      animation: orbPulse 1.2s var(--ease-in-out) infinite;
    }
    .charge.intense .orb { animation-duration: 0.85s; }
    .charge.intense .rays { opacity: 0.8; animation-duration: 6s; }
    @keyframes spin { to { transform: rotate(360deg); } }
    @keyframes orbPulse { 0%,100% { transform: scale(0.9); } 50% { transform: scale(1.08); } }

    /* ---- reveal: cartas ---- */
    .reveal { display: flex; flex-wrap: wrap; gap: 14px; justify-content: center; }
    .reveal.batch { max-width: 880px; }
    .card {
      position: relative; width: 150px; height: 200px;
      opacity: 0; transform: translateY(26px) scale(0.82);
      transition: opacity 0.4s var(--ease-out), transform 0.55s var(--ease-spring);
      transition-delay: var(--d, 0ms);
    }
    .reveal.single .card { width: clamp(244px, 62vmin, 320px); height: clamp(340px, 82vmin, 432px); }
    .card.revealed { opacity: 1; transform: none; }
    .burst {
      position: absolute; inset: -10px; pointer-events: none; opacity: 0;
      background: radial-gradient(circle, color-mix(in srgb, var(--rc) 70%, #fff) 0%, transparent 62%);
    }
    .card.revealed .burst { animation: burst 0.7s var(--ease-out); animation-delay: var(--d, 0ms); }
    @keyframes burst { 0% { opacity: 0.85; transform: scale(0.55); } 100% { opacity: 0; transform: scale(1.5); } }
    .inner {
      position: relative; height: 100%; border-radius: var(--r-lg); border: 1.5px solid var(--rc);
      background: linear-gradient(180deg, rgba(28,28,42,0.96), rgba(14,14,22,0.98));
      box-shadow: 0 0 18px color-mix(in srgb, var(--rc) 38%, transparent), var(--sh-2);
      display: flex; flex-direction: column; align-items: center; justify-content: flex-start; gap: 8px;
      padding: 10px; overflow: hidden;
    }
    .card.top .inner {
      box-shadow: 0 0 0 1px var(--rc), 0 0 34px color-mix(in srgb, var(--rc) 55%, transparent), 0 0 90px color-mix(in srgb, var(--rc) 28%, transparent);
    }
    .art { flex: 1 1 auto; width: 100%; min-height: 0; display: grid; place-items: center; }
    .portrait { width: 100%; height: 100%; object-fit: cover; }
    .meta { display: flex; flex-direction: column; align-items: center; gap: 3px; text-align: center; z-index: 1; }
    .stars { font-size: 14px; }
    .name { font-family: var(--font-display); font-weight: 700; font-size: 1.05rem; line-height: 1.1; }
    .reveal.single .name { font-size: 1.7rem; }
    .tags { display: flex; flex-wrap: wrap; gap: 4px; justify-content: center; margin-top: 2px; }
    .tag { font-size: 10px; font-weight: 800; letter-spacing: 0.04em; padding: 2px 7px; border-radius: var(--r-full); }
    .tag.new { color: var(--bg-0); background: var(--el-energy); }
    .tag.shards { color: var(--text-mute); }
    .tag.feat { color: #2a1700; background: linear-gradient(180deg, var(--gold-bright), var(--gold-deep)); }

    .reveal-controls { display: flex; gap: 12px; }
    .ctrl {
      padding: 9px 22px; border-radius: var(--r-full); cursor: pointer;
      border: 1px solid var(--line-strong); background: rgba(255,255,255,0.05); color: var(--text-dim);
      font: inherit; font-weight: 700; font-size: var(--fs-sm);
      transition: border-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
    }
    .ctrl:hover { border-color: var(--accent); color: var(--text); }
    .ctrl.primary { border-color: transparent; background: linear-gradient(180deg, var(--gold-bright), var(--gold-deep)); color: #2a1700; }

    @media (prefers-reduced-motion: reduce) {
      .overlay, .overlay * { animation: none !important; }
      .card { transition: none; opacity: 1; transform: none; }
    }

    @media (max-width: 1240px) {
      .content-divider { display: none; }
      .banner-rail {
        position: relative; left: auto; top: auto; transform: none;
        width: 100%; max-height: none; flex-direction: row; overflow-x: auto; overflow-y: hidden; padding-bottom: 4px;
      }
      .banner-rail > .eyebrow { display: none; }
      .banner-tab { min-width: 170px; height: 111px; }
      .tab-thumb { height: 100%; }
      .utility-row { left: 18px; bottom: 104px; }
      .featured-callout {
        right: 24px;
        bottom: 130px;
      }
      .banner-copy {
        margin-left: 0;
        width: min(640px, 100%);
        min-height: auto;
        padding-top: clamp(56px, 12vh, 120px);
        padding-bottom: 210px;
      }
      .summon-actions { width: min(640px, calc(100vw - 36px)); left: clamp(18px, 3vw, 36px); right: auto; }
    }

    @media (max-width: 720px) {
      .summon { min-height: calc(100dvh - 53px); padding: 14px; overflow-y: auto; }
      .hero-art { object-position: 62% center; opacity: 0.76; }
      .scrim {
        background:
          linear-gradient(0deg, rgba(7,7,13,0.95) 0%, rgba(7,7,13,0.76) 42%, rgba(7,7,13,0.24) 100%);
      }
      .banner-tab { min-width: 180px; height: 102px; }
      .tab-thumb { height: 100%; }
      .banner-copy { padding-top: 34px; padding-bottom: 0; }
      h1 { font-size: clamp(2.4rem, 15vw, 4rem); }
      .pity-meta { flex-direction: column; gap: 4px; }
      .utility-row, .summon-actions, .featured-callout {
        position: relative; left: auto; right: auto; bottom: auto;
        width: 100%; margin: 14px 0 0;
      }
      .featured-callout { min-width: 0; }
      .utility-row { justify-content: flex-start; }
      .action-row { grid-template-columns: 1fr; }
      .orb-stage { width: 72vmin; height: 72vmin; }
      .reveal.batch { max-height: 70dvh; overflow-y: auto; padding: 0 10px; }
      .reveal.batch .card { width: 132px; height: 178px; }
      .reveal.single .card { width: min(80vw, 280px); height: min(64vh, 380px); }
    }
  `],
})
export class RecruitPage implements OnDestroy {
  readonly banners = computed(() => this.api.catalog()?.banners ?? []);
  readonly selectedBannerId = signal<string | null>(null);
  readonly activeBanner = computed<BannerDef | null>(() => {
    const banners = this.banners();
    const selected = this.selectedBannerId();
    return banners.find((b) => b.id === selected) ?? banners[0] ?? null;
  });
  readonly activeFeatured = computed(() => {
    const b = this.activeBanner();
    return b ? this.featuredWaifu(b.featuredWaifuId) : null;
  });
  readonly pullCost = computed(() => this.api.catalog()?.pullCost ?? 160);
  readonly kaeros = computed(() => this.api.account()?.kaeros ?? 0);
  readonly busy = signal(false);
  readonly ratesOpen = signal(false);
  readonly results = signal<PullResult[] | null>(null);
  readonly revealed = signal(0);
  /** 'charge' = orbe de antecipação; 'reveal' = cartas reveladas. */
  readonly phase = signal<'charge' | 'reveal'>('charge');

  readonly isBatch = computed(() => (this.results()?.length ?? 0) > 1);
  readonly allRevealed = computed(() => this.revealed() >= (this.results()?.length ?? 0));
  readonly spriteSize = computed(() => (this.isBatch() ? 80 : 150));
  /** Maior raridade do lote — colore o orbe (dourado = 5★) antes de revelar. */
  readonly topRarity = computed(() => (this.results() ?? []).reduce((m, r) => Math.max(m, r.rarity), 3));
  /** Primeira carta com a maior raridade — ganha o destaque do lote. */
  readonly topCardIndex = computed(() => {
    const res = this.results();
    return res ? res.findIndex((r) => r.rarity === this.topRarity()) : -1;
  });

  private readonly reduceMotion =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  private timers: ReturnType<typeof setTimeout>[] = [];

  constructor(
    private readonly api: ApiService,
    private readonly art: KaeliArtService,
  ) {}

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

  bannerArt(waifuId: string): string | null {
    return this.art.banner(waifuId);
  }

  thumb(waifuId: string): string | null {
    return this.art.thumb(waifuId);
  }

  elementGradient(element: string): string {
    return this.art.elementGradient(element);
  }

  elementLabel(element: string): string {
    return ELEMENT_LABELS[element] ?? element;
  }

  elementColor(element: string): string {
    return ELEMENT_PALETTE.has(element) ? `var(--el-${element})` : 'var(--accent)';
  }

  bannerRarityColor(banner: BannerDef): string {
    const fw = this.featuredWaifu(banner.featuredWaifuId);
    return fw ? this.rarityColor(fw.rarity) : 'var(--gold)';
  }

  featuredLabel(banner: BannerDef): string {
    const fw = this.featuredWaifu(banner.featuredWaifuId);
    return fw ? `${fw.name} rate up` : 'Convocacao padrao';
  }

  fiveStarPercent(bannerId: string): number {
    return Math.min(100, (this.pity(bannerId).pullsSinceFiveStar / 80) * 100);
  }

  pullsUntilFiveStar(bannerId: string): number {
    return Math.max(1, 80 - this.pity(bannerId).pullsSinceFiveStar);
  }

  pullsUntilFourStar(bannerId: string): number {
    return Math.max(1, 10 - this.pity(bannerId).pullsSinceFourStar);
  }

  async pull(bannerId: string, count: number): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      const res = await this.api.pull(bannerId, count);
      this.openReveal(res.results);
    } catch (err) {
      alert((err as Error).message);
    } finally {
      this.busy.set(false);
    }
  }

  /** Abre o overlay na fase de carga; o orbe antecipa a raridade antes da revelação. */
  private openReveal(results: PullResult[]): void {
    this.clearTimers();
    this.results.set(results);
    this.revealed.set(0);
    this.phase.set('charge');
    if (this.reduceMotion) {
      this.startReveal();
      return;
    }
    const chargeMs = results.length > 1 ? 1600 : 1400;
    this.timers.push(setTimeout(() => this.startReveal(), chargeMs));
  }

  /** Troca para as cartas e revela em cascata (1 por vez no x10). */
  private startReveal(): void {
    this.phase.set('reveal');
    const res = this.results();
    if (!res) return;
    if (this.reduceMotion || res.length === 1) {
      this.revealed.set(res.length);
      return;
    }
    this.revealed.set(1);
    const id = setInterval(() => {
      this.revealed.update((r) => Math.min(r + 1, res.length));
      if (this.revealed() >= res.length) clearInterval(id);
    }, 200);
    this.timers.push(id);
  }

  /** Pula a animação: revela tudo imediatamente. */
  skip(): void {
    this.clearTimers();
    this.phase.set('reveal');
    this.revealed.set(this.results()?.length ?? 0);
  }

  dismiss(): void {
    this.clearTimers();
    this.results.set(null);
    this.revealed.set(0);
    this.phase.set('charge');
  }

  /** Clique no fundo: pula enquanto revela, fecha quando concluído. */
  onOverlay(): void {
    if (this.allRevealed()) this.dismiss();
    else this.skip();
  }

  private clearTimers(): void {
    for (const t of this.timers) {
      clearTimeout(t);
      clearInterval(t as unknown as ReturnType<typeof setInterval>);
    }
    this.timers = [];
  }

  ngOnDestroy(): void {
    this.clearTimers();
  }
}

const ELEMENT_PALETTE = new Set([
  'physical', 'fire', 'ice', 'energy', 'earth', 'death', 'holy',
]);
