import { AfterViewInit, Component, ElementRef, OnChanges, ViewChild, input } from '@angular/core';
import { ApiService } from './api.service';
import { AssetsService } from './assets.service';

/** Static Tibia item sprite (object category) rendered into a small canvas. */
@Component({
  selector: 'app-item-icon',
  standalone: true,
  template: `<canvas #cv [width]="size()" [height]="size()" [style.width.px]="size()" [style.height.px]="size()"></canvas>`,
  styles: [`:host { display: inline-block; line-height: 0; } canvas { image-rendering: pixelated; }`],
})
export class ItemIcon implements AfterViewInit, OnChanges {
  itemId = input.required<number>();
  size = input(48);

  private readonly tierFrameColors: Record<number, string> = {
    1: '#8cbf4d',
    2: '#d99a3c',
    3: '#a662ff',
    4: '#ff6a3d',
    5: '#7b6bf2',
  };

  @ViewChild('cv') cv!: ElementRef<HTMLCanvasElement>;

  constructor(
    private readonly assets: AssetsService,
    private readonly api: ApiService,
  ) {}

  ngAfterViewInit(): void {
    this.render();
  }

  ngOnChanges(): void {
    if (this.cv) this.render();
  }

  private render(): void {
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
          this.assets.drawObject(ctx, item?.appearanceItemId ?? this.itemId(), 0, 0, this.size() / 32);
        }
        this.drawTierFrame(ctx, item?.tier ?? 0);
        if (item?.tag === 'relic') this.drawRelicFrame(ctx);
        if (++tries < 10) setTimeout(tick, 200);
      };
      tick();
    });
  }

  private drawTierFrame(ctx: CanvasRenderingContext2D, tier: number): void {
    const color = this.tierFrameColors[tier];
    if (!color) return;

    const size = this.size();
    const inset = Math.max(1, Math.round(size * 0.06));
    ctx.save();
    ctx.lineWidth = Math.max(2, Math.round(size * 0.06));
    ctx.strokeStyle = color;
    ctx.shadowColor = color;
    ctx.shadowBlur = Math.max(2, Math.round(size * 0.08));
    ctx.strokeRect(inset, inset, size - inset * 2, size - inset * 2);
    ctx.restore();
  }

  private drawRelicFrame(ctx: CanvasRenderingContext2D): void {
    const size = this.size();
    const inset = Math.max(3, Math.round(size * 0.12));
    ctx.save();
    ctx.lineWidth = Math.max(1, Math.round(size * 0.035));
    ctx.strokeStyle = '#ffd36a';
    ctx.shadowColor = '#ffd36a';
    ctx.shadowBlur = Math.max(3, Math.round(size * 0.12));
    ctx.strokeRect(inset, inset, size - inset * 2, size - inset * 2);
    ctx.fillStyle = '#ffd36a';
    const mark = Math.max(3, Math.round(size * 0.10));
    ctx.fillRect(size - inset - mark, inset - 1, mark, Math.max(2, Math.round(mark * 0.35)));
    ctx.restore();
  }
}
