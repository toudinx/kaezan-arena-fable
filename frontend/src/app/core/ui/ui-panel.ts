import { Component, booleanAttribute, input } from '@angular/core';

/**
 * Design-system glass card with the "crystal edge" (top light hairline)
 * and optional header. Content comes through projection; [header] slot is for right-side actions.
 *
 *   <ui-panel header="Daily Contracts">
 *     <span ngProjectAs="actions">resets 00:00</span>
 *     ...content...
 *   </ui-panel>
 */
@Component({
  selector: 'ui-panel',
  standalone: true,
  template: `
    <section class="card" [class.solid]="solid()">
      @if (header() || eyebrow()) {
        <header class="hd">
          <div class="titles">
            @if (eyebrow()) { <span class="eyebrow">{{ eyebrow() }}</span> }
            @if (header()) { <h3>{{ header() }}</h3> }
          </div>
          <div class="actions"><ng-content select="[actions]" /></div>
        </header>
      }
      <div class="body"><ng-content /></div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .card {
      background: var(--glass-bg);
      -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(1.3);
      backdrop-filter: blur(var(--glass-blur)) saturate(1.3);
      border: 1px solid var(--line-strong);
      border-radius: var(--r-lg);
      box-shadow: var(--glass-edge), var(--sh-2);
      padding: var(--sp-4) var(--sp-5);
    }
    .card.solid {
      background: var(--glass-bg-strong);
      -webkit-backdrop-filter: blur(calc(var(--glass-blur) + 6px)) saturate(1.3);
      backdrop-filter: blur(calc(var(--glass-blur) + 6px)) saturate(1.3);
      box-shadow: var(--glass-edge), var(--sh-3);
    }
    .hd {
      display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sp-4);
      margin-bottom: var(--sp-4);
      padding-bottom: var(--sp-3);
      border-bottom: 1px solid var(--line);
    }
    .titles { display: flex; flex-direction: column; gap: var(--sp-1); }
    .hd h3 { margin: 0; }
    .actions { color: var(--text-mute); font-size: var(--fs-sm); }
  `],
})
export class UiPanel {
  /** Main title (display font). */
  header = input<string>('');
  /** Overline above the title. */
  eyebrow = input<string>('');
  /** More opaque/elevated glass. */
  solid = input(false, { transform: booleanAttribute });
}
