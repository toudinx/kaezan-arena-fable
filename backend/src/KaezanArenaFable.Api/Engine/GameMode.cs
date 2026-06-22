using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Identidade do modo de jogo (LM-03). Default <see cref="Dungeon"/> = comportamento legado
/// (andares procedurais + boss). <see cref="Arena"/> é a sala de sobrevivência por waves (LM-04).
/// IDs estáveis: o valor numérico é parte do contrato cliente↔hub — não reordenar.
/// </summary>
public enum GameMode { Dungeon = 0, Arena = 1 }

/// <summary>
/// Costura mínima de modo/mapa (LM-03). Localiza as <b>3 únicas diferenças</b> entre modos —
/// <b>(1)</b> fonte de mapa (como o lugar é produzido), <b>(2)</b> povoamento (pré-spawn vs.
/// agendador de waves) e <b>(3)</b> condição de fim — mantendo <i>compartilhados</i> no
/// <see cref="GameWorld"/> o tick, o movimento, o combate, o snapshot e a recompensa.
///
/// Um modo novo pluga aqui (mapa + spawn + fim) sem tocar o pipeline de combate. Determinismo:
/// implementações usam <b>apenas</b> o <see cref="Rng"/> da run (nada de <c>Random</c>/<c>DateTime</c>).
/// </summary>
public abstract class GameModeStrategy
{
    public abstract GameMode Id { get; }

    /// <summary>(1) Fonte de mapa: produz os andares/sala da run. Só consome o <paramref name="rng"/> da run.</summary>
    public abstract DungeonFloor[] BuildFloors(Rng rng, BiomeDef biome);

    /// <summary>(2) Povoamento inicial (pré-spawn). Chamado uma vez na construção da run.</summary>
    public abstract void Populate(GameWorld world);

    /// <summary>(2b) Povoamento contínuo por tick (ex.: agendador de waves da arena). No-op no dungeon.</summary>
    public virtual void OnTick(GameWorld world) { }

    /// <summary>(3) Condição de fim disparada na morte de um monstro (ex.: boss derrotado = vitória).
    /// A morte do jogador é fim compartilhado (vive no <see cref="GameWorld"/>), não aqui.</summary>
    public virtual void OnMonsterKilled(GameWorld world, Actor monster) { }

    /// <summary>Resolve a estratégia a partir do enum público (vindo do <c>JoinRun</c>).</summary>
    public static GameModeStrategy For(GameMode mode) => mode switch
    {
        GameMode.Arena => throw new NotImplementedException("modo Arena chega na LM-04"),
        _ => new DungeonModeStrategy()
    };
}

/// <summary>
/// Modo legado: 2 andares procedurais (andar 0 normal + andar 1 boss), pré-spawn por sala e POIs;
/// fim por <b>derrotar o boss</b> (vitória) ou pela morte do jogador (compartilhado). Comportamento
/// idêntico ao da run original — a costura só reposiciona as mesmas decisões (LM-01 verde).
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
            world.EndRun(true, $"{monster.Species!.Name} derrotado");
    }
}
