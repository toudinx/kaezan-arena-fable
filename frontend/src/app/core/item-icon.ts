import { AfterViewInit, Component, ElementRef, ViewChild, input } from '@angular/core';
import { ApiService } from './api.service';
import { AssetsService } from './assets.service';

/** Static Tibia item sprite (object category) rendered into a small canvas. */
@Component({
  selector: 'app-item-icon',
  standalone: true,
  template: `<canvas #cv [width]="size()" [height]="size()" [style.width.px]="size()" [style.height.px]="size()"></canvas>`,
  styles: [`:host { display: inline-block; line-height: 0; } canvas { image-rendering: pixelated; }`],
})
export class ItemIcon implements AfterViewInit {
  itemId = input.required<number>();
  size = input(48);

  @ViewChild('cv') cv!: ElementRef<HTMLCanvasElement>;

  constructor(
    private readonly assets: AssetsService,
    private readonly api: ApiService,
  ) {}

  ngAfterViewInit(): void {
    void this.assets.load().then(() => {
      // small retry loop because the atlas image may still be loading
      let tries = 0;
      const tick = () => {
        const canvas = this.cv?.nativeElement;
        if (!canvas) return;
        const ctx = canvas.getContext('2d')!;
        ctx.imageSmoothingEnabled = false;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        const item = this.api.catalog()?.items.find((entry) => entry.itemId === this.itemId());
        if (item?.mountLookType) {
          const scale = this.size() / 48;
          const offset = (this.size() - 32 * scale) / 2;
          this.assets.drawOutfit(
            ctx, item.mountLookType, offset, offset, scale, 2, false, 0,
          );
        } else {
          this.assets.drawObject(ctx, this.itemId(), 0, 0, this.size() / 32);
        }
        if (++tries < 10) setTimeout(tick, 200);
      };
      tick();
    });
  }
}
