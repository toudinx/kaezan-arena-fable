/**
 * Hunt game modes. Today only the "dungeon" mode is playable (`live`); the others are `soon`
 * placeholders that fill the carousel and telegraph the direction. Can move to ContentStore/catalog
 * later, following the admin panel's data-driven direction.
 */
export interface GameModeDef {
  id: string;
  name: string;
  tagline: string;
  description: string;
  icon: string;
  /** Base hue used by the card/banner (CSS hue/gradient). */
  theme: string;
  status: 'live' | 'soon';
}

export const GAME_MODES: GameModeDef[] = [
  {
    id: 'dungeon',
    name: 'Expedition',
    tagline: 'Procedural dungeons · 5 tiers',
    description: 'Mob rooms, chests, and a boss at the end. Choose the tier and Kaeli before entering.',
    icon: '⚔',
    theme: '#2dd4bf',
    status: 'live',
  },
  {
    id: 'training',
    name: 'Training Room',
    tagline: 'Sandbox · test your kit',
    description: 'A quiet arena with a passive, high-HP dummy. Try dashes, skills and reactions with no pressure.',
    icon: '🎯',
    theme: '#7b6bf2',
    status: 'live',
  },
  {
    id: 'endless',
    name: 'Endless Abyss',
    tagline: 'Endless waves · leaderboard',
    description: 'Survive increasingly brutal waves. The deeper you go, the better the loot.',
    icon: '♾',
    theme: '#a06bd6',
    status: 'soon',
  },
  {
    id: 'boss-rush',
    name: 'Boss Rush',
    tagline: 'Bosses only · against the clock',
    description: 'Face each tier boss in sequence, with no rooms between them.',
    icon: '👑',
    theme: '#e8a93c',
    status: 'soon',
  },
  {
    id: 'raid',
    name: 'Squad Raid',
    tagline: 'Co-op · coming soon',
    description: 'Bring a squad of Kaelis to face colossal enemies.',
    icon: '🛡',
    theme: '#5ba8d4',
    status: 'soon',
  },
];
