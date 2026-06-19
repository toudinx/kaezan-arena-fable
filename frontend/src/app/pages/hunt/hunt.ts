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
    .page {
      max-width: 1100px;
      margin: 0 auto;
      padding: var(--sp-6) var(--sp-5);
    }
    h1 { margin-bottom: var(--sp-2); }
    .sub { color: var(--text-dim); margin: 0 0 var(--sp-6); max-width: 680px; }

    .carousel-wrap { position: relative; display: flex; align-items: stretch; gap: var(--sp-3); }
    .mode-track {
      display: flex; gap: var(--sp-4); overflow-x: auto; scroll-snap-type: x mandatory;
      scroll-behavior: smooth; padding: var(--sp-1) 2px var(--sp-4); flex: 1;
    }
    .mode-track::-webkit-scrollbar { height: 6px; }
    .mode-track::-webkit-scrollbar-thumb { background: var(--bg-4); border-radius: var(--r-full); }
    .nav {
      flex-shrink: 0; width: 40px; align-self: center; height: 64px;
      background: var(--glass-bg); border: 1px solid var(--line-strong); border-radius: var(--r-md);
      box-shadow: var(--glass-edge), var(--sh-1);
      color: var(--text-dim); font-size: 24px; font-weight: 800; cursor: pointer;
      transition: border-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out),
        transform var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .nav:hover { border-color: var(--accent); color: var(--accent-bright); transform: translateY(-1px); box-shadow: var(--glass-edge), var(--sh-accent); }

    .mode-card {
      flex: 0 0 300px; scroll-snap-align: start; text-align: left; cursor: pointer;
      background:
        linear-gradient(160deg, color-mix(in srgb, var(--mt) 16%, transparent), transparent 48%),
        var(--glass-bg);
      -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      backdrop-filter: blur(var(--glass-blur)) saturate(1.25);
      border: 1px solid var(--line-strong); border-radius: var(--r-lg); padding: var(--sp-5) var(--sp-5) var(--sp-4);
      box-shadow: var(--glass-edge), var(--sh-2);
      color: inherit; display: flex; flex-direction: column; gap: 5px; position: relative;
      min-height: 230px; transition: border-color var(--dur-fast) var(--ease-out),
        transform var(--dur-fast) var(--ease-out), box-shadow var(--dur) var(--ease-out);
    }
    .mode-card:not(:disabled):hover { transform: translateY(-4px); border-color: color-mix(in srgb, var(--mt) 70%, white 10%); box-shadow: var(--glass-edge), 0 14px 38px color-mix(in srgb, var(--mt) 24%, transparent); }
    .mode-card:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 3px; }
    .mode-card.soon { opacity: 0.6; cursor: default; }
    .mode-icon { font-size: 34px; margin-bottom: 4px; }
    .mode-name { font-family: var(--font-display); font-size: 23px; font-weight: 650; color: var(--text); line-height: var(--lh-tight); }
    .mode-tagline { font-size: var(--fs-xs); color: var(--mt); font-weight: 800; text-transform: uppercase; letter-spacing: 0.08em; }
    .mode-desc { margin: var(--sp-2) 0 0; color: var(--text-dim); font-size: var(--fs-sm); line-height: 1.5; flex: 1; }
    .cta { color: var(--mt); font-weight: 800; font-size: 14px; margin-top: 8px; }
    .soon-badge {
      position: absolute; top: var(--sp-4); right: var(--sp-4); font-size: 9px; font-weight: 800;
      border-radius: var(--r-full); padding: 3px 8px; letter-spacing: 0.08em;
      background: var(--bg-3); color: var(--text-mute); border: 1px solid var(--line-strong);
    }

    @media (max-width: 720px) {
      .page { padding: var(--sp-5) var(--sp-4); }
      .nav { display: none; }
      .mode-card { flex-basis: 82vw; }
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
