# Gameplay Rework: Card Cadence · Card-less Baseline · Ordered Chaos — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Card choices become rare and heavy (floor clear + Echo Sanctuary only), the base kit clears floors without any card, and mob area attacks only fire when they actually threaten the player (with size caps for non-boss mobs).

**Architecture:** Three independent seams in the deterministic engine: (1) `OfferCardBeat` call sites + offer constants in `GameConfig`; (2) a proximity/size gate inside `TryMonsterAttacks`/`CanAttackPlayer` backed by pure static rules on `GameConfig` (unit-testable); (3) numeric recalibration of `MonsterStatLines` via the existing headless `tools/BalanceSim` (`--cards none` already exists). Spec: `docs/superpowers/specs/2026-07-06-gameplay-cards-chaos-design.md`.

**Tech Stack:** ASP.NET Core net8.0 (`backend/src/KaezanArenaFable.Api`), xUnit (`backend/tests/KaezanArenaFable.Api.Tests`), BalanceSim console (`tools/BalanceSim`), Angular frontend (build-check only).

## Global Constraints

- All code + comments in **English**; player-visible strings in English (CLAUDE.md language policy).
- **Never use `var`** in new or modified C# code — declare explicit types (user preference 2026-07-06; overrides the surrounding codebase style; do not rewrite untouched lines just for this).
- **Determinism:** inside the tick use only the run `Rng` (`_rng`); no `Random`/`DateTime.Now`/unstable iteration.
- **Every simulation constant lives in `Domain/GameConfig.cs`** — never hardcode gameplay values elsewhere.
- Stable IDs (`card:*`, `echo:*`, species names) are never renamed.
- Work directly on `main`; selective staging (never commit unrelated files, e.g. `docs/.obsidian/*`).
- Before finishing any task: `dotnet build` (backend) passes; the final task also requires `npx ng build`.
- Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Replay note: Tasks 2–3 change Rng consumption and replay serialization → old recorded replays become invalid by design; the battery is regenerated in Task 3.

---

### Task 1: Baseline sweeps (BEFORE any change)

**Model · Effort:** Sonnet · low — mechanical command runs; only care point is copying the pivots faithfully.

Capture the current balance state so every number changed later is justified against it (MG-08 methodology).

**Files:**
- Create: `docs/balance/cards_rework_before_full.csv` (generated)
- Create: `docs/balance/cards_rework_before_none.csv` (generated)

**Interfaces:**
- Produces: the two "before" CSVs that Task 4 compares against.

- [ ] **Step 1: Run the two sweeps** (from repo root; takes a few minutes each)

```powershell
dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards full --out docs/balance/cards_rework_before_full.csv
dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards none --out docs/balance/cards_rework_before_none.csv
```

Expected: each prints `== determinism canary ==` with `PASS`, the TTK pivots, and `CSV written to ...`. If the canary FAILS, stop — the engine is non-deterministic and that must be fixed first.

- [ ] **Step 2: Save the console pivot output**

Copy the `== TTK vs. TARGET (MG-08) ==` table from both runs into a scratch note (you will need the `obsTTK` and `xHP` columns in Task 4). Do not skip: the CSVs have raw rows, the pivot is the readable summary.

- [ ] **Step 3: Commit**

