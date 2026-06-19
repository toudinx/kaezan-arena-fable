import { Component, OnInit, computed, isDevMode } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AssetsService } from '../core/assets.service';
import { CurrencyPill } from '../core/ui/currency-pill';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CurrencyPill],
  template: `
    <header class="topbar">
      <a routerLink="/" class="brand" aria-label="Kaezan Arena Fable - Início">
        <span class="brand-mark">K</span>
        <span class="brand-copy">
          <strong>Kaezan</strong>
          <small>Arena Fable</small>
        </span>
      </a>

      <nav class="main-nav" aria-label="Navegação principal">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Início</a>
        <a routerLink="/hunt" routerLinkActive="active">Caçada</a>
        <a routerLink="/kaelis" routerLinkActive="active">Kaelis</a>
        <a routerLink="/recruit" routerLinkActive="active" class="reward">Recrutar</a>
        <a routerLink="/backpack" routerLinkActive="active">Mochila</a>
        <a routerLink="/bestiary" routerLinkActive="active">Bestiário</a>
      </nav>

      <div class="status">
        @if (account(); as acc) {
          <span class="level-pill" title="Nível de conta">
            <span>Conta</span>
            <strong>Lv. {{ acc.accountLevel }}</strong>
          </span>
          <currency-pill icon="🪙" label="Ouro" [value]="acc.gold" />
          <currency-pill icon="✦" label="Kaeros" tone="gold" [value]="acc.kaeros" />

          <details class="tools" routerLinkActive="admin-active">
            <summary title="Ferramentas" aria-label="Ferramentas">⚙</summary>
            <div class="tools-menu">
              <a routerLink="/admin" routerLinkActive="active" class="tool-link">
                <span>Admin</span>
                <small>Ferramental</small>
              </a>
              @if (devMode) {
                <button class="tool-link dev-grant" type="button" title="[DEV] +1600 Kaeros (10 pulls)" (click)="addKaeros()">
                  <span>+1600 Kaeros</span>
                  <small>Dev</small>
                </button>
              }
            </div>
          </details>
        }
      </div>
    </header>
    <main><router-outlet /></main>
  `,
  styles: [`
    .topbar {
      display: flex;
      align-items: center;
      gap: clamp(14px, 2vw, 28px);
      min-height: 56px;
      padding: 8px clamp(14px, 2.5vw, 28px);
      position: sticky;
      top: 0;
      z-index: 50;
      background: linear-gradient(180deg, rgba(14, 14, 24, 0.9), rgba(12, 12, 21, 0.72));
      -webkit-backdrop-filter: blur(22px) saturate(1.25);
      backdrop-filter: blur(22px) saturate(1.25);
      border-bottom: 1px solid var(--line-strong);
      box-shadow: var(--glass-edge), 0 12px 38px rgba(0, 0, 0, 0.28);
    }

    .brand {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      min-width: max-content;
      color: var(--text);
      text-decoration: none;
    }
    .brand-mark {
      width: 34px;
      height: 34px;
      border-radius: var(--r-md);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-family: var(--font-display);
      font-size: 20px;
      font-weight: 800;
      color: var(--gold-bright);
      background:
        radial-gradient(circle at 30% 20%, rgba(255, 210, 122, 0.24), transparent 42%),
        linear-gradient(145deg, rgba(123, 107, 242, 0.28), rgba(232, 169, 60, 0.12));
      border: 1px solid color-mix(in srgb, var(--gold) 36%, transparent);
      box-shadow: var(--glass-edge);
    }
    .brand-copy {
      display: flex;
      flex-direction: column;
      line-height: 1.05;
    }
    .brand-copy strong {
      font-family: var(--font-display);
      font-size: 1rem;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .brand-copy small {
      color: var(--text-mute);
      font-size: 0.62rem;
      font-weight: 700;
      letter-spacing: var(--tracking-eyebrow);
      text-transform: uppercase;
    }

    .main-nav {
      display: flex;
      align-items: center;
      gap: 4px;
      flex: 1;
      min-width: 0;
    }
    .main-nav a {
      position: relative;
      color: var(--text-dim);
      text-decoration: none;
      padding: 8px 12px;
      border-radius: var(--r-full);
      font-weight: 700;
      font-size: var(--fs-sm);
      white-space: nowrap;
      transition:
        color var(--dur-fast) var(--ease-out),
        background var(--dur-fast) var(--ease-out),
        box-shadow var(--dur) var(--ease-out);
    }
    .main-nav a:hover {
      color: var(--text);
      background: rgba(255, 255, 255, 0.04);
    }
    .main-nav a.active {
      color: var(--accent-bright);
      background: color-mix(in srgb, var(--accent) 16%, transparent);
      box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--accent) 34%, transparent);
    }
    .main-nav a.reward.active {
      color: var(--gold-bright);
      background: color-mix(in srgb, var(--gold) 14%, transparent);
      box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--gold) 30%, transparent);
    }

    .status {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
      min-width: max-content;
    }
    .level-pill {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      padding: 5px 12px;
      border-radius: var(--r-full);
      color: var(--text);
      background: var(--glass-bg);
      border: 1px solid var(--line-strong);
      box-shadow: var(--glass-edge);
      font-size: var(--fs-sm);
      font-weight: 700;
    }
    .level-pill span {
      color: var(--text-mute);
      font-size: var(--fs-xs);
      text-transform: uppercase;
      letter-spacing: 0.09em;
    }
    .level-pill strong {
      color: var(--accent-bright);
      font-size: var(--fs-sm);
    }

    .tools {
      position: relative;
    }
    .tools summary {
      list-style: none;
      width: 34px;
      height: 34px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border-radius: var(--r-full);
      color: var(--text-dim);
      background: var(--glass-bg);
      border: 1px solid var(--line-strong);
      box-shadow: var(--glass-edge);
      cursor: pointer;
      transition:
        color var(--dur-fast) var(--ease-out),
        border-color var(--dur-fast) var(--ease-out),
        box-shadow var(--dur) var(--ease-out);
    }
    .tools summary::-webkit-details-marker {
      display: none;
    }
    .tools summary:hover,
    .tools[open] summary,
    .tools.admin-active summary {
      color: var(--gold-bright);
      border-color: color-mix(in srgb, var(--gold) 44%, transparent);
      box-shadow: var(--glass-edge), var(--sh-gold);
    }
    .tools-menu {
      position: absolute;
      right: 0;
      top: calc(100% + 10px);
      min-width: 190px;
      padding: 8px;
      border-radius: var(--r-md);
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(24px) saturate(1.25);
      backdrop-filter: blur(24px) saturate(1.25);
      border: 1px solid var(--line-strong);
      box-shadow: var(--glass-edge), var(--sh-3);
    }
    .tool-link {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 14px;
      padding: 9px 10px;
      border-radius: var(--r-sm);
      color: var(--text);
      background: transparent;
      border: none;
      text-align: left;
      text-decoration: none;
      font-weight: 700;
      font-size: var(--fs-sm);
      transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
    }
    .tool-link small {
      color: var(--text-mute);
      font-size: var(--fs-xs);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }
    .tool-link:hover,
    .tool-link.active {
      color: var(--gold-bright);
      background: color-mix(in srgb, var(--gold) 12%, transparent);
    }
    .dev-grant {
      margin-top: 4px;
      border-top: 1px solid var(--line);
    }

    main {
      min-height: calc(100vh - 56px);
    }

    @media (max-width: 980px) {
      .topbar {
        align-items: stretch;
        flex-wrap: wrap;
      }
      .main-nav {
        order: 3;
        flex-basis: 100%;
        overflow-x: auto;
        padding-bottom: 2px;
      }
      .status {
        margin-left: auto;
      }
    }

    @media (max-width: 640px) {
      .topbar {
        gap: 8px;
        padding: 8px 10px;
      }
      .brand-copy small {
        display: none;
      }
      .main-nav a {
        padding: 7px 10px;
      }
      .level-pill span,
      currency-pill:first-of-type {
        display: none;
      }
      .tools-menu {
        right: -2px;
      }
    }
  `],
})
export class Shell implements OnInit {
  readonly account = computed(() => this.api.account());
  readonly devMode = isDevMode();

  constructor(
    private readonly api: ApiService,
    private readonly assets: AssetsService,
  ) {}

  addKaeros(): void {
    void this.api.grantKaeros(1600);
  }

  ngOnInit(): void {
    void this.api.loadCatalog();
    void this.api.refreshAccount();
    void this.assets.load();
  }
}
