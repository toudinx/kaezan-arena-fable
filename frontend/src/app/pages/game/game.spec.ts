import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiService } from '../../core/api.service';
import { AssetsService } from '../../core/assets.service';
import { GameClientService, GameMode, JoinRunResult } from '../../core/game-client.service';
import { SoundService } from '../../core/sound.service';
import { GamePage } from './game';

describe('GamePage', () => {
  let fixture: ComponentFixture<GamePage>;
  let preloadResolve: () => void;
  let client: {
    snapshot: ReturnType<typeof signal<null>>;
    map: ReturnType<typeof signal<null>>;
    joinRun: ReturnType<typeof vi.fn<() => Promise<JoinRunResult>>>;
    leave: ReturnType<typeof vi.fn<() => Promise<void>>>;
  };
  let assets: {
    load: ReturnType<typeof vi.fn<() => Promise<void>>>;
    preload: ReturnType<typeof vi.fn<() => Promise<void>>>;
  };

  beforeEach(async () => {
    vi.stubGlobal('requestAnimationFrame', vi.fn(() => 1));
    vi.stubGlobal('cancelAnimationFrame', vi.fn());

    client = {
      snapshot: signal(null),
      map: signal(null),
      joinRun: vi.fn(async () => ({
        seed: 123,
        tier: 1,
        tierName: 'Tier 1',
        waifuId: 'waifu:eloa',
        mode: GameMode.Dungeon,
        resumed: false,
      })),
      leave: vi.fn(async () => undefined),
    };
    assets = {
      load: vi.fn(async () => undefined),
      preload: vi.fn(() => new Promise<void>((resolve) => { preloadResolve = resolve; })),
    };

    await TestBed.configureTestingModule({
      imports: [GamePage],
      providers: [
        { provide: GameClientService, useValue: client },
        { provide: AssetsService, useValue: assets },
        { provide: ApiService, useValue: { loadCatalog: vi.fn(async () => ({ skills: [] })) } },
        { provide: SoundService, useValue: { muted: signal(false), toggleMute: vi.fn() } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: (key: string) => key === 'tier' ? '1' : null },
              queryParamMap: { get: () => null },
            },
          },
        },
        { provide: Router, useValue: { navigate: vi.fn(async () => true) } },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    fixture?.destroy();
    vi.unstubAllGlobals();
  });

  it('waits for atlas preload before joining the run', async () => {
    fixture = TestBed.createComponent(GamePage);
    fixture.detectChanges();
    await Promise.resolve();

    expect(assets.preload).toHaveBeenCalledWith(['outfits', 'objects', 'effects', 'missiles']);
    expect(client.joinRun).not.toHaveBeenCalled();

    preloadResolve();
    await Promise.resolve();
    await Promise.resolve();
    await fixture.whenStable();

    expect(client.joinRun).toHaveBeenCalledOnce();
  });
});
