/**
 * Tema visual por tier (profundidade da Caçada) — espelha `Domain/Biomes.cs` no frontend.
 * Cada tier é um estrato cada vez mais fundo da terra; a cor codifica o bioma e a ameaça
 * (solo → forte → cripta → covil de lava → abismo), não é enfeite. Use como acento contextual
 * da descida (rail de tiers, dossiê de boss, strip de pré-run) — nunca como cor primária de ação.
 */
export interface TierBiome {
  /** Acento do bioma (glow/atmosfera/borda da profundidade). */
  accent: string;
  /** Tom profundo do bioma para gradientes de atmosfera. */
  deep: string;
  /** Rótulo curto do bioma (eyebrow da descida). */
  label: string;
  /** Arte panoramica opcional do bioma; CSS cai para gradientes se faltar. */
  bg: string;
}

export const TIER_BIOMES: Record<number, TierBiome> = {
  1: { accent: '#8cbf4d', deep: '#2c3a17', label: 'Caverna', bg: '/assets/biomes/tier-1.webp' },
  2: { accent: '#d99a3c', deep: '#4a3210', label: 'Forte', bg: '/assets/biomes/tier-2.webp' },
  3: { accent: '#a662ff', deep: '#2e1a4d', label: 'Cripta', bg: '/assets/biomes/tier-3.webp' },
  4: { accent: '#ff6a3d', deep: '#4a1a0e', label: 'Covil', bg: '/assets/biomes/tier-4.webp' },
  5: { accent: '#7b6bf2', deep: '#1f1a45', label: 'Abismo', bg: '/assets/biomes/tier-5.webp' },
};

export function tierBiome(tier: number): TierBiome {
  return TIER_BIOMES[tier] ?? TIER_BIOMES[1];
}
