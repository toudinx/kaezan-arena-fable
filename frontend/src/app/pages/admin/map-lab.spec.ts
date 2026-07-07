import { afterEach, describe, expect, it, vi } from 'vitest';
import { cloneBiomeRow, drawMapPreviewCanvas, previewRequestFromDraft, replaceBiomeRow } from './map-lab';
import type { AssetsService } from '../../core/assets.service';
import type { BiomeDef, BiomeRow, MapDto } from '../../core/types';

function previewMap(): MapDto {
  return {
    floor: 0,
    w: 2,
    h: 1,
    ground: [11, 12],
    borderA: [21, 0],
    borderB: [31, 0],
    decor: [41, 0],
    wall: [0, 52],
    blocked: [false, true],
    entryX: 0,
    entryY: 0,
    ladderX: null,
    ladderY: null,
    pois: [],
    rooms: [{ x: 0, y: 0, w: 1, h: 1, role: 'mob' }],
    biome: {
      name: 'Test',
      tintR: 0,
      tintG: 0,
      tintB: 0,
      tintStrength: 0,
      fogR: 0,
      fogG: 0,
      fogB: 0,
      fogStrength: 0,
      vignette: 0,
      particleR: 0,
      particleG: 0,
      particleB: 0,
      particleDensity: 0,
      particleDrift: 0,
    },
  };
}

function biomeDef(name: string): BiomeDef {
  return {
    ground: [351],
    bossGround: [351],
    bedrock: 101,
    wallH: 356,
    wallV: 357,
    wallPole: 358,
    wallCorner: 359,
    decor: [1772],
    decorChance: 0.03,
    accent: [727],
    accentChance: 0.02,
    atmosphere: {
      name,
      tintR: 15,
      tintG: 20,
      tintB: 25,
      tintStrength: 0.1,
      fogR: 30,
      fogG: 35,
      fogB: 40,
      fogStrength: 0.2,
      vignette: 0.3,
      particleR: 45,
      particleG: 50,
      particleB: 55,
      particleDensity: 0.04,
      particleDrift: 0.05,
    },
    wallSet: null,
    wallFamily: 'mountain',
    groundFamilies: ['cave', 'earth floor'],
  };
}

function biomeRows(): BiomeRow[] {
  return [
    { tier: 1, name: 'Cave', def: biomeDef('Cave') },
    { tier: 2, name: 'Forest', def: biomeDef('Forest') },
  ];
}

describe('drawMapPreviewCanvas', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('draws map layers in the renderer order and keeps overlays optional', () => {
    const canvas = document.createElement('canvas');
    vi.spyOn(canvas, 'getContext').mockReturnValue({
      clearRect: vi.fn(),
      fillRect: vi.fn(),
      strokeRect: vi.fn(),
      fillText: vi.fn(),
      save: vi.fn(),
      restore: vi.fn(),
      beginPath: vi.fn(),
      rect: vi.fn(),
      clip: vi.fn(),
      scale: vi.fn(),
      set fillStyle(_value: string) {},
      set strokeStyle(_value: string) {},
      set lineWidth(_value: number) {},
      set font(_value: string) {},
    } as unknown as CanvasRenderingContext2D);
    const drawObject = vi.fn();
    const assets = { drawObject } as unknown as AssetsService;

    drawMapPreviewCanvas(canvas, previewMap(), assets, { zoom: 1, showBlocked: false, showRooms: false });

    expect(canvas.width).toBe(64);
    expect(canvas.height).toBe(32);
    expect(drawObject.mock.calls.map((call) => call[1])).toEqual([11, 21, 31, 41, 12, 52]);
  });
});

describe('Map Lab biome draft helpers', () => {
  it('clones a selected row deeply before editing palettes and atmosphere', () => {
    const rows = biomeRows();
    const draft = cloneBiomeRow(rows[0]);

    draft.def.decor.push(1773);
    draft.def.atmosphere.tintR = 99;
    draft.def.groundFamilies?.push('mossy floor');

    expect(rows[0].def.decor).toEqual([1772]);
    expect(rows[0].def.atmosphere.tintR).toBe(15);
    expect(rows[0].def.groundFamilies).toEqual(['cave', 'earth floor']);
  });

  it('builds draft preview and save payloads without replacing other tiers', () => {
    const rows = biomeRows();
    const draft = cloneBiomeRow(rows[1]);
    draft.def.wallFamily = 'mossy wall mountain';
    draft.def.decorChance = 0.12;

    expect(previewRequestFromDraft(2, 1234, true, draft)).toMatchObject({
      tier: 2,
      seed: 1234,
      floorIndex: 1,
      bossFloor: true,
      biome: draft.def,
    });

    const replaced = replaceBiomeRow(rows, draft);
    expect(replaced).toHaveLength(2);
    expect(replaced[0]).toBe(rows[0]);
    expect(replaced[1]).not.toBe(rows[1]);
    expect(replaced[1].def.wallFamily).toBe('mossy wall mountain');
    expect(rows[1].def.wallFamily).toBe('mountain');
  });
});
