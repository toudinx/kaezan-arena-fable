import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { MapDto, SnapshotDto } from './types';

export interface JoinRunResult {
  seed: number;
  tier: number;
  tierName: string;
  waifuId: string;
  resumed: boolean;
}

/** SignalR channel for the live dungeon run. */
@Injectable({ providedIn: 'root' })
export class GameClientService {
  private connection: signalR.HubConnection | null = null;

  readonly snapshot = signal<SnapshotDto | null>(null);
  readonly map = signal<MapDto | null>(null);
  readonly connected = signal(false);

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hub/game')
      .withAutomaticReconnect()
      .build();

    this.connection.on('snapshot', (snap: SnapshotDto) => this.snapshot.set(snap));
    this.connection.on('map', (map: MapDto) => this.map.set(map));

    await this.connection.start();
    this.connected.set(true);
  }

  async joinRun(tier: number, waifuId?: string, seed?: number, resume = false): Promise<JoinRunResult> {
    this.snapshot.set(null);
    this.map.set(null);
    await this.connect();
    return this.connection!.invoke<JoinRunResult>('JoinRun', tier, waifuId ?? null, seed ?? null, resume);
  }

  async leave(abandon = false): Promise<void> {
    if (!this.connection) return;
    try {
      if (abandon && this.connection.state === signalR.HubConnectionState.Connected)
        await this.connection.invoke('Abandon');
      await this.connection.stop();
    } finally {
      this.connection = null;
      this.connected.set(false);
      this.snapshot.set(null);
      this.map.set(null);
    }
  }

  move(dx: number, dy: number): void {
    void this.connection?.invoke('Move', dx, dy).catch(() => undefined);
  }

  setTarget(actorId: number): void {
    void this.connection?.invoke('SetTarget', actorId).catch(() => undefined);
  }

  castSkill(slot: number): void {
    void this.connection?.invoke('CastSkill', slot).catch(() => undefined);
  }

  usePotion(): void {
    void this.connection?.invoke('UsePotion').catch(() => undefined);
  }

  toggleStance(): void {
    void this.connection?.invoke('ToggleStance').catch(() => undefined);
  }

  setAutoHelper(
    targeting: boolean,
    skills: boolean,
    ultimate: boolean,
    targetPreference: 'lowestHp' | 'nearest',
    movementMode: 'none' | 'follow' | 'avoid',
    autoHeal: boolean,
    autoHealPct: number,
    navMode: 'off' | 'loot',
    autoCards: boolean,
  ): void {
    void this.connection
      ?.invoke('SetAutoHelper', targeting, skills, ultimate, targetPreference, movementMode,
        autoHeal, autoHealPct, navMode, autoCards)
      .catch(() => undefined);
  }

  /** G-10: salva a config atual do helper como default da Kaeli da run. */
  saveHelperProfile(): void {
    void this.connection?.invoke('SaveHelperProfile').catch(() => undefined);
  }

  interact(x: number, y: number): void {
    void this.connection?.invoke('Interact', x, y).catch(() => undefined);
  }

  chooseCard(cardId: string): void {
    void this.connection?.invoke('ChooseCard', cardId).catch(() => undefined);
  }

  rerollCards(): void {
    void this.connection?.invoke('RerollCards').catch(() => undefined);
  }

  banCard(cardId: string): void {
    void this.connection?.invoke('BanCard', cardId).catch(() => undefined);
  }

}
