import { Component, computed, input } from '@angular/core';
import { RARITY_COLORS } from '../types';

/**
 * Rarity stars (3★/4★/5★) colored by the --rarity-N token (aligned with
 * RARITY_COLORS in core/types.ts). 5★ gets a slight golden glow.
 *
 *   <rarity-stars [rarity]="w.rarity" [size]="18" />
 */
@Component({
  selector: 'rarity-stars',
  standalone: true,
  template: `
    <span class="stars" [class.legendary]="rarity() >= 5"
          [style.color]="color()" [style.fontSize.px]="size()"
          [attr.aria-label]="rarity() + ' stars'" role="img">{{ glyphs() }}</span>
  `,
  styles: [`
    :host { display: inline-flex; }
    .stars { letter-spacing: 0.12em; line-height: 1; filter: drop-shadow(0 1px 3px rgba(0,0,0,0.55)); }
    .stars.legendary { filter: drop-shadow(0 0 6px var(--gold-glow)); }
  `],
})
export class RarityStars {
  rarity = input.required<number>();
  size = input(16);

  readonly glyphs = computed(() => '★'.repeat(Math.max(0, this.rarity())));
  readonly color = computed(() => RARITY_COLORS[this.rarity()] ?? 'var(--text)');
}
