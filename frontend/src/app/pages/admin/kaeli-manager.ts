import { Component, signal } from '@angular/core';
import { KaeliStudio, StudioSeed } from './kaeli-studio';
import { KaeliWardrobe } from './kaeli-wardrobe';

/**
 * Host da aba Kaelis: alterna entre o <b>Guarda-roupa</b> (gestão de skins por Kaeli) e o
 * <b>Outfit Studio</b> (desenho visual). O guarda-roupa é a entrada; "Nova skin"/"Editar visual"
 * abrem o estúdio já apontado à Kaeli/skin via {@link StudioSeed}, e o estúdio volta pelo evento
 * <code>closed</code>. Trocar de visão remonta o componente alvo, então o guarda-roupa relê o
 * catálogo (recarregado a cada salvar) e reflete as mudanças.
 */
@Component({
  selector: 'app-kaeli-manager',
  standalone: true,
  imports: [KaeliWardrobe, KaeliStudio],
  template: `
    <div class="sub-tabs">
      <button type="button" [class.active]="view() === 'wardrobe'" (click)="showWardrobe()">Guarda-roupa</button>
      <button type="button" [class.active]="view() === 'studio'" (click)="showStudio()">Outfit Studio</button>
    </div>

    @if (view() === 'wardrobe') {
      <app-kaeli-wardrobe [initialWaifuId]="lastWaifuId()"
        (waifuSelected)="lastWaifuId.set($event)" (openStudio)="openStudio($event)" />
    } @else {
      <app-kaeli-studio [seed]="seed()" (closed)="showWardrobe()" />
    }
  `,
  styles: [`
    :host { display: block; }
    .sub-tabs { background: #0f0f17; border: 1px solid #303043; border-radius: 5px; display: inline-flex; margin-bottom: 14px; overflow: hidden; }
    .sub-tabs button { background: transparent; border: 0; color: #9290a4; cursor: pointer; font: inherit; font-size: 11px; font-weight: 900; min-height: 34px; min-width: 130px; }
    .sub-tabs button + button { border-left: 1px solid #303043; }
    .sub-tabs button.active { background: #1b433d; color: #64ead6; }
  `],
})
export class KaeliManager {
  readonly view = signal<'wardrobe' | 'studio'>('wardrobe');
  readonly seed = signal<StudioSeed | null>(null);
  /** Última Kaeli vista no guarda-roupa, preservada ao ir/voltar do estúdio. */
  readonly lastWaifuId = signal('');

  showWardrobe(): void {
    this.seed.set(null);
    this.view.set('wardrobe');
  }

  /** Abre o estúdio "em branco" (botão da sub-aba): nova skin para a primeira Kaeli. */
  showStudio(): void {
    this.seed.set(null);
    this.view.set('studio');
  }

  openStudio(seed: StudioSeed): void {
    this.lastWaifuId.set(seed.waifuId);
    this.seed.set(seed);
    this.view.set('studio');
  }
}
