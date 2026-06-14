import { AfterViewInit, Component, ElementRef, ViewChild, effect, input } from '@angular/core';
import { AssetsService } from './assets.service';

/**
 * Static (single-frame) Tibia outfit thumbnail. Unlike {@link OutfitPreview} it does not run an
 * animation loop, so it is safe to render dozens at once in the Outfit Studio lists.
 */
@Component({
  selector: 'app-outfit-thumb',
  standalone: true,
  template: `<canvas #cv [width]="size()" [height]="size()"></canvas>`,
  styles: [`
    :host { display: inline-grid; place-items: center; line-height: 0; }
    canvas { display: block; image-rendering: pixelated; }
  `],
})
export class OutfitThumb implements AfterViewInit {
  lookType = input.required<number>();
  head = input(0);
  body = input(0);
  legs = input(0);
  feet = input(0);
  addons = input(0);
  mountLookType = input(0);
  size = input(52);
  dir = input(2);

  @ViewChild('cv') private cv!: ElementRef<HTMLCanvasElement>;

  constructor(private readonly assets: AssetsService) {
    effect(() => {
      this.lookType(); this.head(); this.body(); this.legs(); this.feet();
      this.addons(); this.mountLookType(); this.size(); this.dir();
      if (this.cv) void this.render();
    });
  }

  ngAfterViewInit(): void {
    void this.render();
  }

  private async render(): Promise<void> {
    await this.assets.load();
    const entry = this.assets.entry('outfits', this.lookType());
    if (entry) await this.assets.image(entry.file).catch(() => undefined);
    const mount = this.mountLookType() > 0 ? this.assets.entry('outfits', this.mountLookType()) : null;
    if (mount) await this.assets.image(mount.file).catch(() => undefined);

    const canvas = this.cv.nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.imageSmoothingEnabled = false;
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    this.assets.drawOutfitFitted(
      ctx, this.lookType(), this.size(), this.dir(), false, 0,
      this.head(), this.body(), this.legs(), this.feet(), this.addons(), this.mountLookType(),
    );
  }
}
