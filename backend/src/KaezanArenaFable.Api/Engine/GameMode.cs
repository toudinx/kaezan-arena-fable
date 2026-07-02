using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Game mode identity (LM-03). Default <see cref="Dungeon"/> = legacy behavior
/// (procedural floors + boss). <see cref="Arena"/> is the wave-survival room (LM-04).
/// Stable IDs: the numeric value is part of the client↔hub contract — do not reorder.
/// </summary>
public enum GameMode { Dungeon = 0, Arena = 1, Training = 2 }

/// <summary>
/// Minimal mode/map seam (LM-03). Localizes the <b>3 only differences</b> between modes:
/// <b>(1)</b> map source, <b>(2)</b> population (pre-spawn vs. wave scheduler), and
/// <b>(3)</b> end condition, while keeping tick, movement, combat, snapshots, and rewards
/// shared in <see cref="GameWorld"/>.
///
/// A new mode plugs in here (map + spawn + end) without touching the combat pipeline.
/// Determinism: implementations use <b>only</b> the run <see cref="Rng"/>.
/// </summary>
public abstract class GameModeStrategy
{
    public abstract GameMode Id { get; }

    /// <summary>(1) Map source: produces run floors/rooms. Only consumes the run <paramref name="rng"/>.</summary>
    public abstract DungeonFloor[] BuildFloors(Rng rng, BiomeDef biome);

    /// <summary>(2) Initial population (pre-spawn). Called once during run construction.</summary>
    public abstract void Populate(GameWorld world);

    /// <summary>(2b) Continuous population per tick (for example, arena wave scheduler). No-op in dungeon.</summary>
    public virtual void OnTick(GameWorld world) { }

    /// <summary>(3) End condition fired when a monster dies (for example, boss defeated = victory).
    /// Player death is a shared end path that lives in <see cref="GameWorld"/>, not here.</summary>
    public virtual void OnMonsterKilled(GameWorld world, Actor monster) { }

    /// <summary>Resolves the strategy from the public enum coming from <c>JoinRun</c>.</summary>
    public static GameModeStrategy For(GameMode mode) => mode switch
    {
        GameMode.Arena => throw new NotImplementedException("Arena mode arrives in LM-04"),
        GameMode.Training => new TrainingModeStrategy(),
        _ => new DungeonModeStrategy()
    };
}

/// <summary>
/// Legacy mode: 2 procedural floors (floor 0 normal + floor 1 boss), pre-spawn by room and POIs;
/// ends by <b>defeating the boss</b> or by player death. Behavior is identical to the original run;
/// the seam only repositions the same decisions (LM-01 green).
/// </summary>
public sealed class DungeonModeStrategy : GameModeStrategy
{
    public override GameMode Id => GameMode.Dungeon;

    public override DungeonFloor[] BuildFloors(Rng rng, BiomeDef biome) =>
    [
        DungeonGenerator.Generate(rng, 0, isBossFloor: false, biome),
        DungeonGenerator.Generate(rng, 1, isBossFloor: true, biome)
    ];

    public override void Populate(GameWorld world)
    {
        world.SpawnFloorMonsters(0);
        world.SpawnFloorMonsters(1);
        world.SpawnPois();
    }

    public override void OnMonsterKilled(GameWorld world, Actor monster)
    {
        if (monster.IsBossActor)
            world.EndRun(true, $"{monster.Species!.Name} defeated");
    }
}

/// <summary>
/// Training Room: a single small fixed arena with one passive, huge-HP/regen dummy. A sandbox to test
/// kits, dashes and FX, and a debug stage. The dummy never attacks/chases and respawns if it ever dies,
/// so the run never clears or ends on its own — the player leaves with ESC. No rewards (see EndRun).
/// </summary>
public sealed class TrainingModeStrategy : GameModeStrategy
{
    public override GameMode Id => GameMode.Training;

    public override DungeonFloor[] BuildFloors(Rng rng, BiomeDef biome) =>
        [DungeonGenerator.GenerateTrainingRoom(rng, biome)];

    public override void Populate(GameWorld world) => world.SpawnTrainingDummy();

    public override void OnMonsterKilled(GameWorld world, Actor monster)
    {
        // Belt-and-braces: the dummy is built to survive (huge HP + regen), but if it ever dies, bring a
        // fresh one back so the sandbox stays usable and the run never soft-locks on an empty room.
        if (monster.IsTrainingDummy) world.SpawnTrainingDummy();
    }
}
