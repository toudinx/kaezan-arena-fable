import { Component, OnInit, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { RoleTuningRow } from '../../core/types';

/**
 * MG-05: role tuning table editor (Knight · Mage · Archer). Reads/writes
 * `/admin/content/role-tuning`; edits persist in `.data/content/role-tuning.json`, and the
 * next run reflects the new numbers (the Hub injects the active table into GameWorld).
 */
@Component({
  selector: 'app-role-tuning-editor',
  standalone: true,
  imports: [],
  template: `
    <section class="rt-editor">
      @if (status(); as st) {
        <div class="status" [class.ok]="st.kind === 'ok'" [class.err]="st.kind === 'err'">{{ st.msg }}</div>
      }

      <div class="rt-head">
        <div>
          <span class="eyebrow">Balance</span>
          <h2>Role tuning</h2>
          <p class="rt-desc">
            Auto vs skill damage, auto speed, range, and AOE size by role.
            Target orders: auto archer/knight &gt; mage · skill mage &gt; archer &gt; knight ·
            spd archer &gt; knight &gt; mage · range archer &gt; mage &gt; knight · aoe mage &gt; knight &gt; archer.
          </p>
        </div>
        <div class="rt-actions">
          <button class="secondary" type="button" [disabled]="busy()" (click)="reset()">Revert</button>
          <button class="primary" type="button" [disabled]="busy() || rows().length === 0" (click)="save()">
            {{ saving() ? 'Saving...' : 'Save tuning' }}
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="empty">Loading tuning...</div>
      } @else {
        <div class="rt-grid">
          @for (row of rows(); track row.role; let i = $index) {
            <article class="rt-card">
              <header><strong>{{ row.role }}</strong></header>
              <label>Auto damage (×)
                <input type="number" step="0.01" min="0.01" [value]="row.autoDmgMult"
                  (input)="setNum(i, 'autoDmgMult', $any($event.target).value)" />
              </label>
              <label>Skill damage (×)
                <input type="number" step="0.01" min="0.01" [value]="row.skillDmgMult"
                  (input)="setNum(i, 'skillDmgMult', $any($event.target).value)" />
              </label>
              <label>Auto speed (ms)
                <input type="number" step="50" min="400" [value]="row.baseAutoAttackMs"
                  (input)="setNum(i, 'baseAutoAttackMs', $any($event.target).value)" />
              </label>
              <label>Auto range (tiles)
                <input type="number" step="1" min="1" [value]="row.autoRange"
                  (input)="setNum(i, 'autoRange', $any($event.target).value)" />
              </label>
              <label>AOE scale (×)
                <input type="number" step="0.05" min="0.05" [value]="row.aoeScale"
                  (input)="setNum(i, 'aoeScale', $any($event.target).value)" />
              </label>
            </article>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .rt-editor { max-width: 980px; }
    .status { border: 1px solid; border-radius: 6px; font-size: 12px; margin-bottom: 12px; padding: 9px 11px; }
    .status.ok { background: #102a25; border-color: #22675d; color: #55e5cf; }
    .status.err { background: #32191e; border-color: #6d303b; color: #ff9aa5; }
    .rt-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; margin-bottom: 18px; border-bottom: 1px solid #29293a; padding-bottom: 14px; }
    .rt-head h2 { margin: 2px 0 0; font-size: 21px; } .rt-head p { color: #8c899d; font-size: 12px; margin: 4px 0 0; max-width: 560px; }
    .eyebrow { color: #2dd4bf; display: block; font-size: 9px; font-weight: 900; letter-spacing: 1.3px; text-transform: uppercase; }
    .rt-actions { display: flex; gap: 8px; flex-shrink: 0; }
    button { border: 1px solid transparent; border-radius: 5px; color: #d9d7e5; font: inherit; min-height: 37px; padding: 0 14px; font-size: 11px; font-weight: 900; }
    button:disabled { opacity: .55; }
    .primary { background: #1db9aa; color: #061d1a; } .secondary { background: #1b1b28; border-color: #313145; }
    .rt-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 12px; }
    .rt-card { background: #11111a; border: 1px solid #2b2b3c; border-radius: 8px; padding: 14px; }
    .rt-card header { border-bottom: 1px solid #29293a; margin-bottom: 10px; padding-bottom: 8px; }
    .rt-card header strong { font-size: 15px; font-weight: 900; color: #e8e6f0; }
    label { color: #89879b; display: flex; flex-direction: column; gap: 5px; font-size: 10px; font-weight: 800; margin-top: 10px; }
    input { background: #0e0e16; border: 1px solid #303043; border-radius: 5px; color: #e8e6f0; font: inherit; height: 36px; padding: 0 9px; outline: none; }
    input:focus { border-color: #26aa9d; }
    .empty { color: #77758c; padding: 60px 20px; text-align: center; }
    @media (max-width: 720px) { .rt-head { flex-direction: column; align-items: stretch; } }
  `],
})
export class RoleTuningEditor implements OnInit {
  readonly rows = signal<RoleTuningRow[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly status = signal<{ kind: 'ok' | 'err'; msg: string } | null>(null);

  busy(): boolean { return this.loading() || this.saving(); }

  constructor(private readonly api: ApiService) {}

  async ngOnInit(): Promise<void> {
    await this.reset();
    this.loading.set(false);
  }

  async reset(): Promise<void> {
    try {
      this.rows.set((await this.api.getAdminRoleTuning()).map((row) => ({ ...row })));
      this.status.set(null);
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    }
  }

  setNum(index: number, field: keyof Omit<RoleTuningRow, 'role'>, value: string): void {
    const parsed = +value;
    if (Number.isNaN(parsed)) return;
    this.rows.update((rows) => rows.map((row, i) => i === index ? { ...row, [field]: parsed } : row));
    this.status.set(null);
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.status.set(null);
    try {
      this.rows.set((await this.api.saveAdminRoleTuning(this.rows())).map((row) => ({ ...row })));
      this.status.set({ kind: 'ok', msg: 'Tuning saved. Upcoming runs already use these numbers.' });
    } catch (err) {
      this.status.set({ kind: 'err', msg: (err as Error).message });
    } finally {
      this.saving.set(false);
    }
  }
}
