/** Fixed-size ring of duration samples with percentile readout (debug overlay only). */
export class PerfRing {
  private samples: number[] = [];
  private next = 0;

  constructor(private readonly capacity: number) {}

  add(ms: number): void {
    if (this.samples.length < this.capacity) {
      this.samples.push(ms);
      return;
    }
    this.samples[this.next] = ms;
    this.next = (this.next + 1) % this.capacity;
  }

  percentile(p: number): number {
    if (!this.samples.length) return 0;
    const sorted = [...this.samples].sort((a, b) => a - b);
    const rank = Math.ceil((p / 100) * sorted.length) - 1;
    return sorted[Math.max(0, Math.min(rank, sorted.length - 1))];
  }
}
