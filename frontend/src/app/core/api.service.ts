import { Injectable, signal } from '@angular/core';
import { Account, Catalog, PullResponse } from './types';

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
    const cat = await this.request<Catalog>('GET', '/catalog');
    this.catalog.set(cat);
    return cat;
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

  async claimDaily(contractId: string): Promise<void> {
    await this.request('POST', '/dailies/claim', { contractId });
    await this.refreshAccount();
  }

  async sellItem(itemId: number, count: number): Promise<void> {
    await this.request('POST', '/items/sell', { itemId, count });
    await this.refreshAccount();
  }
}
