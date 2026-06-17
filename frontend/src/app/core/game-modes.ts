/**
 * Modos de jogo da Caçada. Hoje só o modo "dungeon" é jogável (`live`); os demais são placeholders
 * `soon` para popular o carrossel e telegrafar a direção. Migrável pra ContentStore/catalog depois,
 * seguindo a direção data-driven do painel admin.
 */
export interface GameModeDef {
  id: string;
  name: string;
  tagline: string;
  description: string;
  icon: string;
  /** matiz base usada no card/banner (CSS hue/gradiente). */
  theme: string;
  status: 'live' | 'soon';
}

export const GAME_MODES: GameModeDef[] = [
  {
    id: 'dungeon',
    name: 'Expedição',
    tagline: 'Dungeons procedurais · 5 tiers',
    description: 'Salas de mobs, baús e um boss no fundo. Escolha o tier e a Kaeli antes de entrar.',
    icon: '⚔',
    theme: '#2dd4bf',
    status: 'live',
  },
  {
    id: 'endless',
    name: 'Abismo Sem Fim',
    tagline: 'Ondas infinitas · ranking',
    description: 'Sobreviva a ondas cada vez mais duras. Quanto mais fundo, melhor o saque.',
    icon: '♾',
    theme: '#a06bd6',
    status: 'soon',
  },
  {
    id: 'boss-rush',
    name: 'Boss Rush',
    tagline: 'Só os bosses · contra o relógio',
    description: 'Encare os bosses de cada tier em sequência, sem salas intermediárias.',
    icon: '👑',
    theme: '#e8a93c',
    status: 'soon',
  },
  {
    id: 'raid',
    name: 'Raide de Esquadrão',
    tagline: 'Co-op · em breve',
    description: 'Leve um esquadrão de Kaelis para encarar inimigos colossais.',
    icon: '🛡',
    theme: '#5ba8d4',
    status: 'soon',
  },
];
