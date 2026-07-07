import { afterEach, describe, expect, it, vi } from 'vitest';
import { drawMapPreviewCanvas } from './map-lab';
import type { AssetsService } from '../../core/assets.service';
import type { MapDto } from '../../core/types';

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