```powershell
git add docs/balance/cards_rework_before_full.csv docs/balance/cards_rework_before_none.csv
git commit -m @'
docs(balance): baseline sweeps before card-cadence rework (cards full/none)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: Card cadence — offers only on floor clear + Echo Sanctuary

**Model · Effort:** Sonnet · medium — multi-site but fully specified edits; the blessed-plumbing removal table lists every touch point.

Elite kills and chests stop opening card offers; they pay out directly. Blessed-offer plumbing is removed. Cap drops 9→4 and the rarity curve is retuned so the Kaeli's echo is reachable by the 2nd choice.

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (~lines 374, 386–397, 780–796)
- Modify: `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (~lines 264–270, 3846–3848, 4236–4320, 4410–4420, 5190–5192)
- Modify: `backend/src/KaezanArenaFable.Api/Engine/GameWorld.Replay.cs` (~line 144)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/CardCadenceTests.cs` (create)

**Interfaces:**
- Consumes: existing `GrantGearMaterial(int x, int y)`, `EmitLootFly(int spriteId, string label, int x, int y, bool isGold)`, `CardValue(string stat)`, `_gold`, `Tier.StatMultiplier`.
- Produces: `OfferCardBeat(CardOfferBeat beat)` gated by `GameConfig.OpensCardOffer`, `GameConfig.MaxCardChoicesPerRun == 4`, retuned `GameConfig.CardRarityWeight`, and elite/chest direct reward beats with no blessed-offer state.

**Completion note (2026-07-06):** implemented in `27eca38 feat: reduce card choice cadence`. The implementation keeps elite gold on the existing loot path (`DropLoot`/`DropKaezanLoot`) and adds direct Echo material, instead of adding new elite-gold constants. Verified with `dotnet test -c Release`, `dotnet build -c Release`, `npx ng build`, and `dotnet run --project tools/BalanceSim -c Release -- --seeds 5 --tier 1 --cards full`.

- [x] **Step 1: Write the failing tests**

Create `backend/tests/KaezanArenaFable.Api.Tests/CardCadenceConfigTests.cs`:

```csharp
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

public class CardCadenceConfigTests
{
    [Fact]
    public void EchoIsReachableBySecondChoice()
    {
        // With MaxCardChoicesPerRun = 4, the 2nd choice sits at progress 1/3. The echo weight
        // there must be meaningful so the run-defining pick can appear early (pre-rework: ~20).
        double echo = GameConfig.CardRarityWeight(Cards.Echo, 1.0 / 3);
        Assert.True(echo >= 25, $"echo weight at 2nd choice = {echo}");
    }

    [Fact]
    public void CommonsNoLongerDominateLateOffers()
    {
        double common = GameConfig.CardRarityWeight(Cards.Common, 1.0);
        Assert.True(common < GameConfig.CardRarityWeight(Cards.Rare, 1.0));
        Assert.True(common < GameConfig.CardRarityWeight(Cards.Echo, 1.0));
    }

    [Fact]
    public void RunOffersStayScarce()
    {
        // Expected organic beats: 1 floor-clear + up to 2 sanctuaries = 3; cap gives 1 margin.
        Assert.True(GameConfig.MaxCardChoicesPerRun <= 4,
            $"cap = {GameConfig.MaxCardChoicesPerRun} — scarcity is the whole point of the rework");
    }
}
```

- [x] **Step 2: Run tests to verify the new expectations fail**

```powershell
dotnet test backend/tests/KaezanArenaFable.Api.Tests -c Release --filter CardCadenceConfigTests
```

Expected: `EchoIsReachableBySecondChoice` FAILS (current weight at 1/3 = 20) and `RunOffersStayScarce` FAILS (cap is 9). `CommonsNoLongerDominateLateOffers` may already pass (common 22 < rare 52/echo 46) — that is fine, it is a regression guard.

- [x] **Step 3: Update `GameConfig.cs` — cap, rarity curve, elite reward, drop blessed const**

At line 374–376, change the cap and its comment:

```csharp
    /// <summary>Card choice cap per run. Cadence rework (2026-07-06): choices come ONLY from
    /// clearing a floor and the optional Echo Sanctuary (~3 organic beats + 1 margin), so each
    /// pick carries real build weight. Elites/chests pay out directly instead.</summary>
    public const int MaxCardChoicesPerRun = 4;
```

Replace the `CardRarityWeight` switch arms (lines 390–396) — with only ~3 picks the echo must be reachable by the 2nd choice:

```csharp
        return rarity switch
        {
            Cards.Common => Lerp(72, 20, progress),
            Cards.Rare => Lerp(40, 50, progress),
            Cards.Echo => Lerp(18, 55, progress),
            _ => Lerp(72, 20, progress),
        };
