import { describe, expect, it } from 'vitest';

import { TIER_BIOMES, tierBiome } from './tier-biomes';

describe('TIER_BIOMES', () => {
  it('points every dungeon tier at its generated cinematic wallpaper', () => {
    expect(TIER_BIOMES).toEqual({
      1: {
        accent: '#8cbf4d',
        deep: '#2c3a17',
        label: 'Cave',
        bg: '/assets/biomes/generated/tier-1-cave-cinematic.png',
      },
      2: {
        accent: '#d99a3c',
        deep: '#4a3210',
        label: 'Fort',
        bg: '/assets/biomes/generated/tier-2-fort-cinematic.png',
      },
      3: {
        accent: '#a662ff',
        deep: '#2e1a4d',
        label: 'Crypt',
        bg: '/assets/biomes/generated/tier-3-crypt-cinematic.png',
      },
      4: {
        accent: '#ff6a3d',
        deep: '#4a1a0e',
        label: 'Lair',
        bg: '/assets/biomes/generated/tier-4-lair-cinematic.png',
      },
      5: {
        accent: '#7b6bf2',
        deep: '#1f1a45',
        label: 'Abyss',
        bg: '/assets/biomes/generated/tier-5-abyss-cinematic.png',
      },
    });
  });

  it('falls back to the generated cave wallpaper for unknown tiers', () => {
    expect(tierBiome(99).bg).toBe('/assets/biomes/generated/tier-1-cave-cinematic.png');
  });
});
