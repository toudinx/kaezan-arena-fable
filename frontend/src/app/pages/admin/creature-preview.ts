import { AfterViewInit, Component, ElementRef, ViewChild, effect, input } from '@angular/core';
import { AssetsService } from '../../core/assets.service';
import { MonsterCatalogEntry } from '../../core/types';

/** Static Tibia creature thumbnail used by the dungeon authoring UI. */
@Component({
  selector: 'app-creature-preview',
  standalone: true,
  template: `<canvas #canvas [width]="size()" [height]="size()"></canvas>`,
  styles: [`
    :host { display: inline-grid; place-items: center; line-height: 0; }
    canvas { display: block; image-rendering: pixelated; }
  `],
})
export class CreaturePreview implements AfterViewInit {
  readonly creature = input.required<MonsterCatalogEntry>();
  readonly size = input(76);

  @ViewChild('canvas') private canvas!: ElementRef<HTMLCanvasElement>;

  constructor(private readonly assets: AssetsService) {
    effect(() => {
      this.creature();
      this.size();
      if (this.canvas) void this.render();
    });
  }

  ngAfterViewInit(): void {
    void this.render();
  }

  private async render(): Promise<void> {
    await this.assets.load();
    const creature = this.creature();
    const entry = this.assets.entry('outfits', creature.outfit.lookType);
    if (entry) await this.assets.image(entry.file).catch(() => undefined);

    const canvas = this.canvas.nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.imageSmoothingEnabled = false;
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    this.assets.drawOutfitFitted(
      ctx,
      creature.outfit.lookType,
      this.size(),
      2,
      false,
      0,
      creature.outfit.head,
      creature.outfit.body,
      creature.outfit.legs,
      creature.outfit.feet,
      creature.outfit.addons,
    );
  }
}
