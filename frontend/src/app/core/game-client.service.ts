import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { MapDto, SnapshotDto } from './types';

/** LM-03: game mode (mirrors the backend GameMode enum). Defaults to Dungeon. */
export const enum GameMode {
  Dungeon = 0,
  Arena = 1,
  Training = 2,
}

export interface JoinRunResult {
  seed: number;
  tier: number;
  tierName: string;
  waifuId: string;
  mode: GameMode;
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

  async joinRun(
    tier: number,
    waifuId?: string,
    seed?: number,
    resume = false,
    mode: GameMode = GameMode.Dungeon,
  ): Promise<JoinRunResult> {
    this.snapshot.set(null);
    this.map.set(null);
    await this.connect();
    // The hub requires exact arity (SignalR does not support a missing optional argument), so always
    // send the mode. Default Dungeon keeps the legacy flow identical; Arena (LM-04/05) passes GameMode.Arena.
    return this.connection!.invoke<JoinRunResult>('JoinRun', tier, waifuId ?? null, seed ?? null, resume, mode);
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

  // Dash/Dodge (Shift): sends the current direction (0,0 = engine uses movement/facing direction).
  dash(dx: number, dy: number): void {
    void this.connection?.invoke('Dash', dx, dy).catch(() => undefined);
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

  /** Training Room sandbox toggle: when on, skills and the ultimate ignore cooldown/gauge. */
  setTrainingFreeCast(enabled: boolean): void {
    void this.connection?.invoke('SetTrainingFreeCast', enabled).catch(() => undefined);
  }

  /** G-10: saves the current helper config as the run Kaeli's default. */
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
