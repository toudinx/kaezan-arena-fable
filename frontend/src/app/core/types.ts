// ---- REST DTOs ----

export interface TraitDef {
  id: string;
  name: string;
  kind: string;
  value: number;
  param: number;
  tag: string;
  description: string;
}

export interface SkinDef {
  id: string;
  name: string;
  description: string;
  lookType: number;
  head: number;
  body: number;
  legs: number;
  feet: number;
  unlock: 'default' | 'affinity' | 'gold' | 'kaeros';
  unlockValue: number;
  addons?: number;
  mountLookType?: number;
}

/** Skin autoral criada no Outfit Studio do painel admin. */
export interface KaeliSkinDefinition {
  waifuId: string;
  id: string;
  name: string;
  description: string;
  lookType: number;
  head: number;
  body: number;
  legs: number;
  feet: number;
  addons: number;
  mountLookType: number;
  unlock: 'default' | 'affinity' | 'gold' | 'kaeros';
  unlockValue: number;
}

export interface KaeliAuthoringKaeli {
  id: string;
  name: string;
  title: string;
  rarity: number;
  element: string;
  classId: string;
  defaultSkin: { lookType: number; head: number; body: number; legs: number; feet: number };
  /** ids of code-defined skins (static); the wardrobe uses them to distinguish overrides. */
  staticSkinIds: string[];
  /** default skin id (index 0); must keep the "default" unlock. */
  defaultSkinId: string;
}

export interface KaeliAuthoringMetadata {
  kaelis: KaeliAuthoringKaeli[];
  unlockKinds: string[];
  outfitColorCount: number;
  affinityMaxLevel: number;
}

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
  classId: string;
  description: string;
  personality: string;
  trait: TraitDef;
  lore: string[];
  favoriteGiftItemIds: number[];
  skins: SkinDef[];
}

export interface MasteryNodeDef {
  id: string;
  branch: 'off' | 'def' | 'eco';
  order: number;
  name: string;
  description: string;
  cost: number;
  effectKind: string;
  effectTarget: string;
  value: number;
}

export interface AffinityConfig {
  maxLevel: number;
  xpPerLevel: number[];
  statBonusPerLevel: number;
  loreLevels: number[];
  kaerosRewards: Record<string, number>;
  giftsPerDay: number;
  giftFavoriteMultiplier: number;
  giftBaseXp: number;
  giftXpPerGold: number;
  giftXpCap: number;
}

export interface MasteryConfig {
  respecGold: number;
  pointsPerVictory: number;
  pointsPerDefeat: number;
}

export interface FarmConfig {
  autoRepeatDelayMs: number;
  minRuns: number;
  maxRuns: number;
  energyPerRun: number;
  energyCap: number;
}

export interface AffinityProgress {
  level: number;
  xpIntoLevel: number;
  xpToNext: number;
}

export interface MasteryState {
  points: number;
  spent: number;
  nodes: string[];
}

export interface ClassStanceDef {
  id: string;
  name: string;
  element: string;
  slots: string[];
  ultimate: string;
}

