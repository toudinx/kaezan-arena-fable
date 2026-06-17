import { Component, ElementRef, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { GAME_MODES, GameModeDef } from '../../core/game-modes';

@Component({
  selector: 'app-hunt',
  standalone: true,
  template: `
    <div class="page">
      <h1>Caçada</h1>
      <p class="sub">Escolha um modo de jogo. Cada modo tem suas próprias regras e recompensas.</p>

      <div class="carousel-wrap">
        <button class="nav prev" (click)="scroll(-1)" aria-label="Anterior">‹</button>
        <div class="mode-track" #track>
          @for (m of modes; track m.id) {
            <button class="mode-card"
                    [class.soon]="m.status === 'soon'"
                    [style.--mt]="m.theme"
                    [disabled]="m.status === 'soon'"
                    (click)="enter(m)">
              <span class="mode-icon">{{ m.icon }}</span>
              <span class="mode-name">{{ m.name }}</span>
              <span class="mode-tagline">{{ m.tagline }}</span>
              <p class="mode-desc">{{ m.description }}</p>
              @if (m.status === 'soon') {
                <span class="soon-badge">EM BREVE</span>
              } @else {
                <span class="cta">Jogar ›</span>
              }
            </button>
          }
        </div>
        <button class="nav next" (click)="scroll(1)" aria-label="Próximo">›</button>
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1100px; margin: 0 auto; padding: 24px; }
    .sub { color: #9c9ab0; margin-bottom: 28px; }

    .carousel-wrap { position: relative; display: flex; align-items: stretch; gap: 8px; }
    .mode-track {
      display: flex; gap: 14px; overflow-x: auto; scroll-snap-type: x mandatory;
      scroll-behavior: smooth; padding: 4px 2px 14px; flex: 1;
    }
    .mode-track::-webkit-scrollbar { height: 6px; }
    .mode-track::-webkit-scrollbar-thumb { background: #2a2a3e; border-radius: 3px; }
    .nav {
      flex-shrink: 0; width: 40px; align-self: center; height: 64px;
      background: #13131e; border: 1px solid #2c2c3e; border-radius: 10px;
      color: #9c9ab0; font-size: 24px; font-weight: 800; cursor: pointer;
    }
    .nav:hover { border-color: #2dd4bf; color: #2dd4bf; }

    .mode-card {
      flex: 0 0 300px; scroll-snap-align: start; text-align: left; cursor: pointer;
      background: linear-gradient(160deg, #15151f 0%, #0f0f18 100%);
      border: 2px solid #26263a; border-radius: 16px; padding: 22px 22px 18px;
      color: inherit; display: flex; flex-direction: column; gap: 5px; position: relative;
      min-height: 230px; transition: border-color 0.15s, transform 0.12s;
    }
    .mode-card:not(:disabled):hover { transform: translateY(-4px); border-color: var(--mt); }
    .mode-card.soon { opacity: 0.6; cursor: default; }
    .mode-icon { font-size: 34px; margin-bottom: 4px; }
    .mode-name { font-size: 22px; font-weight: 800; color: #fff; }
    .mode-tagline { font-size: 12px; color: var(--mt); font-weight: 700; }
    .mode-desc { margin: 8px 0 0; color: #9c9ab0; font-size: 13px; line-height: 1.5; flex: 1; }
    .cta { color: var(--mt); font-weight: 800; font-size: 14px; margin-top: 8px; }
    .soon-badge {
      position: absolute; top: 16px; right: 16px; font-size: 9px; font-weight: 800;
      border-radius: 5px; padding: 3px 8px; letter-spacing: 0.5px;
      background: #1e1e2c; color: #707088; border: 1px solid #3a3a52;
    }
  `],
})
export class HuntPage {
  @ViewChild('track') track!: ElementRef<HTMLDivElement>;
  readonly modes = GAME_MODES;

  constructor(private readonly router: Router) {}

  enter(mode: GameModeDef): void {
    if (mode.status !== 'live') return;
    void this.router.navigate(['/hunt', mode.id]);
  }

  scroll(dir: number): void {
    this.track?.nativeElement.scrollBy({ left: dir * 314, behavior: 'smooth' });
  }
}
