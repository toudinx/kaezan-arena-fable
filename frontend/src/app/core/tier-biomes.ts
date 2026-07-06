/**
 * Visual theme by tier (Hunt depth), mirroring `Domain/Biomes.cs` on the frontend.
 * Each tier is a deeper earth stratum; color encodes the biome and threat
 * (soil -> fort -> crypt -> lava lair -> abyss), not decoration. Use it as a contextual
 * descent accent (tier rail, boss dossier, pre-run strip), never as the primary action color.
 */
export interface TierBiome {
  /** Biome accent (glow/atmosphere/depth edge). */
  accent: string;
  /** Deep biome tone for atmosphere gradients. */
  deep: string;
  /** Short biome label (descent eyebrow). */
  label: string;
  /** Optional panoramic biome art; CSS falls back to gradients if missing. */
  bg: string;
}

export const TIER_BIOMES: Record<number, TierBiome> = {
  1: { accent: '#8cbf4d', deep: '#2c3a17', label: 'Cave', bg: '/assets/biomes/generated/tier-1-cave-cinematic.png' },
  2: { accent: '#d99a3c', deep: '#4a3210', label: 'Fort', bg: '/assets/biomes/generated/tier-2-fort-cinematic.png' },
  3: { accent: '#a662ff', deep: '#2e1a4d', label: 'Crypt', bg: '/assets/biomes/generated/tier-3-crypt-cinematic.png' },
  4: { accent: '#ff6a3d', deep: '#4a1a0e', label: 'Lair', bg: '/assets/biomes/generated/tier-4-lair-cinematic.png' },
  5: { accent: '#7b6bf2', deep: '#1f1a45', label: 'Abyss', bg: '/assets/biomes/generated/tier-5-abyss-cinematic.png' },
};

export function tierBiome(tier: number): TierBiome {
  return TIER_BIOMES[tier] ?? TIER_BIOMES[1];
}
