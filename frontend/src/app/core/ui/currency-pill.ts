import { Component, booleanAttribute, input, output } from '@angular/core';

/**
 * Currency pill: icon + formatted value + optional "+" button.
 * The game's two currencies: Kaeros (✦, premium/aurum) and Gold (🪙).
 *
 *   <currency-pill icon="✦" tone="gold" [value]="acc.kaeros" [plus]="true" (add)="buy()" />
 */
@Component({
  selector: 'currency-pill',
  standalone: true,
  template: `
    <span class="pill" [class.gold]="tone() === 'gold'" [attr.title]="label() || null">
      <span class="ico" aria-hidden="true">{{ icon() }}</span>
      <span class="val">{{ display() }}</span>
      @if (plus()) {
        <button class="add" type="button" [attr.aria-label]="'Add ' + (label() || 'currency')" (click)="add.emit()">+</button>
      }
    </span>
  `,
  styles: [`
    :host { display: inline-flex; }
    .ico { font-size: 1.05em; line-height: 1; }
    .gold .val { color: var(--gold-bright); }
    .val { font-variant-numeric: tabular-nums; letter-spacing: 0.01em; }
    .add {
      display: inline-flex; align-items: center; justify-content: center;
      width: 20px; height: 20px; margin: -2px -4px -2px 2px;
      border-radius: var(--r-full);
      border: none;
      background: var(--accent);
      color: #0b0820; font-weight: 800; font-size: 14px; line-height: 1;
      transition: filter var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out);
    }
    .gold .add { background: var(--gold); }
    .add:hover { filter: brightness(1.12); transform: scale(1.08); }
    .add:focus-visible { outline: 2px solid var(--accent-bright); outline-offset: 2px; }
  `],
})
export class CurrencyPill {
  icon = input<string>('✦');
  value = input<number>(0);
  /** Accessible label / tooltip (for example, "Kaeros"). */
  label = input<string>('');
  /** 'gold' tints the value with aurum (used for premium). */
  tone = input<'default' | 'gold'>('default');
  plus = input(false, { transform: booleanAttribute });
  add = output<void>();

  display(): string {
    return new Intl.NumberFormat('en-US').format(this.value() ?? 0);
  }
}
