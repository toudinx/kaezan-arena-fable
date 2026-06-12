import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, input, effect } from '@angular/core';
import { AssetsService } from './assets.service';

/** Animated Tibia outfit preview (rotates through directions while "walking"). */
@Component({
  selector: 'app-outfit-preview',
  standalone: true,
  template: `<canvas #cv [width]="size()" [height]="size()" [style.width.px]="size()" [style.height.px]="size()"></canvas>`,
  styles: [`:host { display: inline-block; line-height: 0; } canvas { image-rendering: pixelated; }`],
})
export class OutfitPreview implements AfterViewInit, OnDestroy {
  lookType = input.required<number>();
  head = input(0);
  body = input(0);
  legs = input(0);
  feet = input(0);
  addons = input(0);
  size = input(96);
  animate = input(true);

  @ViewChild('cv') cv!: ElementRef<HTMLCanvasElement>;
  private raf = 0;

  constructor(private readonly assets: AssetsService) {
    effect(() => {
      // re-render when inputs change
      this.lookType(); this.head(); this.body(); this.legs(); this.feet(); this.addons();
    });
  }

  ngAfterViewInit(): void {
    void this.assets.load().then(() => this.loop(performance.now()));
  }

  private loop = (now: number): void => {
    const canvas = this.cv?.nativeElement;
    if (!canvas) return;
    const ctx = canvas.getContext('2d')!;
    ctx.imageSmoothingEnabled = false;
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const scale = this.size() / 48;
    const dir = this.animate() ? [2, 1, 0, 3][Math.floor(now / 1400) % 4] : 2;
    const offset = (this.size() - 32 * scale) / 2;
    ctx.save();
    ctx.translate(offset, offset);
    this.assets.drawOutfit(
      ctx, this.lookType(), 0, 0, scale, dir, this.animate(), now,
      this.head(), this.body(), this.legs(), this.feet(), this.addons(),
    );
    ctx.restore();

    this.raf = requestAnimationFrame(this.loop);
  };

  ngOnDestroy(): void {
    cancelAnimationFrame(this.raf);
  }
}