```

In the G-09 chest section, DELETE the `BlessedOfferProgress` const and its comment (lines 782–783), and ADD the elite payout constants right after `CardRerollGoldCost`:

```csharp
    /// <summary>Cadence rework (2026-07-06): an elite kill pays out directly (gold + Echo
    /// material) instead of opening a card offer — the dopamine beat stays, the overlay goes.</summary>
    public const int EliteRewardGoldMin = 60;
    public const int EliteRewardGoldMax = 140;
```

- [x] **Step 4: Update `GameWorld.cs` — elite beat becomes a direct payout**

At lines 3846–3847 replace:

```csharp
        // G-06: defeating a common-room elite is a beat: grants a heavy card choice.
        if (monster.IsElite && !monster.IsSummon) OfferCardBeat();
```

with:

```csharp
        // Cadence rework (2026-07-06): the elite beat pays out directly (gold + Echo material)
        // instead of opening a card offer — choices live on floor clear and the Echo Sanctuary.
        if (monster.IsElite && !monster.IsSummon)
        {
            long eliteGold = (long)(_rng.Range(GameConfig.EliteRewardGoldMin, GameConfig.EliteRewardGoldMax)
                * Tier.StatMultiplier * (1 + CardValue("goldPercent")));
            _gold += eliteGold;
            EmitLootFly(GameConfig.GoldCoinItemId, $"+{eliteGold} gold", monster.X, monster.Y, isGold: true);
            GrantGearMaterial(monster.X, monster.Y);
        }
```

- [x] **Step 5: Update `GameWorld.cs` — chest stops offering cards; remove blessed plumbing**

In `TryInteract` (lines 5190–5191) DELETE:

```csharp
        // altar: opens a card offer (overlay reuses reroll/ban/shop). Cursed = blessed offer.
        OfferCardBeat(blessed: cursed);
