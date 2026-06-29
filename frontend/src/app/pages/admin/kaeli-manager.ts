import { Component, signal } from '@angular/core';
import { KaeliStudio, StudioSeed } from './kaeli-studio';
import { KaeliWardrobe } from './kaeli-wardrobe';

/**
 * Host for the Kaelis tab: switches between the <b>Wardrobe</b> (skin management per Kaeli) and the
 * <b>Outfit Studio</b> (visual authoring). The wardrobe is the entry point; "New skin"/"Edit visual"
 * open the studio already pointed at the Kaeli/skin through {@link StudioSeed}, and the studio
 * returns through the <code>closed</code> event. Switching views remounts the target component, so
 * the wardrobe rereads the catalog (reloaded after every save) and reflects the changes.
 */
@Component({
  selector: 'app-kaeli-manager',
  standalone: true,
  imports: [KaeliWardrobe, KaeliStudio],
  template: `
    <div class="sub-tabs">
      <button type="button" [class.active]="view() === 'wardrobe'" (click)="showWardrobe()">Wardrobe</button>
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
  /** Last Kaeli seen in the wardrobe, preserved when going to/from the studio. */
  readonly lastWaifuId = signal('');

  showWardrobe(): void {
    this.seed.set(null);
    this.view.set('wardrobe');
  }

  /** Opens a blank studio (sub-tab button): new skin for the first Kaeli. */
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
