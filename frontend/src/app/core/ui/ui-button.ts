import { Component, booleanAttribute, input, output } from '@angular/core';

/**
 * Botão premium do design system. Variantes: primary (íris/UI), gold (recompensa),
 * ghost (vidro). Estados loading/disabled. Usa os tokens globais de styles.css.
 *
 *   <ui-button variant="gold" [loading]="busy()" (act)="pull()">Convocar ×10</ui-button>
 */
@Component({
  selector: 'ui-button',
  standalone: true,
  template: `
    <button
      [class]="'btn ' + cls()"
      [disabled]="disabled() || loading()"
      [attr.aria-busy]="loading() || null"
      [type]="type()"
      (click)="onClick($event)">
      @if (loading()) {
        <span class="spinner" aria-hidden="true"></span>
      }
      <span class="label" [class.dim]="loading()"><ng-content /></span>
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }
    .btn { width: 100%; }
    .ghost {
      background: transparent;
      color: var(--text);
      border: 1px solid var(--line-strong);
      box-shadow: none;
    }
    .ghost:hover:not(:disabled) { border-color: var(--accent); background: rgba(255,255,255,0.03); filter: none; box-shadow: var(--sh-accent); }
    .label { display: inline-flex; align-items: center; gap: var(--sp-2); }
    .label.dim { opacity: 0.65; }
    .spinner {
      width: 14px; height: 14px; border-radius: 50%;
      border: 2px solid rgba(0,0,0,0.25);
      border-top-color: currentColor;
      animation: ui-spin 0.7s linear infinite;
    }
    @keyframes ui-spin { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { .spinner { animation-duration: 1.4s; } }
  `],
})
export class UiButton {
  /** primary | gold | ghost (secondary = alias de ghost) */
  variant = input<'primary' | 'gold' | 'ghost' | 'secondary'>('primary');
  loading = input(false, { transform: booleanAttribute });
  disabled = input(false, { transform: booleanAttribute });
  type = input<'button' | 'submit'>('button');
  /** emitido no clique quando não está disabled/loading */
  act = output<MouseEvent>();

  cls(): string {
    const v = this.variant();
    if (v === 'gold') return 'gold';
    if (v === 'ghost' || v === 'secondary') return 'ghost';
    return '';
  }

  onClick(e: MouseEvent): void {
    if (this.disabled() || this.loading()) return;
    this.act.emit(e);
  }
}
