import { Component, booleanAttribute, input } from '@angular/core';

/**
 * Cartão de vidro do design system, com a "crystal edge" (hairline de luz no topo)
 * e header opcional. Conteúdo via projeção; slot [header] para ações à direita.
 *
 *   <ui-panel header="Contratos Diários">
 *     <span ngProjectAs="actions">reseta 00:00</span>
 *     ...conteúdo...
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
  /** título principal (display font) */
  header = input<string>('');
  /** overline acima do título */
  eyebrow = input<string>('');
  /** vidro mais opaco/elevado */
  solid = input(false, { transform: booleanAttribute });
}
