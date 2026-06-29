using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>A measured monster death: TTK in ticks/ms/cycles + one-shot flag.</summary>
internal sealed record KillRow(
    string Kaeli, int Tier, long Seed,
    string Species, string Rank, int MaxHp,
    long TtkTicks, long TtkMs, double TtkCycles, bool OneShot);

/// <summary>Whole-run summary: victory, duration, damage dealt/taken, deaths, one-shots.</summary>
/// <remarks>MG-08: EndHpFraction = player HP at run end (post-boss on victory -> "ends with
/// 30-60% HP"); MinHpFraction = lowest HP seen during the run (survival pressure, less masked by
/// auto-heal). Both are read from snapshots only — pure reporting, no engine changes.</remarks>
/// <remarks>F-E posture calibration: BossMaxHp + BossBreakDamage/BossTotalDamage isolate how much
/// of the boss death comes from Echo Break windows (BreakCount = number of breaks; PeakWindowDamage
/// = largest effective damage dealt in one window). Snapshot-only reporting.</remarks>
internal sealed record RunRow(
    string Kaeli, int Tier, long Seed,
    bool Victory, bool PlayerDied, bool Unfinished,
    long DurationMs, int Kills, long DamageDealt, long DamageTaken, int OneShotCount,
    double EndHpFraction, double MinHpFraction,
    int BossMaxHp, long BossTotalDamage, long BossBreakDamage, int BreakCount, long PeakWindowDamage);

internal sealed record RunResult(RunRow Summary, List<KillRow> Kills);

