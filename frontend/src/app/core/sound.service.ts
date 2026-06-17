import { Injectable, signal } from '@angular/core';

/**
 * Minimal WebAudio SFX synth — no assets, no deps. The first sound in the game.
 * Lazily creates an AudioContext on first use (after a user gesture) and
 * synthesizes short blips, so there's nothing to download.
 */
const MUTE_KEY = 'kaezan_muted';

@Injectable({ providedIn: 'root' })
export class SoundService {
  readonly muted = signal(localStorage.getItem(MUTE_KEY) === '1');

  private ctx: AudioContext | null = null;
  private broken = false;

  toggleMute(): void {
    this.muted.update(v => {
      const next = !v;
      localStorage.setItem(MUTE_KEY, next ? '1' : '0');
      return next;
    });
  }

  private ac(): AudioContext | null {
    if (this.broken) return null;
    if (!this.ctx) {
      const Ctor = window.AudioContext
        ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!Ctor) { this.broken = true; return null; }
      try { this.ctx = new Ctor(); } catch { this.broken = true; return null; }
    }
    if (this.ctx.state === 'suspended') void this.ctx.resume();
    return this.ctx;
  }

  /** "cha-ching" coin blip — two bright notes; pitch climbs with the chain index. */
  coinChing(chainIndex = 0): void {
    if (this.muted()) return;
    const ctx = this.ac();
    if (!ctx) return;
    const base = 880 * Math.pow(2, Math.min(chainIndex, 10) / 24); // climb ~half octave max
    this.blip(ctx, base, ctx.currentTime, 0.05);
    this.blip(ctx, base * 1.5, ctx.currentTime + 0.055, 0.09);
  }

  private blip(ctx: AudioContext, freq: number, at: number, dur: number): void {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(freq, at);
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(0.1, at + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + dur);
    osc.connect(gain).connect(ctx.destination);
    osc.start(at);
    osc.stop(at + dur + 0.02);
  }
}