```

(the cursed chest already pays `CursedChestMaterialDrops` materials just above — that stays its reward). Then remove the blessed machinery entirely. Reference list of every `_offerBlessed`/blessed site:

| Site | Change |
|---|---|
| `GameWorld.cs:268` field `_offerBlessed` | delete field |
| `GameWorld.cs:4255` `OfferCardBeat(bool blessed = false)` | signature → `OfferCardBeat()`; body line 4262 `if (_pendingOffer is null) OfferCards(blessed);` → `if (_pendingOffer is null) OfferCards();` |
| `GameWorld.cs:4295` `OfferCards(bool blessed = false)` | signature → `OfferCards()`; delete `_offerBlessed = blessed;` (4300) and change line 4302 `{ _offerBlessed = false; return; }` → `return;` |
| `GameWorld.cs:4308-4310` `OfferProgress` property | delete property; replace its usages (grep `OfferProgress` — used in `DrawCardOffer`) with `RunChoiceProgress` |
| `GameWorld.cs:4417` `else _offerBlessed = false;` | delete line |
| `GameWorld.Replay.cs:144` `w.Write(_offerBlessed); w.Write(_cardRerollsRemaining);` | → `w.Write(_cardRerollsRemaining);` |

Update the stale comments that describe the old cadence:
- `GameWorld.cs:4238` "beats (elite/floor/sanctuary)" → "beats (floor clear / sanctuary)".
- `GameWorld.cs:4252` doc of `OfferCardBeat` → "grants a heavy card choice on a fixed beat (cleared floor, Echo Sanctuary room)".
- `GameWorld.cs:3861` chest-drop comment "(ambush + blessed offer)" → "(ambush + extra materials)".
- `GameWorld.cs:657-658` (`SpawnPois`) same "blessed offer" mention → "extra materials".

- [x] **Step 6: Sweep for leftovers**

```powershell
Select-String -Path backend\src\KaezanArenaFable.Api\**\*.cs -Pattern "blessed" -SimpleMatch
Select-String -Path frontend\src\app\**\*.ts -Pattern "blessed" -SimpleMatch
```

Expected: no hits in backend (or only in strings you just rewrote); if the frontend references a blessed flag on the card offer overlay, delete that branch too (the DTO never carried it — expect zero hits).

- [x] **Step 7: Build + run tests**

```powershell
dotnet build backend/src/KaezanArenaFable.Api
dotnet test backend/tests/KaezanArenaFable.Api.Tests -c Release --filter CardCadenceConfigTests
```

Expected: build clean; 3/3 PASS.

- [x] **Step 8: Smoke the cadence in the simulator**

```powershell
dotnet run --project tools/BalanceSim -c Release -- --seeds 5 --tier 1 --cards full
```

Expected: canary PASS, runs finish (`win` counts > 0). This proves the offer path still resolves (the sim auto-picks offers) with the new beats.

- [x] **Step 9: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs backend/src/KaezanArenaFable.Api/Engine/GameWorld.Replay.cs backend/tests/KaezanArenaFable.Api.Tests/CardCadenceConfigTests.cs
git commit -m @'
feat(cards): offers only on floor clear + sanctuary; elites/chests pay out directly

Cap 9->4, rarity curve favors rare/echo earlier, blessed-offer plumbing removed.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: Ordered chaos — intent gate + shape caps for mob AoE

**Model · Effort:** Opus · medium — deterministic tick code; exact replacement blocks are given, but Rng-order and replay implications demand care.

Every mob area cast must land near the player; non-boss shapes are capped at dragon-fire-wave scale. No telegraphs (kept by design). Includes replay battery regeneration (Rng consumption order changes).

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (add cast-rule consts + statics near `MonsterDamageTuning`, ~line 117; retune `MonsterBehaviorProfiles` ~lines 275–284)
- Modify: `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (`CanAttackPlayer` ~4687–4701, `TryMonsterAttacks` ranged branch ~4748–4790, new private helper)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/MonsterCastRuleTests.cs` (create)

**Interfaces:**
- Consumes: `Actor.IsBossActor`, `ConeTiles(int x, int y, int dx, int dy, int reach)`, `CircleTiles(int x, int y, int radius)`, `Chebyshev`, `(int dx, int dy) DirDelta(Dir facing, Actor? target)`, `Dir FacingFrom(int dx, int dy, Dir? previous = null)`.
- Produces: `GameConfig.MonsterConeReach(bool isBoss, int length)`, `GameConfig.MonsterAoeRadius(bool isBoss, int radius)`, `GameConfig.SelfCenteredAoeInRange(int dist, int radius)`, consts `MonsterAoeProximityMargin=2`, `MonsterConeReachCap=3`, `MonsterAoeRadiusCap=2`, `BossConeReachCap=5`, `BossAoeRadiusCap=4`; private `GameWorld.ConeLandsNearPlayer`.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/KaezanArenaFable.Api.Tests/MonsterCastRuleTests.cs`:

