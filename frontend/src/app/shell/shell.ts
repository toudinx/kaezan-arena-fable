import { Component, OnInit, computed } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AssetsService } from '../core/assets.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a routerLink="/" class="logo">KAEZAN <span>ARENA FABLE</span></a>
      <nav>
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Início</a>
        <a routerLink="/hunt" routerLinkActive="active">Caçada</a>
        <a routerLink="/kaelis" routerLinkActive="active">Kaelis</a>
        <a routerLink="/recruit" routerLinkActive="active">Recrutar</a>
        <a routerLink="/backpack" routerLinkActive="active">Mochila</a>
      </nav>
      <div class="currencies">
        @if (account(); as acc) {
          <span class="cur lvl" title="Nível de conta">Lv. {{ acc.accountLevel }}</span>
          <span class="cur gold" title="Ouro">🪙 {{ acc.gold }}</span>
          <span class="cur kaeros" title="Kaeros">✦ {{ acc.kaeros }}</span>
        }
      </div>
    </header>
    <main><router-outlet /></main>
  `,
  styles: [`
    .topbar {
      display: flex; align-items: center; gap: 28px;
      padding: 10px 24px;
      background: rgba(12, 12, 20, 0.95);
      border-bottom: 1px solid #26263a;
      position: sticky; top: 0; z-index: 50;
    }
    .logo { font-size: 18px; font-weight: 800; color: #2dd4bf; text-decoration: none; letter-spacing: 1px; }
    .logo span { color: #e8a93c; }
    nav { display: flex; gap: 4px; flex: 1; }
    nav a {
      color: #9c9ab0; text-decoration: none; padding: 8px 16px; border-radius: 8px;
      font-weight: 600; font-size: 14px; transition: background 0.15s, color 0.15s;
    }
    nav a:hover { color: #fff; background: #1d1d2c; }
    nav a.active { color: #2dd4bf; background: #16242a; }
    .currencies { display: flex; gap: 12px; }
    .cur { font-weight: 700; font-size: 14px; padding: 6px 12px; border-radius: 8px; background: #16161f; }
    .cur.gold { color: #fbbf24; }
    .cur.kaeros { color: #c084fc; }
    .cur.lvl { color: #7df0ff; }
    main { min-height: calc(100vh - 53px); }
  `],
})
export class Shell implements OnInit {
  readonly account = computed(() => this.api.account());

  constructor(
    private readonly api: ApiService,
    private readonly assets: AssetsService,
  ) {}

  ngOnInit(): void {
    void this.api.loadCatalog();
    void this.api.refreshAccount();
    void this.assets.load();
  }
}