/// <summary>
/// Runs a headless <see cref="GameWorld"/> with autopilot enabled and measures everything from
/// per-tick snapshots (no engine hooks, preserving determinism). TTK comes from the pair
/// "first damage" -> "death event"; one-shot = a single hit >= target HP at tick start.
/// </summary>
internal static class Simulator
{
    public static RunResult Run(GameWorld world, string kaeli, int tier, long seed, bool cards, int maxTicks)
    {
        var playerId = world.Player.Id;
        // MG-02: action cycle = the base auto-attack interval of the Kaeli role (archer 1400 <
        // knight 1700 < mage 2000). Measuring TTK in role cycles keeps comparisons fair.
        var cycleMs = (double)GameConfig.Roles[world.Waifu.Role].BaseAutoAttackMs;

        // Enable autopilot: targeting + skills + ult + auto-heal (+auto-cards when cards=full),
        // loot navigation (walks to collect and then to exit/boss). Targets nearest, heals at 50% HP.
        var flags = 1 | 2 | 4 | GameConfig.AutoHelperAutoHealFlag
                    | (cards ? GameConfig.AutoHelperAutoCardsFlag : 0);
        var payload = $"{GameConfig.AutoHelperTargetPreferenceNearest}|{GameConfig.AutoHelperNavLoot}|50";
        world.Enqueue(new Command(CommandKind.ToggleAutoHelper, flags, GameConfig.AutoHelperMovementModeAvoidCode, payload));

        var firstDmgTick = new Dictionary<int, long>();
        var prevHp = new Dictionary<int, int>();   // HP at previous tick end = current tick start
        var maxHp = new Dictionary<int, int>();
        var species = new Dictionary<int, string>();
        // MG-06: dmgDealt = EFFECTIVE damage (capped to target remaining HP). The engine emits the
        // full hit even on overkill, so crit/burst kits inflated the number and distorted parity.
        // Capping here is pure reporting and does not touch the engine.
        var effHp = new Dictionary<int, int>();    // remaining effective HP, resynced from snapshots

        var kills = new List<KillRow>();
        long dmgDealt = 0, dmgTaken = 0;
        var oneShotCount = 0;

        // F-E posture: Echo Break window contribution to boss death (EFFECTIVE damage, capped to
        // remaining HP so overkill does not inflate it). bossId comes from MonsterDto.IsBoss.
        int bossId = 0, bossMaxHp = 0, breakCount = 0, prevPostureCycle = 0;
        long bossTotalDamage = 0, bossBreakDamage = 0, peakWindowDamage = 0, currentWindowDamage = 0;
        var prevStaggered = false;

        long tick = 0;
        RunEndDto? ended = null;
        long lastElapsedMs = 0;
        double endHpFrac = 1.0, minHpFrac = 1.0;

        while (true)
        {
            var (snap, _) = world.Tick();
            tick++;
            lastElapsedMs = snap.Run.ElapsedMs;

            // MG-08: player HP fraction this tick — last = run end; minimum = worst moment.
            if (snap.Player.MaxHp > 0)
            {
                endHpFrac = (double)snap.Player.Hp / snap.Player.MaxHp;
                if (endHpFrac < minHpFrac) minHpFrac = endHpFrac;
            }

            foreach (var e in snap.Events)
            {
                if (e.Kind == "damage")
                {
                    if (e.ActorId == playerId)
                    {
                        dmgTaken += e.Value;
                    }
                    else
                    {
                        var id = e.ActorId;
                        firstDmgTick.TryAdd(id, tick);
                        // Effective damage: never count more than the target still had.
                        var rem = effHp.TryGetValue(id, out var r) ? r : maxHp.GetValueOrDefault(id, e.Value);
                        var eff = Math.Min(e.Value, Math.Max(0, rem));
                        dmgDealt += eff;
                        effHp[id] = rem - e.Value;
                        // F-E: EFFECTIVE damage dealt to the boss; stagger-window damage counts as break damage.
                        if (id == bossId)
                        {
                            bossTotalDamage += eff;
                            if (snap.Run.BossStaggered) { bossBreakDamage += eff; currentWindowDamage += eff; }
                        }
                        var threshold = prevHp.TryGetValue(id, out var hp0) && hp0 > 0
                            ? hp0
                            : maxHp.GetValueOrDefault(id);
                        if (threshold > 0 && e.Value >= threshold)
                            oneShotCount++;
                    }
                }
                else if (e.Kind == "death" && e.ActorId != playerId && maxHp.ContainsKey(e.ActorId)
                         && !world.IsSummonedMonster(e.ActorId))
                {
                    // MG-08: summoned units are transient fixed-HP adds that do not belong to the
                    // tier x rank cell; excluding them keeps TTK calibration clean.
                    var id = e.ActorId;
                    var ttkTicks = tick - firstDmgTick.GetValueOrDefault(id, tick) + 1;
                    var ttkMs = ttkTicks * GameConfig.TickMs;
                    // Death one-shot: dropped from full HP in the same tick as the first damage.
                    var died = prevHp.GetValueOrDefault(id, maxHp[id]);
                    var oneShot = firstDmgTick.GetValueOrDefault(id, tick) == tick
                                  && died >= maxHp[id];
                    kills.Add(new KillRow(
                        kaeli, tier, seed,
                        species.GetValueOrDefault(id, "?"),
                        world.MonsterRank(id) ?? "common",
                        maxHp[id], ttkTicks, ttkMs, ttkMs / cycleMs, oneShot));
                }
            }

            // Post-tick: record HP/maxHp/species for live monsters on the current floor.
            foreach (var m in snap.Monsters)
            {
                prevHp[m.Id] = m.Hp;
                maxHp[m.Id] = m.MaxHp;
                effHp[m.Id] = m.Hp;   // resync effective HP from snapshot, covering regen/healing
                species[m.Id] = m.Species;
                if (m.IsBoss) { bossId = m.Id; bossMaxHp = m.MaxHp; }
            }

            // F-E: count breaks (PostureCycle rises on each Echo Break) and close the peak window
            // when stagger ends (largest effective damage dumped into a single break).
            if (snap.Run.BossPostureCycle > prevPostureCycle)
                breakCount += snap.Run.BossPostureCycle - prevPostureCycle;
            prevPostureCycle = snap.Run.BossPostureCycle;
            if (prevStaggered && !snap.Run.BossStaggered)
            {
                peakWindowDamage = Math.Max(peakWindowDamage, currentWindowDamage);
                currentWindowDamage = 0;
            }
            prevStaggered = snap.Run.BossStaggered;

            if (snap.Run.Ended is not null) { ended = snap.Run.Ended; break; }
            if (tick >= maxTicks) break;
        }

        // Boss killed inside an open window -> close the last peak.
        peakWindowDamage = Math.Max(peakWindowDamage, currentWindowDamage);

        var victory = ended?.Victory ?? false;
        var summary = new RunRow(
            kaeli, tier, seed,
            victory,
            PlayerDied: ended is not null && !victory,
            Unfinished: ended is null,
            DurationMs: ended?.DurationMs ?? lastElapsedMs,
            Kills: kills.Count,
            DamageDealt: dmgDealt,
            DamageTaken: dmgTaken,
            OneShotCount: oneShotCount,
            EndHpFraction: endHpFrac,
            MinHpFraction: minHpFrac,
            BossMaxHp: bossMaxHp,
            BossTotalDamage: bossTotalDamage,
            BossBreakDamage: bossBreakDamage,
            BreakCount: breakCount,
            PeakWindowDamage: peakWindowDamage);

        return new RunResult(summary, kills);
    }
}
