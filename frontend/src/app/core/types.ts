// ---- REST DTOs ----

export interface WaifuDef {
  id: string;
  name: string;
  title: string;
  rarity: number;
  element: string;
  weapon: string;
  lookType: number;
  head: number;
  body: number;
  legs: number;
  feet: number;
  baseAtk: number;
  baseHp: number;
  skill1: string;
  skill2: string;
  skill3: string;
  ultimate: string;
  description: string;
}

export interface SkillDef {
  id: string;
  name: string;
  shape: string;
  element: string;
  power: number;
  cooldownMs: number;
  range: number;
  radius: number;
  missileId: number;
  effectId: number;
  stunMs: number;
  buff: string | null;
  buffMs: number;
  description: string;
}

export interface CardDef {
  id: string;
  name: string;
  description: string;
  stat: string;
  value: number;
}

export interface DungeonTier {
  tier: number;
  name: string;
  description: string;
  commonMobs: string[];
  eliteMobs: string[];
  boss: string;
  requiredAccountLevel: number;
  statMultiplier: number;
}

export interface BannerDef {
  id: string;
  name: string;
  description: string;
  featuredWaifuId: string | null;
}

export interface MonsterCatalogEntry {
  name: string;
  description: string;
  health: number;
  experience: number;
  isBoss: boolean;
  bestiaryClass: string;
  outfit: { lookType: number; head: number; body: number; legs: number; feet: number; addons: number };
  loot: { itemId: number; name: string; chance: number }[];
}

export interface Catalog {
  waifus: WaifuDef[];
  skills: SkillDef[];
  cards: CardDef[];
  tiers: DungeonTier[];
  banners: BannerDef[];
  pullCost: number;
  ascensionShardCost: number[];
  addonAscensions: number[];
  bestiaryRanks: number[];
  monsters: MonsterCatalogEntry[];
}

export interface DailyContract {
  id: string;
  kind: string;
  param: string;
  description: string;
  target: number;
  progress: number;
  claimed: boolean;
}

export interface PityState {
  pullsSinceFiveStar: number;
  pullsSinceFourStar: number;
  featuredGuaranteed: boolean;
  totalPulls: number;
}

export interface InventoryStack {
  itemId: number;
  name: string;
  count: number;
}

export interface Account {
  id: string;
  accountLevel: number;
  accountXp: number;
  accountXpNext: number;
  gold: number;
  kaeros: number;
  ownedWaifus: string[];
  shards: Record<string, number>;
  ascension: Record<string, number>;
  activeWaifuId: string;
  bestiaryKills: Record<string, number>;
  inventory: InventoryStack[];
  runsPlayed: number;
  runsWon: number;
  tierClears: Record<string, number>;
  pity: Record<string, PityState>;
  dailies: DailyContract[];
}

export interface PullResult {
  waifuId: string;
  name: string;
  title: string;
  rarity: number;
  isNew: boolean;
  shardsGained: number;
  wasFeatured: boolean;
}

export interface PullResponse {
  results: PullResult[];
  kaerosLeft: number;
  pullsSinceFiveStar: number;
  pullsSinceFourStar: number;
  featuredGuaranteed: boolean;
}

// ---- game (SignalR) DTOs ----

export interface OutfitDto {
  lookType: number;
  head: number;
  body: number;
  legs: number;
  feet: number;
  addons: number;
}

export interface PoiDto {
  id: number;
  kind: string;
  x: number;
  y: number;
  itemId: number;
  used: boolean;
}

export interface MapDto {
  floor: number;
  w: number;
  h: number;
  ground: number[];
  wall: number[];
  decor: number[];
  blocked: boolean[];
  entryX: number;
  entryY: number;
  ladderX: number | null;
  ladderY: number | null;
  pois: PoiDto[];
}

export interface SkillStateDto {
  id: string;
  name: string;
  cooldownRemainingMs: number;
  cooldownTotalMs: number;
  ready: boolean;
}

export interface PlayerDto {
  id: number;
  x: number;
  y: number;
  dir: number;
  hp: number;
  maxHp: number;
  fromX: number;
  fromY: number;
  stepDurMs: number;
  stepStartTick: number;
  outfit: OutfitDto;
  targetId: number;
  gauge: number;
  skills: SkillStateDto[];
  autoAttackReadyInMs: number;
  activeBuffs: string[];
}

export interface MonsterDto {
  id: number;
  species: string;
  x: number;
  y: number;
  dir: number;
  hp: number;
  maxHp: number;
  fromX: number;
  fromY: number;
  stepDurMs: number;
  stepStartTick: number;
  outfit: OutfitDto;
  isBoss: boolean;
  stunned: boolean;
}

export interface GroundItemDto {
  id: number;
  x: number;
  y: number;
  itemId: number;
  count: number;
}

export interface EventDto {
  kind: string;
  x: number;
  y: number;
  toX: number;
  toY: number;
  value: number;
  text: string;
  actorId: number;
  crit: boolean;
}

export interface CardOfferDto {
  id: string;
  name: string;
  description: string;
  currentStacks: number;
}

export interface CardStackDto {
  id: string;
  name: string;
  stacks: number;
}

export interface RewardItemDto {
  itemId: number;
  name: string;
  count: number;
}

export interface RunEndDto {
  victory: boolean;
  reason: string;
  goldEarned: number;
  accountXpEarned: number;
  kaerosEarned: number;
  kills: number;
  runLevel: number;
  durationMs: number;
  items: RewardItemDto[];
  dailyProgressNotes: string[];
}

export interface RunStateDto {
  tier: number;
  tierName: string;
  seed: number;
  level: number;
  xp: number;
  xpNext: number;
  gold: number;
  kills: number;
  cards: CardStackDto[];
  offer: CardOfferDto[] | null;
  bossHp: number | null;
  bossMaxHp: number | null;
  bossName: string | null;
  elapsedMs: number;
  ended: RunEndDto | null;
}

export interface SnapshotDto {
  tick: number;
  floor: number;
  player: PlayerDto;
  monsters: MonsterDto[];
  items: GroundItemDto[];
  events: EventDto[];
  run: RunStateDto;
}

export const TICK_MS = 100;

export const RARITY_COLORS: Record<number, string> = {
  3: '#5ba8d4',
  4: '#a06bd6',
  5: '#e8a93c',
};

export const ELEMENT_LABELS: Record<string, string> = {
  physical: 'Físico',
  fire: 'Fogo',
  ice: 'Gelo',
  energy: 'Energia',
  earth: 'Terra',
  death: 'Morte',
  holy: 'Sagrado',
};

export const WEAPON_LABELS: Record<string, string> = {
  melee: 'Corpo a corpo',
  bow: 'Arco',
  wand: 'Cajado',
};