```csharp
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

public class MonsterCastRuleTests
{
    [Fact]
    public void NonBossConeIsCappedAtDragonWaveReach()
    {
        Assert.Equal(3, GameConfig.MonsterConeReach(isBoss: false, length: 8));
        Assert.Equal(2, GameConfig.MonsterConeReach(isBoss: false, length: 2)); // never inflates
    }

    [Fact]
    public void NonBossAoeRadiusIsCapped()
    {
        Assert.Equal(2, GameConfig.MonsterAoeRadius(isBoss: false, radius: 4));
        Assert.Equal(1, GameConfig.MonsterAoeRadius(isBoss: false, radius: 1));
    }

    [Fact]
    public void BossKeepsBigShapesAsSignature()
    {
        Assert.Equal(5, GameConfig.MonsterConeReach(isBoss: true, length: 8));
        Assert.Equal(4, GameConfig.MonsterAoeRadius(isBoss: true, radius: 6));
    }

    [Fact]
    public void SelfCenteredAoeOnlyFiresNearThePlayer()
    {
        // radius 2 + margin 2: legit up to Chebyshev 4, gated at 5 ("UE at an empty corner").
        Assert.True(GameConfig.SelfCenteredAoeInRange(dist: 4, radius: 2));
        Assert.False(GameConfig.SelfCenteredAoeInRange(dist: 5, radius: 2));
    }

    [Fact]
    public void RetunedProfilesRespectTheCaps()
    {
        foreach (MonsterBehaviorProfile profile in GameConfig.MonsterBehaviorProfiles)
        foreach (MonsterAttackPattern atk in profile.Attacks)
        {
            Assert.True(atk.Length <= GameConfig.MonsterConeReachCap,
                $"{profile.Id}: cone length {atk.Length} > cap");
            Assert.True(atk.Radius <= GameConfig.MonsterAoeRadiusCap,
                $"{profile.Id}: radius {atk.Radius} > cap");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test backend/tests/KaezanArenaFable.Api.Tests -c Release --filter MonsterCastRuleTests
```

Expected: compile error (`MonsterConeReach` not defined) — that counts as the failing state.

- [ ] **Step 3: Add cast rules to `GameConfig.cs`**

Right after `MonsterDamageTuning` (~line 117), add:

```csharp
    // ---- ordered chaos (2026-07-06): mob AoE discipline ----
    /// <summary>Proximity margin (Chebyshev tiles) for a mob area/cone cast to be legitimate:
    /// the shape does not need to contain the player, but must land within this margin of them.
    /// Kills the "UE at an empty corner" noise — every area FX on screen is a real threat.
    /// No telegraph by design (open-map direction).</summary>
    public const int MonsterAoeProximityMargin = 2;
    /// <summary>Shape caps applied at cast time (covers canary-imported and authored kits alike).
    /// Non-boss reference: Tibia dragon fire wave (~reach 3). Bosses keep bigger shapes as a
    /// dramatic signature — a giant UE reads as boss language, not mob noise.</summary>
    public const int MonsterConeReachCap = 3;
    public const int MonsterAoeRadiusCap = 2;
    public const int BossConeReachCap = 5;
    public const int BossAoeRadiusCap = 4;

    public static int MonsterConeReach(bool isBoss, int length) =>
        Math.Min(length, isBoss ? BossConeReachCap : MonsterConeReachCap);

    public static int MonsterAoeRadius(bool isBoss, int radius) =>
        Math.Min(radius, isBoss ? BossAoeRadiusCap : MonsterAoeRadiusCap);

    /// <summary>A self-centered area cast is worth attempting only when the player is inside
    /// the (capped) radius plus the proximity margin.</summary>
    public static bool SelfCenteredAoeInRange(int dist, int radius) =>
        dist <= radius + MonsterAoeProximityMargin;
```

- [ ] **Step 4: Retune the two oversized behavior profiles**

In `MonsterBehaviorProfiles`: artillery's second pattern `new("spell", 6, 3, 0, 0, true, 4400, 35, 0.95, 1.38, true, 0)` → radius **2**; breather's cone `new("spell", 0, 0, 4, 2, false, 3100, 62, 0.72, 1.18, true, 0)` → length **3**. Add one line to each profile's comment noting the 2026-07-06 cap alignment.

- [ ] **Step 5: Gate the casts in `GameWorld.cs`**

Replace the ranged branch of `TryMonsterAttacks` (the `else` block at lines 4748–4790) with:

