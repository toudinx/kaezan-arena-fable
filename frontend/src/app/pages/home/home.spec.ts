import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { ApiService } from '../../core/api.service';
import { KaeliArtService } from '../../core/kaeli-art.service';
import { Account, Catalog, WaifuDef } from '../../core/types';
import { HomePage } from './home';

describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;

  const skin = {
    id: 'skin:eloa:default',
    name: 'Default',
    description: 'Default skin',
    lookType: 128,
    head: 0,
    body: 0,
    legs: 0,
    feet: 0,
    unlock: 'default',
    unlockValue: 0,
  } as const;

  const waifu: WaifuDef = {
    id: 'waifu:eloa',
    name: 'Eloa',
    title: 'Dawn Seraph',
    rarity: 5,
    element: 'holy',
    weapon: 'staff',
    lookType: 128,
    head: 0,
    body: 0,
    legs: 0,
    feet: 0,
    baseAtk: 10,
    baseHp: 100,
    classId: 'mage',
    description: 'Judges the arena with holy light.',
    personality: 'calm',
    trait: {
      id: 'trait:eloa',
      name: 'Judgment Seal',
      kind: 'mark',
      value: 0,
      param: 0,
      tag: 'holy',
      description: 'Marks foes.',
    },
    lore: [],
    favoriteGiftItemIds: [],
    skins: [skin],
  };

  const catalog: Catalog = {
    waifus: [waifu],
    classes: [],
    skills: [],
    cards: [],
    tiers: [
      { tier: 1, name: 'Cave', description: 'Tier one', commonMobs: [], eliteMobs: [], boss: 'monster:boss', requiredAccountLevel: 1, statMultiplier: 1 },
      { tier: 2, name: 'Fort', description: 'Tier two', commonMobs: [], eliteMobs: [], boss: 'monster:boss2', requiredAccountLevel: 2, statMultiplier: 1.4 },
    ],
    banners: [{ id: 'banner:standard', name: 'Dawn Banner', description: 'Recruit Eloa.', featuredWaifuId: 'waifu:eloa' }],
    pullCost: 160,
    ascensionShardCost: [],
    addonAscensions: [],
    bestiaryRanks: [],
    itemFallbackSalePrice: 5,
    masteryTrees: {},
    affinity: {} as Catalog['affinity'],
    mastery: {} as Catalog['mastery'],
    farm: {} as Catalog['farm'],
    items: [],
    monsters: [],
  };

  const account: Account = {
    id: 'local',
    accountLevel: 1,
    accountXp: 0,
    accountXpNext: 100,
    gold: 0,
    kaeros: 0,
    ownedWaifus: ['waifu:eloa'],
    shards: {},
    ascension: {},
    activeWaifuId: 'waifu:eloa',
    affinityXp: {},
    affinity: {},
    giftsToday: {},
    ownedSkins: ['skin:eloa:default'],
    selectedSkins: { 'waifu:eloa': 'skin:eloa:default' },
    mastery: {},
    bestiaryKills: {},
    inventory: [],
    equipment: {},
    runsPlayed: 0,
    runsWon: 0,
    tierClears: {},
    pity: {},
    dailies: [
      { id: 'daily:1', kind: 'hunt', param: '', description: 'Clear a hunt', target: 1, progress: 1, claimed: false },
    ],
    offlineReward: null,
  };

  beforeEach(async () => {
    const api = {
      account: signal<Account | null>(account),
      catalog: signal<Catalog | null>(catalog),
      pinWaifu: async () => undefined,
      claimDaily: async () => undefined,
    };

    const art = {
      wallpaper: () => '/assets/kaelis/eloa/wallpaper.png',
      bgLandscape: () => null,
      elementGradient: () => '/assets/kaelis/_placeholder/holy.svg',
      thumb: () => null,
    };

    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideRouter([]),
        { provide: ApiService, useValue: api },
        { provide: KaeliArtService, useValue: art },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
  });

  it('uses contextual home actions instead of duplicating full navigation', () => {
    const rail = fixture.nativeElement.querySelector('nav.home-actions') as HTMLElement | null;
    expect(rail).not.toBeNull();

    const labels = Array.from(rail!.querySelectorAll('.ri-text strong')).map((el) => el.textContent?.trim());

    expect(labels).toEqual(['Start Hunt', 'Recruit', 'Contracts']);
    expect(rail!.textContent).toContain('2 dungeons');
    expect(rail!.textContent).toContain('1 ready');
    expect(rail!.textContent).not.toContain('Kaelis');
    expect(rail!.textContent).not.toContain('Backpack');
    expect(rail!.textContent).not.toContain('Bestiary');
  });
});
