import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { MapDto, SnapshotDto } from './types';

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

  async joinRun(tier: number, seed?: number): Promise<void> {
    this.snapshot.set(null);
    this.map.set(null);
    await this.connect();
    await this.connection!.invoke('JoinRun', tier, seed ?? null);
  }

  async leave(): Promise<void> {
    if (!this.connection) return;
    try {
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

  interact(x: number, y: number): void {
    void this.connection?.invoke('Interact', x, y).catch(() => undefined);
  }

  chooseCard(cardId: string): void {
    void this.connection?.invoke('ChooseCard', cardId).catch(() => undefined);
  }

  abandon(): void {
    void this.connection?.invoke('Abandon').catch(() => undefined);
  }
}