```csharp
            else
            {
                int range = attack.Range > 0 ? attack.Range : (attack.Radius > 0 || attack.Length > 0 ? 7 : 1);
                if (dist > range) continue;
                if (!HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y)) continue;

                // Ordered chaos (2026-07-06): shape caps by rank + intent gate, checked BEFORE
                // arming the cooldown — a gated mob simply hasn't attempted the cast yet and can
                // fire the moment the player is close enough. No more AoE bursting at nothing.
                int coneReach = attack.Length > 0
                    ? GameConfig.MonsterConeReach(monster.IsBossActor, attack.Length) : 0;
                int aoeRadius = attack.Radius > 0
                    ? GameConfig.MonsterAoeRadius(monster.IsBossActor, attack.Radius) : 0;
                Dir facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);
                if (attack.Length > 0)
                {
                    (int gdx, int gdy) = DirDelta(facing, Player);
                    if (!ConeLandsNearPlayer(monster, gdx, gdy, coneReach)) continue;
                }
                else if (aoeRadius > 0 && !attack.Target
                         && !GameConfig.SelfCenteredAoeInRange(dist, aoeRadius)) continue;

                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;

                monster.Facing = facing;

                if (attack.ShootEffect > 0)
                    Emit("projectile", monster.X, monster.Y, Player.X, Player.Y, attack.ShootEffect);

                if (attack.Length > 0)
                {
                    // wave (e.g. dragon fire): cone toward player, capped at dragon-wave reach
                    (int dx, int dy) = DirDelta(monster.Facing, Player);
                    bool hitPlayer = false;
                    foreach ((int tx, int ty) in ConeTiles(monster.X, monster.Y, dx, dy, coneReach))
                    {
                        if (attack.AreaEffect > 0) Emit("effect", tx, ty, 0, 0, attack.AreaEffect);
                        if (tx == Player.X && ty == Player.Y) hitPlayer = true;
                    }
                    if (hitPlayer) HitPlayerWithAttack(monster, attack);
                }
                else if (aoeRadius > 0)
                {
                    int cx = attack.Target ? Player.X : monster.X;
                    int cy = attack.Target ? Player.Y : monster.Y;
                    bool hitPlayer = false;
                    foreach ((int tx, int ty) in CircleTiles(cx, cy, aoeRadius))
                    {
                        if (attack.AreaEffect > 0) Emit("effect", tx, ty, 0, 0, attack.AreaEffect);
                        if (tx == Player.X && ty == Player.Y) hitPlayer = true;
                    }
                    if (hitPlayer) HitPlayerWithAttack(monster, attack);
                }
                else
                {
                    if (attack.AreaEffect > 0) Emit("effect", Player.X, Player.Y, 0, 0, attack.AreaEffect);
                    HitPlayerWithAttack(monster, attack);
                }
            }
```

(Behavior deltas vs the old code: `Math.Min(attack.Length, 5)` → capped `coneReach`; raw `attack.Radius` → capped `aoeRadius`; gate before `AttackReadyAtMs`/`Chance` so a gated attempt costs neither cooldown nor an Rng roll.)

Add the private helper next to `CanAttackPlayer`:

```csharp
    /// <summary>Ordered chaos: a cone cast is legitimate only if the player stands inside the
    /// cone or within MonsterAoeProximityMargin tiles of its nearest tile.</summary>
    private bool ConeLandsNearPlayer(Actor monster, int dx, int dy, int reach)
    {
        foreach ((int tx, int ty) in ConeTiles(monster.X, monster.Y, dx, dy, reach))
            if (Chebyshev(tx, ty, Player.X, Player.Y) <= GameConfig.MonsterAoeProximityMargin)
                return true;
        return false;
    }
```

- [ ] **Step 6: Align `CanAttackPlayer` so gated mobs keep closing distance**

A mob whose only shot is a gated self-nova must not plant at range 7 doing nothing (`StaticAttackChance` stop). Replace the body of `CanAttackPlayer` (lines 4687–4701) with:

