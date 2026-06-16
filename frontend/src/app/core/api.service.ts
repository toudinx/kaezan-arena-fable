import { Injectable, signal } from '@angular/core';
import {
  Account,
  AdminItem,
  AdminItemsPayload,
  Catalog,
  DungeonTier,
  KaeliAuthoringMetadata,
  KaeliSkinDefinition,
  MonsterAuthoringMetadata,
  MonsterDefinition,
  PullResponse,
} from './types';

/** REST client for meta systems (account, gacha, dailies, inventory). */
@Injectable({ providedIn: 'root' })
export class ApiService {
  readonly account = signal<Account | null>(null);
  readonly catalog = signal<Catalog | null>(null);

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const res = await fetch(`/api/v1${path}`, {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    });
    const json = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error((json as { error?: string }).error ?? `HTTP ${res.status}`);
    return json as T;
  }

  async loadCatalog(): Promise<Catalog> {
    if (this.catalog()) return this.catalog()!;
    return this.reloadCatalog();
  }

  /** Re-busca o catálogo ignorando o cache (ex.: depois de editar conteúdo no painel admin). */
  async reloadCatalog(): Promise<Catalog> {
    const cat = await this.request<Catalog>('GET', '/catalog');
    this.catalog.set(cat);
    return cat;
  }

  // ---- admin: autoria de conteúdo ----
  async getAdminTiers(): Promise<DungeonTier[]> {
    return this.request<DungeonTier[]>('GET', '/admin/content/tiers');
  }

  async saveAdminTiers(tiers: DungeonTier[]): Promise<DungeonTier[]> {
    const saved = await this.request<DungeonTier[]>('PUT', '/admin/content/tiers', tiers);
    await this.reloadCatalog(); // /hunt e dailies passam a refletir a edição
    return saved;
  }

  async getMonsterAuthoringMetadata(): Promise<MonsterAuthoringMetadata> {
    return this.request<MonsterAuthoringMetadata>('GET', '/admin/monster-authoring');
  }

  async getAuthoredMonsters(): Promise<MonsterDefinition[]> {
    return this.request<MonsterDefinition[]>('GET', '/admin/content/monsters');
  }

  async createAuthoredMonster(monster: MonsterDefinition): Promise<MonsterDefinition> {
    const saved = await this.request<MonsterDefinition>('POST', '/admin/content/monsters', monster);
    await this.reloadCatalog();
    return saved;
  }

  async updateAuthoredMonster(monster: MonsterDefinition): Promise<MonsterDefinition> {
    const saved = await this.request<MonsterDefinition>(
      'PUT',
      `/admin/content/monsters/${encodeURIComponent(monster.id)}`,
      monster,
    );
    await this.reloadCatalog();
    return saved;
  }

  async deleteAuthoredMonster(id: string): Promise<void> {
    await this.request('DELETE', `/admin/content/monsters/${encodeURIComponent(id)}`);
    await this.reloadCatalog();
  }

  // ---- admin: Outfit Studio (skins de Kaeli) ----
  async getKaeliAuthoringMetadata(): Promise<KaeliAuthoringMetadata> {
    return this.request<KaeliAuthoringMetadata>('GET', '/admin/kaeli-authoring');
  }

  async getAuthoredKaeliSkins(): Promise<KaeliSkinDefinition[]> {
    return this.request<KaeliSkinDefinition[]>('GET', '/admin/content/kaeli-skins');
  }

  async createKaeliSkin(skin: KaeliSkinDefinition): Promise<KaeliSkinDefinition> {
    const saved = await this.request<KaeliSkinDefinition>('POST', '/admin/content/kaeli-skins', skin);
    await this.reloadCatalog();
    return saved;
  }

  async updateKaeliSkin(skin: KaeliSkinDefinition): Promise<KaeliSkinDefinition> {
    const saved = await this.request<KaeliSkinDefinition>(
      'PUT',
      `/admin/content/kaeli-skins/${encodeURIComponent(skin.id)}`,
      skin,
    );
    await this.reloadCatalog();
    return saved;
  }

  async deleteKaeliSkin(id: string): Promise<void> {
    await this.request('DELETE', `/admin/content/kaeli-skins/${encodeURIComponent(id)}`);
    await this.reloadCatalog();
  }

  async reorderKaeliSkins(waifuId: string, orderedIds: string[]): Promise<KaeliSkinDefinition[]> {
    const saved = await this.request<KaeliSkinDefinition[]>(
      'POST', '/admin/content/kaeli-skins/reorder', { waifuId, orderedIds });
    await this.reloadCatalog();
    return saved;
  }

  // ---- admin: editor de itens ----
  async getAdminItems(): Promise<AdminItemsPayload> {
    return this.request<AdminItemsPayload>('GET', '/admin/items');
  }

  async createAdminItem(item: AdminItem): Promise<AdminItem> {
    const saved = await this.request<AdminItem>('POST', '/admin/items', item);
    await this.reloadCatalog();
    return saved;
  }

  async updateAdminItem(item: AdminItem): Promise<AdminItem> {
    const saved = await this.request<AdminItem>('PUT', `/admin/items/${item.itemId}`, item);
    await this.reloadCatalog();
    return saved;
  }

  async deleteAdminItem(itemId: number): Promise<void> {
    await this.request('DELETE', `/admin/items/${itemId}`);
    await this.reloadCatalog();
  }

  async grantKaeros(amount = 1600): Promise<void> {
    await this.request('POST', '/admin/grant-kaeros', { amount });
    await this.refreshAccount();
  }

  async grantAdminItem(itemId: number, count = 1): Promise<void> {
    await this.request('POST', `/admin/items/${itemId}/grant`, { count });
    await this.refreshAccount();
  }

  async refreshAccount(): Promise<Account> {
    const acc = await this.request<Account>('GET', '/account');
    this.account.set(acc);
    return acc;
  }

  async pull(bannerId: string, count: number): Promise<PullResponse> {
    const res = await this.request<PullResponse>('POST', '/gacha/pull', { bannerId, count });
    await this.refreshAccount();
    return res;
  }

  async setActiveWaifu(waifuId: string): Promise<void> {
    await this.request('POST', '/account/active-waifu', { waifuId });
    await this.refreshAccount();
  }

  async ascend(waifuId: string): Promise<void> {
    await this.request('POST', '/waifus/ascend', { waifuId });
    await this.refreshAccount();
  }

  async giftItem(waifuId: string, itemId: number): Promise<{ xpGained: number; favorite: boolean; level: number; giftsLeftToday: number; notes: string[] }> {
    const res = await this.request<{ xpGained: number; favorite: boolean; level: number; giftsLeftToday: number; notes: string[] }>(
      'POST', '/kaelis/gift', { waifuId, itemId });
    await this.refreshAccount();
    return res;
  }

  async selectSkin(waifuId: string, skinId: string): Promise<void> {
    await this.request('POST', '/kaelis/skin/select', { waifuId, skinId });
    await this.refreshAccount();
  }

  async buySkin(waifuId: string, skinId: string): Promise<void> {
    await this.request('POST', '/kaelis/skin/buy', { waifuId, skinId });
    await this.refreshAccount();
  }

  async unlockMasteryNode(waifuId: string, nodeId: string): Promise<void> {
    await this.request('POST', '/kaelis/mastery/unlock', { waifuId, nodeId });
    await this.refreshAccount();
  }

  async respecMastery(waifuId: string): Promise<void> {
    await this.request('POST', '/kaelis/mastery/respec', { waifuId });
    await this.refreshAccount();
  }

  async claimDaily(contractId: string): Promise<void> {
    await this.request('POST', '/dailies/claim', { contractId });
    await this.refreshAccount();
  }

  async sellItem(itemId: number, count: number): Promise<void> {
    await this.request('POST', '/items/sell', { itemId, count });
    await this.refreshAccount();
  }

  async equipItem(waifuId: string, slot: string, itemId: number): Promise<void> {
    await this.request('POST', '/equipment/equip', { waifuId, slot, itemId });
    await this.refreshAccount();
  }

  async unequipItem(waifuId: string, slot: string): Promise<void> {
    await this.request('POST', '/equipment/unequip', { waifuId, slot });
    await this.refreshAccount();
  }
}