export interface ClassDef {
  id: string;
  name: string;
  description: string;
  defaultStanceId: string;
  stances: ClassStanceDef[];
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

/** MG-05: editable role tuning row in admin (mirrors backend RoleTuningRow). */
export interface RoleTuningRow {
  role: string;
  autoDmgMult: number;
  skillDmgMult: number;
  baseAutoAttackMs: number;
  autoRange: number;
  aoeScale: number;
}

export interface BannerDef {
  id: string;
  name: string;
  description: string;
  featuredWaifuId: string | null;
}

export interface MonsterCatalogEntry {
  id: string;
  name: string;
  description: string;
  health: number;
  experience: number;
  isBoss: boolean;
  bestiaryClass: string;
  origin: string | null;
  bossRace: string | null;
  corpse: number;
  outfit: { lookType: number; head: number; body: number; legs: number; feet: number; addons: number };
  loot: { itemId: number; name: string; chance: number }[];
  source: 'legacy' | 'authored';
  rank: 'legacy' | 'common' | 'elite' | 'boss';
  element: string;
  behaviorId: string;
  statPresetId: string;
  hpMultiplier: number;
  damageMultiplier: number;
  speedMultiplier: number;
  cadenceMultiplier: number;
  powerTier: number;
  resistances: Record<string, number>;
}

export interface MonsterDefinition {
  id: string;
  name: string;
  description: string;
  outfit: MonsterCatalogEntry['outfit'];
  corpse: number;
  powerTier: number;
  rank: 'common' | 'elite' | 'boss';
  behaviorId: string;
  elementId: string;
  statPresetId: string;
  hpMultiplier: number;
  damageMultiplier: number;
  speedMultiplier: number;
  cadenceMultiplier: number;
  bestiaryClass: string;
  resistances: Record<string, number>;
  // G-08B: resistance by card keyword (sin/curse/burn/charge/frost/posture/combo/prey); % 0-100, negative amplifies.
  keywordResistances: Record<string, number>;
  appearanceId: string;
  enabled: boolean;
}

export interface MonsterAppearance {
  id: string;
  name: string;
  source: string;
  outfit: MonsterCatalogEntry['outfit'];
  corpse: number;
  bestiaryClass: string;
  classificationSource: 'bestiary-class' | 'bestiary-race' | 'folder' | 'shared-outfit' | 'override' | 'unclassified' | 'legacy';
  kind: 'normal' | 'boss';
  kindSource: 'bosstiary' | 'rewardBoss' | 'path' | 'default' | 'override' | 'legacy';
  legacyImported: boolean;
}

export interface MonsterStatPreset {
  id: string;
  name: string;
  description: string;
  hpMultiplier: number;
  damageMultiplier: number;
  speedMultiplier: number;
  cadenceMultiplier: number;
}

export interface MonsterBehaviorProfile {
  id: string;
  name: string;
  description: string;
  targetDistance: number;
  staticAttackChance: number;
}

export interface MonsterElementProfile {
  id: string;
  name: string;
  areaEffect: number;
  shootEffect: number;
  conditionType: string | null;
}

export interface MonsterStatLine {
  health: number;
  damage: number;
  armor: number;
  speed: number;
  experience: number;
}

export interface MonsterAuthoringMetadata {
  behaviors: MonsterBehaviorProfile[];
  elements: MonsterElementProfile[];
  presets: MonsterStatPreset[];
  statLines: Record<string, MonsterStatLine>;
  modifierMin: number;
  modifierMax: number;
  resistanceMin: number;
  resistanceMax: number;
  // G-08B: keyword tags (from G-04) and keyword resistance range.
  keywordTags: string[];
  keywordResistMin: number;
  keywordResistMax: number;
  appearances: MonsterAppearance[];
}

export interface ItemCatalogEntry {
  itemId: number;
  name: string;
  description: string;
  salePrice: number;
  slot: EquipmentSlot | null;
  weaponType: string | null;
  attack: number;
  armor: number;
  defense: number;
  mountLookType: number;
  mountSpeed: number;
  appearanceItemId: number;
  sourceItemId: number;
  isAuthored: boolean;
  element: string;
  elementDamage: number;
  skillPower: number;
  critChance: number;
  critDamage: number;
  lifeStealChance: number;
  lifeStealAmount: number;
  cooldownReduction: number;
  moveSpeedPercent: number;
  physicalResistance: number;
  fireResistance: number;
  iceResistance: number;
  earthResistance: number;
  energyResistance: number;
  deathResistance: number;
  holyResistance: number;
  allowedClassIds: string[];
  requiredMasteryPoints: number;
  /** 0 = tierless (legacy, equippable in any loadout); 1..5 = set piece locked to that tier. */
  tier: number;
  tag: 'normal' | 'relic';
  statMultiplier: number;
}

export type EquipmentSlot = 'helmet' | 'armor' | 'weapon' | 'necklace' | 'ring' | 'mount';
export type EquipmentLoadout = Partial<Record<EquipmentSlot, number>>;

/** Loadout key: one set per Kaeli PER tier. Mirrors backend AccountState.EquipKey. */
export function equipKey(waifuId: string, tier: number): string {
  return `${waifuId}#${tier}`;
}

/** Available set tiers (1..5), aligned with dungeon tiers. */
export const SET_TIERS = [1, 2, 3, 4, 5] as const;

/** Item enriched with category/subcategory plus edit flag for the admin item editor. */
export interface ItemCapabilities {
  attack: boolean;
  armor: boolean;
  defense: boolean;
  mountSpeed: boolean;
  offense: boolean;
  support: boolean;
  resistance: boolean;
  critChance: boolean;
  critDamage: boolean;
  vampiric: boolean;
  cooldownReduction: boolean;
  moveSpeed: boolean;
  physicalResistance: boolean;
  elementResistance: boolean;
  elementAffinity: boolean;
}

export interface AdminItem extends ItemCatalogEntry {
  category: string;
  subcategory: string;
  capabilities: ItemCapabilities;
}

export interface ItemBalanceGrade {
  id: 'low' | 'moderate' | 'high';
  name: string;
}

export interface ItemBalanceRange {
  stat: string;
  tier: number;
  lowMin: number;
  lowMax: number;
  moderateMin: number;
  moderateMax: number;
  highMin: number;
  highMax: number;
}

export interface ItemBalanceMetadata {
  tiers: number[];
  grades: ItemBalanceGrade[];
  ranges: ItemBalanceRange[];
  tags: { id: 'normal' | 'relic'; name: string }[];
  relicMultiplierDefault: number;
  relicMultiplierMin: number;
  relicMultiplierMax: number;
}

export interface AdminItemsPayload {
  library: AdminItem[];
  authored: AdminItem[];
  classes: { id: string; name: string }[];
  elements: string[];
  balance: ItemBalanceMetadata;
}

export interface Catalog {
  waifus: WaifuDef[];
  classes: ClassDef[];
  skills: SkillDef[];
  cards: CardDef[];
  tiers: DungeonTier[];
  banners: BannerDef[];
  pullCost: number;
  ascensionShardCost: number[];
  addonAscensions: number[];
  bestiaryRanks: number[];
  itemFallbackSalePrice: number;
  masteryTrees: Record<string, MasteryNodeDef[]>;
  affinity: AffinityConfig;
  mastery: MasteryConfig;
  farm: FarmConfig;
  items: ItemCatalogEntry[];
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

export interface OfflineReward {
  elapsedMinutes: number;
  creditedMinutes: number;
  gold: number;
  accountXp: number;
  tier: number;
  capped: boolean;
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
  affinityXp: Record<string, number>;
  affinity: Record<string, AffinityProgress>;
  giftsToday: Record<string, number>;
  ownedSkins: string[];
  selectedSkins: Record<string, string>;
  mastery: Record<string, MasteryState>;
  bestiaryKills: Record<string, number>;
  inventory: InventoryStack[];
  equipment: Record<string, EquipmentLoadout>;
  runsPlayed: number;
  runsWon: number;
  tierClears: Record<string, number>;
  pity: Record<string, PityState>;
  dailies: DailyContract[];
  offlineReward: OfflineReward | null;
}

export interface PullResult {
  kind: 'waifu' | 'item';
  waifuId: string | null;
  itemId: number | null;
  count: number;
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
  mountLookType: number;
}

export interface EquipmentStatsDto {
  attackBonus: number;
  maxHpBonus: number;
  damageReduction: number;
  moveSpeedPercent: number;
  skillPowerMultiplier: number;
  critChance: number;
  critDamage: number;
  cooldownReduction: number;
}

export interface AutoHelperSettingsDto {
  targeting: boolean;
  skills: boolean;
  ultimate: boolean;
  targetPreference: 'lowestHp' | 'nearest';
  movementMode: 'none' | 'follow' | 'avoid';
  defaultMovementMode: 'follow' | 'avoid';
  /** G-10: auto-uses the potion when health falls below autoHealPct%. */
  autoHeal: boolean;
  /** G-10: auto-heal threshold in % (10..90, default 50). */
  autoHealPct: number;
  /** G-10: auto-loot — 'off' | 'loot' (collects chests/altars and heads to the exit). */
  navMode: 'off' | 'loot';
  /** G-10: automatically picks the highest-rarity card in the offer. */
  autoCards: boolean;
}

export interface PoiDto {
  id: number;
  kind: string;
  x: number;
  y: number;
  itemId: number;
  /** G-09: "" (common) | "cursed" (cursed chest, telegraphed). Mimics arrive as "" (surprise). */
  variant: string;
  used: boolean;
}

/** G-09: Echo material — synthetic ids (1 per tier) outside the equippable item catalog. */
export const GEAR_MATERIAL_ID_BASE = 950000;
export function isGearMaterial(itemId: number): boolean {
  return itemId > GEAR_MATERIAL_ID_BASE && itemId <= GEAR_MATERIAL_ID_BASE + 5;
}

/** G-07: room rectangle + type, so the minimap can draw an icon per room. */
export interface RoomDto {
  x: number;
  y: number;
  w: number;
  h: number;
  role: string;
}

/** G-07: cosmetic per-stratum color-grade/fog/particle palette (rendered client-side only). */
export interface BiomeDto {
  name: string;
  tintR: number;
  tintG: number;
  tintB: number;
  tintStrength: number;
  fogR: number;
  fogG: number;
  fogB: number;
  fogStrength: number;
  vignette: number;
  particleR: number;
  particleG: number;
  particleB: number;
  particleDensity: number;
  particleDrift: number;
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
  rooms: RoomDto[];
  biome: BiomeDto;
}

export interface SkillStateDto {
  id: string;
  name: string;
  element: string;
  description: string;
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
  classId: string;
  className: string;
  stanceId: string;
  stanceName: string;
  stanceElement: string;
  canToggleStance: boolean;
  autoAttackReadyInMs: number;
  autoHelper: AutoHelperSettingsDto;
  activeBuffs: string[];
  activeConditions: string[];
  equipmentStats: EquipmentStatsDto;
  potionCharges: number;
  potionMaxCharges: number;
  potionItemId: number;
  potionCooldownRemainingMs: number;
  potionCooldownTotalMs: number;
  potionHealPct: number;
  dashCooldownRemainingMs: number;
  dashCooldownTotalMs: number;
  dashReady: boolean;
  trait: TraitStateDto;
  /** Training Room sandbox toggle: skills/ult ignore cooldown & gauge. Always false outside Training. */
  trainingFreeCast: boolean;
}

export interface TraitStateDto {
  kind: string;
  name: string;
  value: number;
  max: number;
  text: string;
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
  elementMark: string;
  traitStacks: number;
  traitTag: string;
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

export type CardRarity = 'common' | 'rare' | 'echo';

export interface CardOfferDto {
  id: string;
  name: string;
  description: string;
  currentStacks: number;
  rarity: CardRarity;
  tags: string[];
  maxStacks: number;
}

export interface CardStackDto {
  id: string;
  name: string;
  stacks: number;
  rarity: CardRarity;
  tags: string[];
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
  cardRerollsRemaining: number;
  bannedCardsCount: number;
  cardRerollGoldCost: number;
  bossHp: number | null;
  bossMaxHp: number | null;
  bossName: string | null;
  bossPosture: number | null;
  bossPostureMax: number | null;
  bossStaggered: boolean;
  bossPostureCycle: number;
  elapsedMs: number;
  ended: RunEndDto | null;
  items: RewardItemDto[];
  /** G-10: tile/type the auto-loot is walking toward (null = pathing off). */
  navTarget: NavTargetDto | null;
}

/** G-10: current auto-loot target — readability only (the Kaeli walks there). */
export interface NavTargetDto {
  x: number;
  y: number;
  kind: string;
}

export interface SnapshotDto {
  tick: number;
  simulationMs: number;
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
  physical: 'Physical',
  fire: 'Fire',
  ice: 'Ice',
  energy: 'Energy',
  earth: 'Earth',
  death: 'Death',
  holy: 'Holy',
  support: 'Support',
};

export const WEAPON_LABELS: Record<string, string> = {
  melee: 'Melee',
  bow: 'Bow',
  wand: 'Wand',
};