```csharp
    private bool CanAttackPlayer(Actor monster, int dist, bool hasLos)
    {
        foreach (MonsterAttack attack in monster.Species!.Attacks)
        {
            if (attack.Kind == "melee")
            {
                if (dist <= GameConfig.MeleeRange) return true;
                continue;
            }

            int range = attack.Range > 0 ? attack.Range : (attack.Radius > 0 || attack.Length > 0 ? 7 : 1);
            // Ordered chaos: a gated shape does not justify planting — only count shapes that
            // could legitimately fire from this distance (cap + proximity margin).
            if (attack.Length > 0)
                range = Math.Min(range, GameConfig.MonsterConeReach(monster.IsBossActor, attack.Length)
                    + GameConfig.MonsterAoeProximityMargin);
            else if (attack.Radius > 0 && !attack.Target)
                range = Math.Min(range, GameConfig.MonsterAoeRadius(monster.IsBossActor, attack.Radius)
                    + GameConfig.MonsterAoeProximityMargin);
            if (dist <= range && hasLos) return true;
        }
        return false;
    }
```

- [ ] **Step 7: Build + run tests**

```powershell
dotnet build backend/src/KaezanArenaFable.Api
dotnet test backend/tests/KaezanArenaFable.Api.Tests -c Release
```

Expected: build clean; all tests PASS (including Task 2's).

- [ ] **Step 8: Determinism + generator golden + fresh replay battery**

```powershell
dotnet run --project tools/BalanceSim -c Release -- --golden-check
dotnet run --project tools/BalanceSim -c Release -- --seeds 5 --tier 1 --save-replays $env:TEMP\kaezan-replays
dotnet run --project tools/BalanceSim -c Release -- --replay-check $env:TEMP\kaezan-replays
```

Expected: golden-check PASS (the dungeon generator is untouched); the sweep canary PASS; replay-check PASS on the freshly recorded battery. Replays recorded before this task are invalid by design (Rng order + replay serialization changed) — do NOT run `--replay-check` against old `.data/replays` files; they cycle out on their own (`ReplayKeepLast`).

- [ ] **Step 9: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs backend/tests/KaezanArenaFable.Api.Tests/MonsterCastRuleTests.cs
git commit -m @'
feat(engine): intent gate + shape caps for mob AoE (ordered chaos)

Area/cone casts must land within 2 tiles of the player; non-boss cones cap at
reach 3 (dragon-wave scale) and radii at 2; bosses keep 5/4 as signature.
Artillery/breather profiles retuned to match. Replay battery rebaselined.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: Balance calibration — card-less baseline carries the clear

**Model · Effort:** Fable (or Opus) · high — iterative judgment loop reading sweep pivots and choosing levers; the numbers cannot be pre-written.

Recalibrate so `--cards none` clears floors ~25–35% slower than target and `--cards full` (now ~3 picks) sits on the MG-08 targets. Iterative loop with explicit acceptance; every changed number justified by the sweeps.

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (`MonsterStatLines` ~lines 218–236; possibly `PlayerDamageMult` ~566 / `AtkPerRunLevel` ~567)
- Create: `docs/balance/cards_rework_after_full.csv`, `docs/balance/cards_rework_after_none.csv` (generated)

**Interfaces:**
- Consumes: Task 1 "before" CSVs/pivots; Tasks 2–3 already merged (they change the observed numbers).
- Produces: recalibrated constants + the two "after" CSVs.

**Acceptance (all from the `== TTK vs. TARGET (MG-08) ==` and run-summary pivots):**
- `--cards full`: cross-Kaeli median TTK within ±1 cycle of targets (common 3 · elite 6 · boss 12); boss never < 8 cycles; zero `ONE-SHOT` cells; mage/archer deaths ~0 on T1–T3.
- `--cards none`: run completes (win rate comparable to full, deaths ~0 for mage/archer on T1–T3); median TTK between ~1.25× and ~1.35× of the same targets (common ~3.8–4.1 · elite ~7.5–8.1 · boss ~15–16).

- [ ] **Step 1: Post-rework sweep (both modes)**

```powershell
dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards full
dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards none
```

Record both TTK-target pivots. With ~6 fewer picks the `full` sweep will read **slow** vs targets — that gap is what the calibration removes.

- [ ] **Step 2: Calibration loop (repeat until acceptance holds)**

1. From the `--cards full` pivot, take the `xHP` column (suggested factor = target/observed) per `tier×rank` cell and multiply the corresponding `Health` value in `MonsterStatLines` (round to 2–3 significant digits; keep the existing comment style noting the sweep date).
2. Re-run the `--cards full` sweep (`--seeds 30`). Iterate until every cell is within ±1 cycle and no one-shots.
3. Then check `--cards none`: if the card-less gap is **> 1.35×** target, nudge `PlayerDamageMult` up by 0.05 and re-run BOTH modes (step 2 will re-shrink Health); if **< 1.25×**, cards feel irrelevant — nudge `PlayerDamageMult` down by 0.05 and re-run both.
4. If mage/archer deaths spike on `--cards none`, lower the `Damage` column for the offending tier (MG-08 order: Health first, Damage only for deaths).

Each iteration is `edit GameConfig.cs → dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards <mode>`. Expect 2–4 iterations.

- [ ] **Step 3: Final sweeps with CSV output**

```powershell
dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards full --out docs/balance/cards_rework_after_full.csv
dotnet run --project tools/BalanceSim -c Release -- --seeds 30 --cards none --out docs/balance/cards_rework_after_none.csv
```

Expected: acceptance table above fully green; determinism canary PASS on both.

- [ ] **Step 4: Build + full test suite**

```powershell
dotnet build backend/src/KaezanArenaFable.Api
dotnet test backend/tests/KaezanArenaFable.Api.Tests -c Release
```

Expected: clean build, all PASS.

- [ ] **Step 5: Commit**

```powershell
git add backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs docs/balance/cards_rework_after_full.csv docs/balance/cards_rework_after_none.csv
git commit -m @'
feat(balance): card-less baseline carries the clear; cards are the build edge

MonsterStatLines recalibrated for the 3-pick cadence: cards-full on MG-08
targets, cards-none ~30% slower with deaths ~0 (see docs/balance CSVs).

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 5: Docs, frontend build, push

**Model · Effort:** Sonnet · low — README prose + build gates + push.

**Files:**
- Modify: `README.md` (run-loop / cards section — describe the new cadence and payout beats)
- Verify: `frontend` build only (no expected code change)

**Interfaces:**
- Consumes: everything merged in Tasks 2–4.

- [ ] **Step 1: Update README**

Find the section describing the run/card loop (`Select-String -Path README.md -Pattern "card"`). Rewrite it to state: card choices happen on floor clear and at the optional Echo Sanctuary (cap 4/run); elites pay gold + Echo material; chests are pure loot (gold, tier items, materials; cursed = ambush + extra materials); mob area attacks only fire near the player, with non-boss shapes capped (cone 3 / radius 2) and boss shapes big by design. Keep README voice/format.

- [ ] **Step 2: Frontend build check**

```powershell
Set-Location frontend; npx ng build; Set-Location ..
```

Expected: build clean (the card overlay is snapshot-driven; nothing consumed the removed beats).

- [ ] **Step 3: Final gate + push**

```powershell
dotnet build backend/src/KaezanArenaFable.Api
dotnet test backend/tests/KaezanArenaFable.Api.Tests -c Release
git add README.md
git commit -m @'
docs(readme): document new card cadence, elite/chest payouts, and AoE discipline

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
git push origin main
```

Expected: build/tests clean, push accepted.

- [ ] **Step 4: Manual feel pass (user-facing verification)**

Start the canonical backend (`tools/run-backend.ps1`, Release) + frontend, play 1–2 runs and confirm by feel: ~1–2 card overlays per floor; a full floor is clearable ignoring offers (let the timeout auto-pick be disabled or just observe pace); no mob AoE FX far from the Kaeli; cones read at dragon-wave scale; boss still gets big dramatic shapes. Report deviations back into `GameConfig` feel-tuning (out of this plan's acceptance).
