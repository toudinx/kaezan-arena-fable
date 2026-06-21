using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace BalanceSim;

/// <summary>Uma morte de monstro medida: TTK em ticks/ms/ciclos + flag de one-shot.</summary>
internal sealed record KillRow(
    string Kaeli, int Tier, long Seed,
    string Species, string Rank, int MaxHp,
    long TtkTicks, long TtkMs, double TtkCycles, bool OneShot);

/// <summary>Resumo de uma run inteira: vitória, duração, dano dado/sofrido, mortes, one-shots.</summary>
internal sealed record RunRow(
    string Kaeli, int Tier, long Seed,
    bool Victory, bool PlayerDied, bool Unfinished,
    long DurationMs, int Kills, long DamageDealt, long DamageTaken, int OneShotCount);

internal sealed record RunResult(RunRow Summary, List<KillRow> Kills);

/// <summary>
/// Roda uma run headless do <see cref="GameWorld"/> com o piloto-automático ligado e mede tudo a
/// partir do snapshot por tick (sem hooks no engine, preservando o determinismo). TTK vem do par
/// "primeiro dano" → "evento de morte"; one-shot = um hit único ≥ HP do alvo no início do tick.
/// </summary>
internal static class Simulator
{
    public static RunResult Run(GameWorld world, string kaeli, int tier, long seed, bool cards, int maxTicks)
    {
        var playerId = world.Player.Id;
        // MG-02: ciclo de ação = o intervalo-base de auto-attack do PAPEL da Kaeli (archer 1400 <
        // knight 1700 < mage 2000). Medir TTK em ciclos por papel mantém a comparação justa.
        var cycleMs = (double)GameConfig.Roles[world.Waifu.Role].BaseAutoAttackMs;

        // Liga o piloto: mira + skills + ult + auto-heal (+auto-cards se cards=full), navegação por loot
        // (anda sozinho coletando e indo até a saída/boss). Mira no mais próximo, cura a 50% de vida.
        var flags = 1 | 2 | 4 | GameConfig.AutoHelperAutoHealFlag
                    | (cards ? GameConfig.AutoHelperAutoCardsFlag : 0);
        var payload = $"{GameConfig.AutoHelperTargetPreferenceNearest}|{GameConfig.AutoHelperNavLoot}|50";
        world.Enqueue(new Command(CommandKind.ToggleAutoHelper, flags, GameConfig.AutoHelperMovementModeAvoidCode, payload));

        var firstDmgTick = new Dictionary<int, long>();
        var prevHp = new Dictionary<int, int>();   // HP no fim do tick anterior = início do tick atual
        var maxHp = new Dictionary<int, int>();
        var species = new Dictionary<int, string>();

        var kills = new List<KillRow>();
        long dmgDealt = 0, dmgTaken = 0;
        var oneShotCount = 0;

        long tick = 0;
        RunEndDto? ended = null;
        long lastElapsedMs = 0;

        while (true)
        {
            var (snap, _) = world.Tick();
            tick++;
            lastElapsedMs = snap.Run.ElapsedMs;

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
                        dmgDealt += e.Value;
                        firstDmgTick.TryAdd(e.ActorId, tick);
                        var threshold = prevHp.TryGetValue(e.ActorId, out var hp0) && hp0 > 0
                            ? hp0
                            : maxHp.GetValueOrDefault(e.ActorId);
                        if (threshold > 0 && e.Value >= threshold)
                            oneShotCount++;
                    }
                }
                else if (e.Kind == "death" && e.ActorId != playerId && maxHp.ContainsKey(e.ActorId))
                {
                    var id = e.ActorId;
                    var ttkTicks = tick - firstDmgTick.GetValueOrDefault(id, tick) + 1;
                    var ttkMs = ttkTicks * GameConfig.TickMs;
                    // one-shot da morte: caiu de HP cheio no mesmo tick em que tomou o 1º dano.
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

            // pós-tick: registra HP/maxHp/espécie dos vivos no andar atual (base p/ o próximo tick).
            foreach (var m in snap.Monsters)
            {
                prevHp[m.Id] = m.Hp;
                maxHp[m.Id] = m.MaxHp;
                species[m.Id] = m.Species;
            }

            if (snap.Run.Ended is not null) { ended = snap.Run.Ended; break; }
            if (tick >= maxTicks) break;
        }

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
            OneShotCount: oneShotCount);

        return new RunResult(summary, kills);
    }
}
