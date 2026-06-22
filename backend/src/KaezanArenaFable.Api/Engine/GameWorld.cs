using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

public enum Dir { North = 0, East = 1, South = 2, West = 3 }

public sealed class Actor
{
    public int Id;
    public bool IsPlayer;
    public MonsterType? Species;
    public int Floor;
    public int X, Y;
    public int FromX, FromY;
    public long StepStartMs;
    public int StepDurMs;
    public Dir Facing = Dir.South;
    public int Hp, MaxHp;
    public long StunUntilMs;
    public long[] AttackReadyAtMs = [];
    public long NextWanderAtMs;
    public long NextVoiceAtMs;
    public int TargetId;
    public long LastSawPlayerAtMs;
    public long AggroOutOfRangeSinceMs;
    public long ExposedUntilMs;
    public long SappedUntilMs;
    public bool IsBossActor;
    public bool IsElite;        // G-06: elite de sala comum — derrotá-lo concede um beat de escolha
    public bool IsMimic;        // G-09: baú-Eco corrompido — dropa material de gear ao morrer
    public double StatMult = 1.0;

    // monster kit (T-53): reactive defenses, summon timers, self-haste
    public long[] DefenseReadyAtMs = [];
    public long[] SummonReadyAtMs = [];
    public int OwnerId; // > 0 when this actor was summoned by another monster
    public long HasteUntilMs;
    public double HasteFactor = 1.0;

    // slow applied by the player (trait chiller/shatter)
    public long SlowUntilMs;
    public double SlowFactor = 1.0;

    // G-08B: escudeiro — barreira de eco que absorve dano antes da vida (concedida por um aliado escudeiro).
    public double MonsterShield;
    public long ShieldCastReadyAtMs; // cooldown do próprio escudeiro entre concessões de barreira

    // K-04 signature trait state carried per target.
    public int SinStacks;       // Eloa — Selo de Julgamento (3 = Julgado, próximo acerto detona)
    public long SinUntilMs;
    public int DecayStacks;     // Velvet — Maldição Acumulada (sobe o limiar de execução)
    public long DecayUntilMs;
    public int FrostHits;       // Lunara — acertos no alvo lento até estilhaçar
    public bool IsPrey;         // Gaia — alvo marcado como Presa (HUD/render)
    public bool Killed;         // guard: KillMonster processa cada morte uma única vez

    // DoT left on this monster by a player skill (necromancer wither / eternal suffering).
    public readonly List<MonsterDot> Dots = [];

    // F-E: boss posture (echo break). Only populated on boss actors (PostureMax > 0).
    public double PostureBaseMax;
    public double PostureMax;
    public double Posture;
    public int PostureCycle;
    public long StaggerUntilMs;
    public double StaggerMultiplier = 1.0;
    public long PostureLastHitMs;
    public long PostureBonusReadyAtMs;

    // F-E: elemental reaction mark (any actor).
    public string ElementMark = "";
    public long ElementMarkUntilMs;

    public bool IsSummon => OwnerId != 0;
    public bool IsStaggered(long nowMs) => nowMs < StaggerUntilMs;
    public string ActiveMark(long nowMs) => nowMs < ElementMarkUntilMs ? ElementMark : "";
    public bool IsMoving(long nowMs) => nowMs < StepStartMs + StepDurMs;
    public bool IsStunned(long nowMs) => nowMs < StunUntilMs;
}

/// <summary>A DoT ticking on the player (tibia condition applied by a monster attack).</summary>
public sealed class ActiveCondition
{
    public string Type = "";
    public int DamagePerTick;
    public int TicksLeft;
    public int TickMs;
    public long NextTickAtMs;
}

/// <summary>A DoT ticking on a monster, applied by a player skill (necromancer Wither/Eternal Suffering).</summary>
public sealed class MonsterDot
{
    public string Element = "";
    public int Fx;
    public double DamagePerTick;
    public int TicksLeft;
    public int TickMs;
    public long NextTickAtMs;
}

/// <summary>
/// A stationary construct summoned by the player (necromancer Bone Construct). It pulses area
/// damage around its tile for a while, then expires. Kept off the monster list so it never
/// interferes with player targeting/auto-attack — it is pure scheduled area damage.
/// </summary>
public sealed class PlayerSummon
{
    public int Floor, X, Y;
    public string Element = "";
    public int Fx;
    public int Radius;
    public double DamagePerPulse;
    public int PulseMs;
    public long NextPulseAtMs;
    public long ExpireAtMs;
    public bool IsEchoSpectre; // G-04: espectro da Colheita (Velvet), contado p/ o cap de 5.
}

/// <summary>A hazard tile painted by a player skill (shape "field"): damages/slows the monster
/// standing on it each tick until it expires. Terrain modification — fire patch, frost patch, etc.</summary>
public sealed class GroundField
{
    public int Floor, X, Y;
    public string Element = "";
    public int Fx;
    public double DamagePerTick;
    public double SlowFactor = 1;
    public int SlowMs;
    public int TickMs;
    public long NextTickAtMs;
    public long ExpireAtMs;
}

/// <summary>A future area hit scheduled by a multi-time skill (shape "barrage", delayed nukes,
/// expanding rings). Resolved deterministically when NowMs reaches AtMs. Damage/DoT are
/// pre-computed at cast time so they don't drift with buffs that change mid-flight.</summary>
public sealed class ScheduledStrike
{
    public int Floor, X, Y;
    public long AtMs;
    public string Element = "";
    public int Fx;
    public double Damage;
    public int Radius;
    public int RingInner;
    public int StunMs;
    public double SlowFactor = 1;
    public int SlowMs;
    public int DotTicks, DotTickMs;
    public double DotPower;
}

public sealed class GroundItem
{
    public int Id;
    public int Floor, X, Y;
    public int ItemId;
    public int Count;
    public string Name = "";
}

public sealed class Poi
{
    public int Id;
    public string Kind = "chest"; // chest | sanctuary | ladder
    // G-09: variante do baú — "" (comum) | "cursed" (amaldiçoado, telegrafado) | "mimic" (oculto do cliente).
    public string Variant = "";
    public int Floor, X, Y;
    public bool Used;
}

public enum CommandKind { SetMoveDir, SetTarget, CastSkill, ToggleStance, ToggleAutoHelper, Interact, ChooseCard, RerollCards, BanCard, Abandon, UsePotion }

public sealed record Command(CommandKind Kind, int A, int B, string? S);

/// <summary>
/// One dungeon run: server-authoritative world ticked at GameConfig.TickMs.
/// Movement, monster AI, combat, loot and run-cards all live here.
/// Deterministic for a given seed + command timeline.
/// </summary>
/// Persistence belongs to Meta and is only invoked at run boundaries; never add DB/EF access here.
public sealed class GameWorld
{
    public readonly long Seed;
    public readonly GameMode Mode;
    public readonly DungeonTier Tier;
    public readonly WaifuDef Waifu;
    public readonly ClassDef PlayerClass;
    public readonly int Ascension;
    public readonly EquipmentStats EquipmentStats;
    public readonly KaeliLoadout Loadout;
    private readonly TraitDef _trait;
    private readonly double _traitMult;        // amplificação do trait via maestria (ramo Eco)
    private readonly double _affinityStatBonus; // +1% ATK/HP por nível de afinidade acima de 1
    private readonly double _gaugeRate;        // maestria × trait overcharge
    private readonly GameData _data;
    private readonly ItemRegistry? _items;
    private readonly MonsterRegistry _monsterRegistry;
    private readonly Rng _rng;
    // LM-03: costura de modo — localiza fonte-de-mapa, povoamento e condição de fim.
    private readonly GameModeStrategy _modeRules;
    private readonly IReadOnlyDictionary<string, long> _bestiaryKills;
    // MG-05: tabela de tuning por papel vigente nesta run (injetada pela Hub do ContentStore).
    private readonly IReadOnlyDictionary<KaeliRole, RoleTuning> _roles;

    public long TickCount { get; private set; }
    private long _simulationMs;
    private long NowMs => _simulationMs;

    private readonly DungeonFloor[] _floors;
    private int _currentFloor;
    private int _nextActorId = 1;

    public Actor Player { get; }
    private readonly List<Actor> _monsters = [];
    private readonly List<GroundItem> _groundItems = [];
    private readonly List<Poi> _pois = [];
    private readonly List<EventDto> _events = [];
    private readonly Queue<Command> _commands = new();
    private readonly object _commandLock = new();

    // run progression
    private int _runLevel = 1;
    private long _runXp;
    private long _gold;
    private int _kills;
    private double _gauge;
    private readonly Dictionary<string, int> _cards = [];
    private readonly HashSet<string> _bannedCards = new(StringComparer.Ordinal);
    private List<CardOfferDto>? _pendingOffer;
    private long _cardOfferStartedTick;
    private int _queuedOffers;
    private int _choicesOffered; // G-06: escolhas de carta já concedidas em beats (teto + progresso)
    private bool _offerBlessed;  // G-09: oferta abençoada (baú amaldiçoado) — pondera raro/eco
    private int _cardRerollsRemaining = GameConfig.CardRerollsPerRun;
    public Dictionary<string, int> KillsBySpecies { get; } = [];
    public List<RewardItemDto> ItemsLooted { get; } = [];
    private int _potionCharges = GameConfig.PotionChargesPerRun;
    private long _potionReadyAtMs;
    private int _chestsOpened;
    public int ChestsOpened => _chestsOpened;

    // player combat state
    private readonly long[] _skillReadyAtMs = new long[4];
    private string _stanceId;
    private long _autoAttackReadyAtMs;
    private bool _autoHelperTargeting = true;
    private bool _autoHelperSkills = true;
    private bool _autoHelperUltimate = true;
    private string _autoHelperTargetPreference = GameConfig.AutoHelperTargetPreferenceNearest;
    private string _autoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
    private string _savedAutoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
    private string _defaultAutoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
    // G-10: automações do helper (estilo autoplay) — ligadas por default. Auto-heal usa a poção
    // quando a vida cai abaixo de _autoHelperHealPct%; navMode ("off"/"loot") faz o helper caminhar
    // sozinho coletando baús/altares e indo pra saída; autoCards pega a carta de maior raridade.
    private bool _autoHelperAutoHeal = true;
    private int _autoHelperHealPct = GameConfig.AutoHelperHealPctDefault;
    private string _autoHelperNavMode = GameConfig.AutoHelperNavLoot;
    private bool _autoHelperAutoCards = true;
    // marca quando o andar atual começou — o auto-loot espera AutoLootStartDelayMs antes de andar.
    private long _floorEnteredMs;
    private int _helperMovementOverrideTargetId;
    private int _manualTargetId;
    private int _moveDirX, _moveDirY; // held movement direction (-1..1)
    private int _bufferedMoveDirX, _bufferedMoveDirY;
    private bool _hasBufferedMoveDir;
    private long _moveDirChangedAtMs;
    private readonly Dictionary<string, long> _buffsUntilMs = [];
    private double _regenCarry;
    private readonly List<ActiveCondition> _playerConditions = [];
    private readonly List<PlayerSummon> _summons = [];
    private readonly List<GroundField> _fields = [];
    private readonly List<ScheduledStrike> _pendingStrikes = [];
    private long _playerSlowUntilMs;
    private double _playerSlowFactor = 1.0;

    // K-04 signature trait state carried per Kaeli (the player side of the passive).
    private int _comboTargetId;          // Seren — Disciplina: alvo do ramp atual
    private int _comboHits;              // acertos consecutivos no alvo
    private long _comboExpireMs;         // zera o ramp se passar sem bater
    private double _staticCharge;        // Rynna — Carga Estática (0..RynnaChargeMax)
    private int _preyId;                 // Gaia — Presa: id do alvo marcado
    private long _preyStartMs;           // início da caça (ramp por tempo)
    private long _preyHuntBonusUntilMs;  // janela de cadência após uma execução
    private long _traitHasteUntilMs;     // Lunara — haste do Estilhaçar (move speed)
    private double _traitHasteFactor = 1.0;
    private long _contagionNextJumpMs;   // Rin — Contágio: próximo salto periódico do burn
    private int _cardDoubleStrikeHits;   // G-04 — Golpe Duplo: contador de acertos diretos

    // G-04B: estado vivo dos Ecos por Kaeli (cap de 1 stack; presença = HasEcho).
    private double _echoShield;          // Eloa Mártir / Velvet Pacto: sobre-vida que absorve dano
    private int _eloaSentenceStacks;     // Eloa Sentença: amplificação acumulada do próximo estouro
    private int _preyId2;                // Gaia Matilha: segunda Presa simultânea

    public RunEndDto? Ended { get; private set; }
    public bool MapDirty { get; private set; } = true;

    /// <summary>
    /// MG-01 (tools/BalanceSim): rank de um monstro por id — "common" | "elite" | "boss" — para o
    /// simulador classificar TTK sem ter de inferir do pool da dungeon. Leitura pura: não toca estado
    /// nem o <c>_rng</c>, então não perturba o determinismo. Mortos não são removidos da lista
    /// (ficam com <c>Killed=true</c>), então o rank continua consultável no tick da morte.
    /// </summary>
    public string? MonsterRank(int monsterId) =>
        _monsters.FirstOrDefault(m => m.Id == monsterId) is { } m
            ? m.IsBossActor ? "boss" : m.IsElite ? "elite" : "common"
            : null;

    /// <summary>
    /// MG-08 (tools/BalanceSim): true se o monstro foi conjurado por outro (OwnerId != 0). O simulador
    /// exclui conjurados da calibração de TTK — são adds transitórios (ex.: o summoner gera Ecoídes T1
    /// em qualquer tier), não o comum/elite/boss daquela célula, e poluiriam a mediana. Leitura pura.
    /// </summary>
    public bool IsSummonedMonster(int monsterId) =>
        _monsters.FirstOrDefault(m => m.Id == monsterId) is { IsSummon: true };

    public GameWorld(long seed, DungeonTier tier, WaifuDef waifu, int ascension,
        GameData data, MonsterRegistry monsterRegistry, IReadOnlyDictionary<string, long> bestiaryKills,
        EquipmentStats? equipmentStats = null, KaeliLoadout? loadout = null, ItemRegistry? items = null,
        string? helperProfile = null, IReadOnlyDictionary<KaeliRole, RoleTuning>? roleTuning = null,
        GameMode mode = GameMode.Dungeon)
    {
        Seed = seed;
        Mode = mode;
        _modeRules = GameModeStrategy.For(mode);
        Tier = tier;
        Waifu = waifu;
        // MG-05: a run lê a tabela vigente injetada (editável no admin); cai nos defaults de
        // GameConfig.Roles quando nada é passado (ex.: simulador, que mede contra a baseline estável).
        _roles = roleTuning ?? GameConfig.Roles;
        PlayerClass = Classes.ById.GetValueOrDefault(waifu.ClassId)
                      ?? throw new InvalidOperationException($"classe desconhecida: {waifu.ClassId}");
        _stanceId = PlayerClass.InitialStance(waifu.Element).Id;
        Ascension = ascension;
        EquipmentStats = equipmentStats ?? Domain.EquipmentStats.Empty;
        Loadout = loadout ?? KaeliLoadout.Default(waifu);
        _trait = waifu.Trait;
        _traitMult = Loadout.Mastery.TraitMult;
        _affinityStatBonus = (Loadout.AffinityLevel - 1) * GameConfig.AffinityStatBonusPerLevel;
        _gaugeRate = Loadout.Mastery.GaugeMult
                     * (_trait.Kind is "overcharge" or "static_charge" ? 1 + _trait.Value * _traitMult : 1);
        _data = data;
        _items = items;
        _monsterRegistry = monsterRegistry;
        _bestiaryKills = bestiaryKills;
        _rng = new Rng((ulong)seed);
        // MG-02: o default de movimento (seguir vs kitar) segue o range do papel, não mais a arma.
        var isMelee = _roles[Waifu.Role].AutoRange <= GameConfig.MeleeRange;
        _autoHelperTargetPreference = GameConfig.AutoHelperTargetPreferenceNearest;
        _defaultAutoHelperMovementMode = isMelee
            ? GameConfig.AutoHelperMovementModeFollow
            : GameConfig.AutoHelperMovementModeAvoid;
        _autoHelperMovementMode = _defaultAutoHelperMovementMode;
        if (!string.IsNullOrWhiteSpace(helperProfile)) ApplyHelperProfile(helperProfile);

        var biome = Biomes.ForTier(tier.Tier);
        // LM-03 (1) fonte de mapa: o modo decide como o lugar é produzido (mesma seq. de Rng no Dungeon).
        _floors = _modeRules.BuildFloors(_rng, biome);

        var hp = (int)(waifu.BaseHp * (1 + ascension * GameConfig.AscensionAtkBonus)
                       * (1 + _affinityStatBonus) * Loadout.Mastery.HpMult)
                 + EquipmentStats.MaxHpBonus;
        Player = new Actor
        {
            Id = _nextActorId++,
            IsPlayer = true,
            Floor = 0,
            X = _floors[0].Entry.X,
            Y = _floors[0].Entry.Y,
            FromX = _floors[0].Entry.X,
            FromY = _floors[0].Entry.Y,
            Hp = hp,
            MaxHp = hp
        };

        // LM-03 (2) povoamento: o modo decide o pré-spawn (salas no Dungeon; waves na Arena).
        _modeRules.Populate(this);
    }

    // ================= spawn =================

    internal void SpawnFloorMonsters(int floorIndex)
    {
        var floor = _floors[floorIndex];
        foreach (var room in floor.Rooms)
        {
            switch (room.Role)
            {
                case "entry": continue;
                // G-06: o Santuário de Eco é seguro — o player reivindica a carta sem briga.
                case "sanctuary": continue;
                case "boss":
                    SpawnBossRoom(floorIndex, room);
                    continue;
                // G-07: miniboss = mini-clímax do detour (1 elite reforçado + escolta).
                case "miniboss":
                    SpawnMiniBossRoom(floorIndex, room);
                    continue;
            }

            // echo-spots style budget spawn: commons cost 2, elites cost 5
            var sizeFactor = room.W * room.H / 49.0;
            var budget = (int)(GameConfig.SpawnBudgetBase * (1 + (Tier.Tier - 1) * GameConfig.SpawnBudgetTierGrowth) * Math.Clamp(sizeFactor, 0.6, 1.4));
            // G-07: tesouro = menos guardas; evento/risco = mais guardas (swarm); elite = pacote de elites.
            if (room.Role == "treasure") budget = budget / 2 + 2;
            else if (room.Role == "hazard") budget = (int)(budget * GameConfig.HazardBudgetMult);
            var forceElite = room.Role == "elite";
            var elitesSpawned = 0;
            var guard = 0;
            while (budget > 0 && guard++ < 50)
            {
                var elite = forceElite
                    ? budget >= 5 && elitesSpawned < GameConfig.EliteRoomMaxElites
                    : budget >= 5 && _rng.Chance(0.25);
                var name = elite ? _rng.Pick(Tier.EliteMobs) : _rng.Pick(Tier.CommonMobs);
                // G-06: só elites de salas comuns viram beat (guardas de boss/emboscada não contam).
                // G-08B: o custo do comum vem do perfil (swarm = 1 → dobra a contagem, pressão numérica).
                var cost = elite ? 5 : SpawnCostFor(name);
                if (SpawnMonster(floorIndex, name, room, isElite: elite) is not null)
                {
                    budget -= cost;
                    if (elite) elitesSpawned++;
                }
                else break;
            }
        }
    }

    /// <summary>G-07: sala de miniboss — um elite reforçado (beat de escolha ao morrer) + escolta de comuns.</summary>
    private void SpawnMiniBossRoom(int floorIndex, Room room)
    {
        var mini = SpawnMonster(floorIndex, _rng.Pick(Tier.EliteMobs), room, isElite: true);
        if (mini is not null)
            mini.MaxHp = mini.Hp = (int)(mini.Hp * GameConfig.MiniBossHpScale);
        for (var i = 0; i < GameConfig.MiniBossEscort; i++)
            SpawnMonster(floorIndex, _rng.Pick(Tier.CommonMobs), room, isElite: false);
    }

    private void SpawnBossRoom(int floorIndex, Room room)
    {
        var boss = SpawnMonster(floorIndex, Tier.Boss, room, isBoss: true);
        if (boss is not null)
        {
            if (!boss.Species!.IsAuthored)
                boss.MaxHp = boss.Hp = (int)(boss.Hp * GameConfig.BossHpScale(Tier.Boss));
            boss.PostureBaseMax = GameConfig.PostureBaseMax * (1 + (Tier.Tier - 1) * GameConfig.PostureTierGrowth);
            boss.PostureMax = boss.PostureBaseMax;
        }
        for (var i = 0; i < 2 + Tier.Tier / 2; i++)
            SpawnMonster(floorIndex, _rng.Pick(Tier.EliteMobs), room);
    }

    private Actor? SpawnMonster(int floorIndex, string speciesName, Room room, bool isBoss = false, bool isElite = false)
    {
        var species = _monsterRegistry.Get(speciesName);
        var floor = _floors[floorIndex];
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var x = _rng.Range(room.X, room.X + room.W - 1);
            var y = _rng.Range(room.Y, room.Y + room.H - 1);
            if (floor.IsBlocked(x, y) || OccupiedBy(floorIndex, x, y) is not null) continue;

            var mult = species.IsAuthored ? 1 : Tier.StatMultiplier;
            var actor = new Actor
            {
                Id = _nextActorId++,
                Species = species,
                Floor = floorIndex,
                X = x, Y = y, FromX = x, FromY = y,
                Hp = (int)(species.Health * mult),
                MaxHp = (int)(species.Health * mult),
                AttackReadyAtMs = new long[Math.Max(species.Attacks.Count, 1)],
                DefenseReadyAtMs = new long[species.Defenses.Count],
                SummonReadyAtMs = new long[species.Summons.Count],
                NextWanderAtMs = _rng.Range(0, GameConfig.MonsterWanderIntervalMs),
                IsBossActor = isBoss,
                IsElite = isElite,
                StatMult = mult,
                Facing = (Dir)_rng.Next(4)
            };
            // G-08B: tanque-de-postura — mob comum/elite ganha barra de Postura (Echo Break) escalada pelo perfil.
            if (!isBoss && GameConfig.BehaviorProfile(species.BehaviorId) is { PostureScale: > 0 } postureProfile)
            {
                actor.PostureBaseMax = GameConfig.PostureBaseMax * postureProfile.PostureScale
                                       * (1 + (Tier.Tier - 1) * GameConfig.PostureTierGrowth);
                actor.PostureMax = actor.PostureBaseMax;
            }
            _monsters.Add(actor);
            return actor;
        }
        return null;
    }

    /// <summary>G-08B: custo de orçamento de spawn do comum (swarm custa 1 → enche a sala de chaff).</summary>
    private int SpawnCostFor(string speciesName) =>
        GameConfig.BehaviorProfile(_monsterRegistry.Get(speciesName).BehaviorId)?.SpawnCost ?? 2;

    internal void SpawnPois()
    {
        var nextPoi = 1;
        for (var f = 0; f < _floors.Length; f++)
        {
            foreach (var (cx, cy) in _floors[f].Chests)
            {
                // G-09: cada baú sorteia uma variante determinística — mímico (oculto) ou amaldiçoado
                // (telegrafado), senão altar comum. Determinístico via _rng da run.
                var variant = _rng.Chance(GameConfig.ChestMimicChance) ? "mimic"
                    : _rng.Chance(GameConfig.ChestCursedChance) ? "cursed"
                    : "";
                _pois.Add(new Poi { Id = nextPoi++, Kind = "chest", Variant = variant, Floor = f, X = cx, Y = cy });
            }
            foreach (var (sx, sy) in _floors[f].Sanctuaries)
                _pois.Add(new Poi { Id = nextPoi++, Kind = "sanctuary", Floor = f, X = sx, Y = sy });
            if (_floors[f].LadderDown is { } ladder)
                _pois.Add(new Poi { Id = nextPoi++, Kind = "ladder", Floor = f, X = ladder.X, Y = ladder.Y });
        }
    }

    // ================= commands =================

    public void Enqueue(Command cmd)
    {
        lock (_commandLock) _commands.Enqueue(cmd);
    }

    private void DrainCommands(bool cardPauseAtStart)
    {
        var cardPause = cardPauseAtStart;
        while (true)
        {
            Command cmd;
            lock (_commandLock)
            {
                if (_commands.Count == 0) return;
                cmd = _commands.Dequeue();
            }

            // G-10: ToggleAutoHelper é uma mudança de config (não toca a simulação pausada) — deixa
            // passar mesmo durante a oferta de carta, senão ajustes do painel HELPER são engolidos.
            if (cardPause && cmd.Kind is not (CommandKind.SetMoveDir or CommandKind.ChooseCard or CommandKind.RerollCards or CommandKind.BanCard or CommandKind.Abandon or CommandKind.ToggleAutoHelper))
                continue;

            Apply(cmd);
            cardPause |= _pendingOffer is not null;
        }
    }

    private void Apply(Command cmd)
    {
        if (Ended is not null) return;
        switch (cmd.Kind)
        {
            case CommandKind.SetMoveDir:
                SetMoveDirection(cmd.A, cmd.B);
                break;
            case CommandKind.SetTarget:
                var target = _monsters.FirstOrDefault(m => m.Id == cmd.A && m.Hp > 0);
                Player.TargetId = target?.Id ?? 0;
                _manualTargetId = Player.TargetId;
                break;
            case CommandKind.CastSkill:
                TryCastSkill(cmd.A);
                break;
            case CommandKind.UsePotion:
                TryUsePotion();
                break;
            case CommandKind.ToggleStance:
                ToggleStance();
                break;
            case CommandKind.ToggleAutoHelper:
                _autoHelperTargeting = (cmd.A & 1) != 0;
                _autoHelperSkills = (cmd.A & 2) != 0;
                _autoHelperUltimate = (cmd.A & 4) != 0;
                _autoHelperAutoHeal = (cmd.A & GameConfig.AutoHelperAutoHealFlag) != 0;
                _autoHelperAutoCards = (cmd.A & GameConfig.AutoHelperAutoCardsFlag) != 0;
                _autoHelperMovementMode = NormalizeAutoHelperMovementMode(cmd.B);
                // S carrega "targetPreference|navMode|healPct".
                var parts = (cmd.S ?? "").Split('|');
                _autoHelperTargetPreference = NormalizeAutoHelperTargetPreference(parts.Length > 0 ? parts[0] : null);
                _autoHelperNavMode = GameConfig.NormalizeAutoHelperNav(parts.Length > 1 ? parts[1] : null);
                if (parts.Length > 2 && int.TryParse(parts[2], out var healPct))
                    _autoHelperHealPct = GameConfig.ClampHealPct(healPct);
                _helperMovementOverrideTargetId = 0;
                if (!_autoHelperTargeting && _manualTargetId == 0)
                    Player.TargetId = 0;
                break;
            case CommandKind.Interact:
                TryInteract(cmd.A, cmd.B);
                break;
            case CommandKind.ChooseCard:
                ChooseCard(cmd.S ?? "");
                break;
            case CommandKind.RerollCards:
                RerollCards();
                break;
            case CommandKind.BanCard:
                BanCard(cmd.S ?? "");
                break;
            case CommandKind.Abandon:
                EndRun(false, "abandono");
                break;
        }
    }

    // ================= tick =================

    public (SnapshotDto Snapshot, MapDto? Map) Tick()
    {
        TickCount++;
        _events.Clear();

        if (Ended is null)
        {
            var cardPauseAtStart = _pendingOffer is not null;
            if (!cardPauseAtStart)
                _simulationMs += GameConfig.TickMs;

            DrainCommands(cardPauseAtStart);

            // G-10: auto-pick — escolhe sozinho a carta de maior raridade (depois de um curto flash
            // na tela), pra autoplay/cavebot não travar nas ofertas. Senão, espera o timeout/jogador.
            if (Ended is null && _pendingOffer is not null && _autoHelperAutoCards
                && (TickCount - _cardOfferStartedTick) * GameConfig.TickMs >= GameConfig.AutoCardPickDelayMs)
                ChooseCard(BestOfferCardId());
            else if (Ended is null && _pendingOffer is not null
                && (TickCount - _cardOfferStartedTick) * GameConfig.TickMs >= GameConfig.CardOfferTimeoutMs)
                ChooseCard(_pendingOffer[0].Id);

            if (Ended is null && _pendingOffer is null)
            {
                if (cardPauseAtStart)
                    _simulationMs += GameConfig.TickMs;

                TickPlayerMovement();
                TickAutoHelper();
                TickPlayerCombat();

                if (_pendingOffer is null)
                {
                    TickPlayerRegen();
                    TickPlayerConditions();
                    TickMonsters();
                    // LM-03 (2b) povoamento contínuo: agendador de waves do modo (no-op no Dungeon).
                    _modeRules.OnTick(this);
                    TickPlayerSummons();
                    TickFields();
                    TickPendingStrikes();
                    TickPostureDecay();
                    TickTraitTimers();
                }
            }
        }

        MapDto? map = null;
        if (MapDirty)
        {
            map = BuildMap();
            MapDirty = false;
        }
        return (BuildSnapshot(), map);
    }

    // ---- movement ----

    private DungeonFloor Floor => _floors[_currentFloor];

    private Actor? OccupiedBy(int floor, int x, int y)
    {
        if (Player.Floor == floor && Player.X == x && Player.Y == y && Player.Hp > 0) return Player;
        return _monsters.FirstOrDefault(m => m.Floor == floor && m.X == x && m.Y == y && m.Hp > 0);
    }

    private void SetMoveDirection(int dx, int dy)
    {
        dx = Math.Clamp(dx, -1, 1);
        dy = Math.Clamp(dy, -1, 1);
        if (dx == _moveDirX && dy == _moveDirY) return;

        _moveDirX = dx;
        _moveDirY = dy;
        _moveDirChangedAtMs = NowMs;

        // When player manually moves while follow/avoid is active, pause helper movement until target dies
        if ((dx != 0 || dy != 0) && _helperMovementOverrideTargetId == 0
            && _autoHelperMovementMode != GameConfig.AutoHelperMovementModeNone)
        {
            var target = CurrentPlayerTarget();
            if (target is not null)
            {
                _savedAutoHelperMovementMode = _autoHelperMovementMode;
                _autoHelperMovementMode = GameConfig.AutoHelperMovementModeNone;
                _helperMovementOverrideTargetId = target.Id;
            }
        }

        var remaining = Player.StepStartMs + Player.StepDurMs - NowMs;
        if (Player.IsMoving(NowMs) && remaining <= GameConfig.StepGraceMs)
        {
            _bufferedMoveDirX = dx;
            _bufferedMoveDirY = dy;
            _hasBufferedMoveDir = true;
        }
        else
        {
            _hasBufferedMoveDir = false;
        }
    }

    private int StepDuration(int speed, bool diagonal)
    {
        var ms = 1000.0 * GameConfig.GroundFriction / Math.Max(speed, 30);
        if (diagonal) ms *= GameConfig.DiagonalStepFactor;
        return (int)Math.Clamp(ms, GameConfig.MinStepMs, GameConfig.MaxStepMs);
    }

    private static Dir FacingFrom(int dx, int dy, Dir? previous = null)
    {
        if (dx != 0 && dy != 0 && previous is Dir.North or Dir.South)
            return dy >= 0 ? Dir.South : Dir.North;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? Dir.East : Dir.West)
            : (dy >= 0 ? Dir.South : Dir.North);
    }

    private bool CanStep(Actor actor, int dx, int dy)
    {
        if (dx == 0 && dy == 0) return false;
        var nx = actor.X + dx;
        var ny = actor.Y + dy;
        var floor = _floors[actor.Floor];
        if (dx != 0 && dy != 0
            && (floor.IsBlocked(actor.X + dx, actor.Y)
                || floor.IsBlocked(actor.X, actor.Y + dy)))
            return false;
        return !floor.IsBlocked(nx, ny) && OccupiedBy(actor.Floor, nx, ny) is null;
    }

    private bool TryStep(Actor actor, int dx, int dy, int speed, long? stepStartMs = null)
    {
        if (!CanStep(actor, dx, dy)) return false;

        actor.FromX = actor.X;
        actor.FromY = actor.Y;
        actor.X += dx;
        actor.Y += dy;
        actor.StepStartMs = stepStartMs ?? NowMs;
        actor.StepDurMs = StepDuration(speed, dx != 0 && dy != 0);
        actor.Facing = FacingFrom(dx, dy, actor.Facing);
        return true;
    }

    private static void SettleStep(Actor actor)
    {
        actor.FromX = actor.X;
        actor.FromY = actor.Y;
        actor.StepDurMs = 0;
    }

    private int PlayerSpeed()
    {
        var speed = GameConfig.PlayerBaseSpeed
                    * (1 + EquipmentStats.MoveSpeedPercent + CardValue("moveSpeedPercent"));
        if (IsBuffActive("haste")) speed *= 1.30;
        if (NowMs < _traitHasteUntilMs) speed *= _traitHasteFactor; // Lunara — haste do Estilhaçar
        if (NowMs < _playerSlowUntilMs) speed *= _playerSlowFactor;
        return (int)speed;
    }

    private int MonsterSpeed(Actor monster)
    {
        var speed = monster.Species!.Speed * GameConfig.MonsterSpeedMultiplier;
        if (NowMs < monster.HasteUntilMs) speed = (int)(speed * monster.HasteFactor);
        if (NowMs < monster.SlowUntilMs) speed = (int)(speed * monster.SlowFactor);
        return speed;
    }

    private void TickPlayerMovement()
    {
        if (Player.Hp <= 0 || Player.IsMoving(NowMs)) return;
        if (Player.IsStunned(NowMs))
        {
            SettleStep(Player);
            return;
        }

        var dx = _hasBufferedMoveDir ? _bufferedMoveDirX : _moveDirX;
        var dy = _hasBufferedMoveDir ? _bufferedMoveDirY : _moveDirY;
        _hasBufferedMoveDir = false;
        if (dx == 0 && dy == 0)
        {
            SettleStep(Player);
            return;
        }

        var previousStepEndMs = Player.StepStartMs + Player.StepDurMs;
        var canChain = Player.StepDurMs > 0
                       && previousStepEndMs <= NowMs
                       && (_moveDirChangedAtMs <= previousStepEndMs
                           || NowMs - previousStepEndMs <= GameConfig.StepGraceMs);
        var stepStartMs = canChain ? previousStepEndMs : NowMs;
        var speed = PlayerSpeed();

        // try the held direction; if diagonal is blocked, slide along an axis
        if (!TryStep(Player, dx, dy, speed, stepStartMs))
        {
            if (dx != 0 && TryStep(Player, dx, 0, speed, stepStartMs)) return;
            if (dy != 0 && TryStep(Player, 0, dy, speed, stepStartMs)) return;
            SettleStep(Player);
        }
    }

    // ---- player stats from cards/buffs ----

    private double CardValue(string stat)
    {
        double total = 0;
        foreach (var (cardId, stacks) in _cards)
        {
            var def = Cards.ById[cardId];
            if (def.Stat == stat) total += def.Value * stacks;
        }
        return total;
    }

    private bool IsBuffActive(string buff) => _buffsUntilMs.TryGetValue(buff, out var until) && NowMs < until;

    private ClassStanceDef CurrentStance => PlayerClass.GetStance(_stanceId);

    private SkillDef[] CurrentSkillBar() => Classes.SkillBar(CurrentStance);

    private void ToggleStance()
    {
        if (!PlayerClass.CanToggleStance || Player.Hp <= 0) return;
        var next = PlayerClass.NextStance(_stanceId);
        _stanceId = next.Id;
        Emit("text", Player.X, Player.Y, 0, 0, 0, $"STANCE: {next.Name}");
        Emit("effect", Player.X, Player.Y, 0, 0, Classes.Skills[next.Slots[0]].EffectId);
    }

    // MG-02: tuning do papel da Kaeli (dano de auto vs skill, velocidade, range, AOE).
    private RoleTuning RoleTuning => _roles[Waifu.Role];
    // MG-02: a separação auto/skill é feita NOS CALL SITES, nunca dentro de PlayerAttack() (aplicaria
    // duas vezes). Auto-hit usa * RoleAutoMult(); skill-dmg e todos os procs de trait/echo/carta usam
    // * RoleSkillMult(). PlayerAttack() devolve o ataque "puro" (sem multiplicador de papel).
    private double RoleAutoMult() => RoleTuning.AutoDmgMult;
    private double RoleSkillMult() => RoleTuning.SkillDmgMult;

    // MG-04: tamanho de AOE escalado pelo papel (mage > knight > archer). Raio 0 (golpe de tile
    // único) é preservado; positivos arredondam pelo AoeScale com piso 1. Math.Round é determinístico.
    private int ScaledRadius(int raw) =>
        raw <= 0 ? 0 : Math.Max(1, (int)Math.Round(raw * RoleTuning.AoeScale, MidpointRounding.AwayFromZero));
    // Raio efetivo de uma skill no momento do cast. Ultimates capam em UltimateRadiusCap antes do
    // AoeScale e nunca caem abaixo de 2 — ainda "estouram" (mage 3, archer/knight ~2).
    private int SkillRadius(int raw, bool isUlt) =>
        isUlt ? Math.Max(2, ScaledRadius(Math.Min(raw, GameConfig.UltimateRadiusCap))) : ScaledRadius(raw);

    private double PlayerAttack()
    {
        var atk = (Waifu.BaseAtk + EquipmentStats.AttackBonus)
                  * (1 + GameConfig.AtkPerRunLevel * (_runLevel - 1))
                  * (1 + Ascension * GameConfig.AscensionAtkBonus)
                  * (1 + _affinityStatBonus)
                  * Loadout.Mastery.AtkMult
                  * (1 + CardValue("atkPercent"));
        if (_trait.Kind == "pack_hunter")
        {
            var packed = _monsters.Count(m => m.Hp > 0 && m.Floor == Player.Floor
                                              && Chebyshev(m, Player) <= 2);
            atk *= 1 + Math.Min(packed * _trait.Value, _trait.Param) * _traitMult;
        }
        if (IsBuffActive("atk")) atk *= 1.35;
        if (IsBuffActive("bloodrage")) atk *= GameConfig.BloodRageAttackMultiplier;
        if (IsBuffActive("aegis")) atk *= GameConfig.SentinelAegisAttackMultiplier;
        return atk * GameConfig.PlayerDamageMult;
    }

    private double CritChance()
    {
        var crit = GameConfig.CritChance + CardValue("critChance")
                   + Loadout.Mastery.CritChanceBonus + EquipmentStats.CritChance;
        if (IsBuffActive("crit")) crit += 0.20;
        return crit;
    }

    private long AutoAttackInterval()
    {
        // MG-02: a base de velocidade vem do papel (archer > knight > mage); divisores de carta/buff/Gaia
        // e o piso de 400ms continuam por cima.
        var interval = (double)RoleTuning.BaseAutoAttackMs / (1 + CardValue("atkSpeedPercent"));
        if (IsBuffActive("atkspeed")) interval /= 1.40;
        if (IsBuffActive("aegis")) interval /= GameConfig.SentinelAegisAttackSpeedMultiplier;
        if (NowMs < _preyHuntBonusUntilMs) interval /= 1 + GameConfig.GaiaHuntAtkSpeedBonus; // Gaia — caça
        return (long)Math.Max(interval, 400);
    }

    // ---- combat: player ----

    private void TickAutoHelper()
    {
        if (Player.Hp <= 0) return;

        if (_autoHelperTargeting)
        {
            var manualTarget = LockedManualTarget();
            if (manualTarget is not null)
            {
                Player.TargetId = manualTarget.Id;
            }
            else
            {
                var target = BestAutoHelperTarget(GameConfig.AutoHelperTargetRange);
                Player.TargetId = target?.Id ?? 0;
            }
        }

        if (Player.IsStunned(NowMs)) return;

        // G-10: auto-heal — usa a poção quando a vida cai abaixo do limiar (a poção respeita cargas/cooldown).
        if (_autoHelperAutoHeal && Player.Hp * 100 < Player.MaxHp * _autoHelperHealPct)
            TryUsePotion();

        // G-10: pathing — quando ligado, o helper caminha sozinho até o objetivo (baú/saída),
        // substituindo o movimento de combate (stand/follow/avoid). Combate (alvo/skills/ult) segue.
        if (_autoHelperNavMode == GameConfig.AutoHelperNavOff)
            TickAutoHelperMovement();
        else
            TickHelperNav();

        if (_autoHelperSkills)
            for (var slot = 0; slot < 4; slot++)
                TryAutoHelperSkill(slot);

        if (_autoHelperUltimate)
            TryAutoHelperSkill(4);
    }

    // ---- G-10: pathing do helper ("cavebot") + perfil persistido ----

    // Auto-loot: caminha até o coletável (baú OU altar/Santuário) ativo mais próximo, abre, repete;
    // sem mais nada, segue pra saída (escada/boss). Para pra lutar quando um inimigo encosta. Espera
    // AutoLootStartDelayMs no início do andar (a tela carrega antes de a Kaeli sair andando).
    // Determinístico (só estado do tick + ordem estável no NextNavStep).
    private void TickHelperNav()
    {
        if (NowMs - _floorEnteredMs < GameConfig.AutoLootStartDelayMs) return;
        if (Player.IsMoving(NowMs)) return;
        if (_moveDirX != 0 || _moveDirY != 0 || _hasBufferedMoveDir) return; // input manual tem prioridade

        var goal = NavGoal();
        if (goal is null) return;
        var (gx, gy, interactable, _) = goal.Value;

        // chegou: interage (abre baú/altar, desce escada) quando adjacente ao POI.
        if (interactable && Chebyshev(Player.X, Player.Y, gx, gy) <= 1)
        {
            TryInteract(gx, gy);
            return;
        }

        // pausa pra lutar quando um inimigo encosta (não atravessa o mob tomando dano).
        if (_monsters.Any(m => m.Hp > 0 && m.Floor == _currentFloor && Chebyshev(Player.X, Player.Y, m.X, m.Y) <= 1))
            return;

        var step = NextNavStep(gx, gy);
        if (step is { } s) TryStep(Player, s.Dx, s.Dy, PlayerSpeed());
    }

    // Objetivo de navegação: o coletável ativo mais próximo (baú comum/amaldiçoado OU altar de Eco —
    // os "roxos" do minimapa); sem coletáveis, a saída (escada do andar, ou o boss se não houver escada).
    private (int X, int Y, bool Interactable, string Kind)? NavGoal()
    {
        Poi? loot = null;
        var best = int.MaxValue;
        foreach (var p in _pois)
        {
            if (p.Used || p.Floor != _currentFloor) continue;
            if (p.Kind is not ("chest" or "sanctuary")) continue;
            var d = Chebyshev(Player.X, Player.Y, p.X, p.Y);
            if (d < best || (d == best && (loot is null || p.Id < loot.Id)))
            {
                loot = p;
                best = d;
            }
        }
        if (loot is not null) return (loot.X, loot.Y, true, loot.Kind);

        var ladder = _pois.FirstOrDefault(p => !p.Used && p.Floor == _currentFloor && p.Kind == "ladder");
        if (ladder is not null) return (ladder.X, ladder.Y, true, "ladder");

        // sem escada (andar do boss): a saída é derrotar o boss → caminha até ele.
        var boss = _monsters.FirstOrDefault(m => m.IsBossActor && m.Hp > 0 && m.Floor == _currentFloor);
        return boss is not null ? (boss.X, boss.Y, false, "boss") : null;
    }

    // G-10: alvo atual do auto-loot pra legibilidade no cliente (null quando o pathing está off).
    private NavTargetDto? CurrentNavTargetDto()
    {
        if (_autoHelperNavMode != GameConfig.AutoHelperNavLoot || Player.Hp <= 0) return null;
        return NavGoal() is { } g ? new NavTargetDto(g.X, g.Y, g.Kind) : null;
    }

    // Primeiro passo de um caminho mais curto (BFS) do jogador até ficar adjacente a (tx,ty).
    // BFS real contorna paredes/cantos — o passo guloso travava em becos. Monstros vivos bloqueiam
    // tiles (a Kaeli desvia; o combate limpa se o corredor estiver tampado). Determinístico: ordem
    // de vizinhos fixa, sem `_rng`/`DateTime`. Retorna null se não há caminho.
    private (int Dx, int Dy)? NextNavStep(int tx, int ty)
    {
        var floor = Floor;
        int w = floor.W, h = floor.H, sx = Player.X, sy = Player.Y;
        if (Chebyshev(sx, sy, tx, ty) <= 1) return null;

        var occupied = new HashSet<int>();
        foreach (var m in _monsters)
            if (m.Hp > 0 && m.Floor == _currentFloor) occupied.Add(m.Y * w + m.X);

        var firstStep = new (int Dx, int Dy)?[w * h];
        var seen = new bool[w * h];
        var q = new Queue<int>();
        seen[sy * w + sx] = true;
        q.Enqueue(sy * w + sx);

        // cardinais antes das diagonais → caminho estável e determinístico.
        ReadOnlySpan<(int dx, int dy)> dirs =
        [
            (0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, -1), (-1, 1), (1, 1)
        ];

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int cx = cur % w, cy = cur / w;
            foreach (var (dx, dy) in dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                var ni = ny * w + nx;
                if (seen[ni]) continue;

                var isTargetTile = nx == tx && ny == ty;
                if (!isTargetTile)
                {
                    if (floor.IsBlocked(nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (floor.IsBlocked(cx + dx, cy) || floor.IsBlocked(cx, cy + dy))) continue;
                    if (occupied.Contains(ni)) continue;
                }

                seen[ni] = true;
                var step = firstStep[cur] ?? (dx, dy);
                if (Chebyshev(nx, ny, tx, ty) <= 1) return step; // adjacente ao alvo → basta interagir/lutar
                firstStep[ni] = step;
                q.Enqueue(ni);
            }
        }
        return null;
    }

    // Perfil do helper persistido por Kaeli:
    // "targeting|skills|ult|pref|movement|autoheal|nav|healpct|autocards".
    public string EncodeHelperProfile() => string.Join('|',
        _autoHelperTargeting ? 1 : 0,
        _autoHelperSkills ? 1 : 0,
        _autoHelperUltimate ? 1 : 0,
        _autoHelperTargetPreference,
        _autoHelperMovementMode,
        _autoHelperAutoHeal ? 1 : 0,
        _autoHelperNavMode,
        _autoHelperHealPct,
        _autoHelperAutoCards ? 1 : 0);

    private void ApplyHelperProfile(string encoded)
    {
        var p = encoded.Split('|');
        if (p.Length < 7) return;
        _autoHelperTargeting = p[0] == "1";
        _autoHelperSkills = p[1] == "1";
        _autoHelperUltimate = p[2] == "1";
        _autoHelperTargetPreference = NormalizeAutoHelperTargetPreference(p[3]);
        _autoHelperMovementMode = NormalizeAutoHelperMovementModeName(p[4]);
        _autoHelperAutoHeal = p[5] == "1";
        _autoHelperNavMode = GameConfig.NormalizeAutoHelperNav(p[6]);
        if (p.Length > 7 && int.TryParse(p[7], out var healPct)) _autoHelperHealPct = GameConfig.ClampHealPct(healPct);
        if (p.Length > 8) _autoHelperAutoCards = p[8] == "1";
    }

    private static string NormalizeAutoHelperMovementModeName(string? mode) => mode switch
    {
        GameConfig.AutoHelperMovementModeFollow => GameConfig.AutoHelperMovementModeFollow,
        GameConfig.AutoHelperMovementModeAvoid => GameConfig.AutoHelperMovementModeAvoid,
        _ => GameConfig.AutoHelperMovementModeNone
    };

    private void TickAutoHelperMovement()
    {
        if (_helperMovementOverrideTargetId != 0)
        {
            var overrideDead = _monsters.All(m => m.Id != _helperMovementOverrideTargetId || m.Hp <= 0);
            var newTarget = Player.TargetId != 0 && Player.TargetId != _helperMovementOverrideTargetId;
            if (overrideDead || newTarget)
            {
                _autoHelperMovementMode = _savedAutoHelperMovementMode;
                _helperMovementOverrideTargetId = 0;
            }
        }

        if (_autoHelperMovementMode == GameConfig.AutoHelperMovementModeNone || Player.IsMoving(NowMs)) return;
        if (_moveDirX != 0 || _moveDirY != 0 || _hasBufferedMoveDir) return;

        var target = CurrentPlayerTarget();
        if (target is null) return;

        var speed = PlayerSpeed();

        if (_autoHelperMovementMode == GameConfig.AutoHelperMovementModeFollow)
        {
            var followDistance = Chebyshev(Player, target);
            if (followDistance > GameConfig.AutoHelperFollowDistance)
            {
                var stepped = TryAutoHelperCardinalStep(
                    target,
                    speed,
                    moveAway: false,
                    (_, nextDist) => nextDist >= GameConfig.AutoHelperFollowDistance && nextDist < followDistance);
                // O passo cardinal guloso não desvia de obstáculo: se os dois passos em direção
                // ao alvo estão bloqueados (pedregulho/parede entre a Kaeli e o inimigo), a perseguição
                // travava de vez e a run nunca terminava. Cai no mesmo pather BFS do auto-loot
                // (NextNavStep para na adjacência, AutoHelperFollowDistance=1) para contornar o bloqueio.
                // Determinístico: NextNavStep não usa _rng/DateTime.
                if (!stepped && NextNavStep(target.X, target.Y) is { } nav)
                    TryStep(Player, nav.Dx, nav.Dy, speed);
            }
            return;
        }

        if (_autoHelperMovementMode != GameConfig.AutoHelperMovementModeAvoid) return;

        var distance = Chebyshev(Player, target);
        if (distance < GameConfig.AutoHelperAvoidDistance)
        {
            TryAutoHelperCardinalStep(target, speed, moveAway: true, (_, nextDist) => nextDist > distance);
        }
        else if (distance > GameConfig.AutoHelperAvoidDistance)
        {
            TryAutoHelperCardinalStep(
                target,
                speed,
                moveAway: false,
                (_, nextDist) => nextDist >= GameConfig.AutoHelperAvoidDistance && nextDist <= distance);
        }
    }

    private bool TryAutoHelperCardinalStep(Actor target, int speed, bool moveAway, Func<int, int, bool> acceptDistance)
    {
        var currentDist = Chebyshev(Player, target);
        foreach (var (dx, dy) in CardinalDirectionsToTarget(target, moveAway))
        {
            var nextDist = Math.Max(Math.Abs(target.X - (Player.X + dx)), Math.Abs(target.Y - (Player.Y + dy)));
            if (!acceptDistance(currentDist, nextDist)) continue;
            if (TryStep(Player, dx, dy, speed)) return true;
        }
        return false;
    }

    private IEnumerable<(int dx, int dy)> CardinalDirectionsToTarget(Actor target, bool moveAway)
    {
        var deltaX = target.X - Player.X;
        var deltaY = target.Y - Player.Y;
        var dx = Math.Sign(deltaX);
        var dy = Math.Sign(deltaY);
        if (moveAway)
        {
            dx = -dx;
            dy = -dy;
        }

        var preferHorizontal = Math.Abs(deltaX) >= Math.Abs(deltaY);
        if (preferHorizontal)
        {
            if (dx != 0) yield return (dx, 0);
            if (dy != 0) yield return (0, dy);
        }
        else
        {
            if (dy != 0) yield return (0, dy);
            if (dx != 0) yield return (dx, 0);
        }
    }

    private Actor? LockedManualTarget()
    {
        if (_manualTargetId == 0) return null;
        var lockedId = _manualTargetId;
        var target = _monsters.FirstOrDefault(m => m.Id == lockedId);
        if (target is not null && IsTargetableByPlayer(target, GameConfig.AutoHelperTargetRange))
            return target;

        _manualTargetId = 0;
        if (Player.TargetId == lockedId) Player.TargetId = 0;
        return null;
    }

    private static string NormalizeAutoHelperTargetPreference(string? preference) =>
        preference == GameConfig.AutoHelperTargetPreferenceNearest
            ? GameConfig.AutoHelperTargetPreferenceNearest
            : GameConfig.AutoHelperTargetPreferenceLowestHp;

    private static string NormalizeAutoHelperMovementMode(int movementMode) =>
        movementMode switch
        {
            GameConfig.AutoHelperMovementModeFollowCode => GameConfig.AutoHelperMovementModeFollow,
            GameConfig.AutoHelperMovementModeAvoidCode => GameConfig.AutoHelperMovementModeAvoid,
            _ => GameConfig.AutoHelperMovementModeNone
        };

    private Actor? BestAutoHelperTarget(int maxRange, Func<Actor, bool>? extra = null)
    {
        // G-08B: escudeiro força o foco — se há um escudeiro alvo-válido, o helper o prioriza.
        var shielder = FindTargetableShielder(maxRange, extra);
        if (shielder is not null) return shielder;

        Actor? best = null;
        var bestHp = int.MaxValue;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (!IsTargetableByPlayer(m, maxRange)) continue;
            if (extra is not null && !extra(m)) continue;

            var dist = Chebyshev(Player, m);
            var better = _autoHelperTargetPreference == GameConfig.AutoHelperTargetPreferenceNearest
                ? dist < bestDist || (dist == bestDist && m.Hp < bestHp)
                                  || (dist == bestDist && m.Hp == bestHp && (best is null || m.Id < best.Id))
                : m.Hp < bestHp || (m.Hp == bestHp && dist < bestDist)
                                  || (m.Hp == bestHp && dist == bestDist && (best is null || m.Id < best.Id));
            if (better)
            {
                best = m;
                bestHp = m.Hp;
                bestDist = dist;
            }
        }
        return best;
    }

    /// <summary>G-08B: escudeiro alvo-válido mais próximo de ser escolhido (desempate estável por id).</summary>
    private Actor? FindTargetableShielder(int maxRange, Func<Actor, bool>? extra)
    {
        Actor? best = null;
        foreach (var m in _monsters)
        {
            if (!IsTargetableByPlayer(m, maxRange)) continue;
            if (extra is not null && !extra(m)) continue;
            if (GameConfig.BehaviorProfile(m.Species!.BehaviorId) is not { ShieldFraction: > 0 }) continue;
            if (best is null || m.Id < best.Id) best = m;
        }
        return best;
    }

    private void TryAutoHelperSkill(int slot)
    {
        if (slot < 4 && NowMs < _skillReadyAtMs[slot]) return;
        if (slot == 4 && _gauge < GameConfig.UltimateGaugeMax) return;

        var skill = CurrentSkillBar()[slot];
        if (!ShouldAutoHelperCast(skill, slot == 4, out var target)) return;

        if (_autoHelperTargeting && _manualTargetId == 0 && target is not null)
            Player.TargetId = target.Id;

        TryCastSkill(slot);
    }

    private bool ShouldAutoHelperCast(SkillDef skill, bool isUlt, out Actor? target)
    {
        target = null;

        if (skill.Shape == "buff")
            return ShouldAutoHelperCastBuff(skill);

        var currentTarget = _autoHelperTargeting ? LockedManualTarget() : CurrentPlayerTarget();
        if (currentTarget is not null)
        {
            target = currentTarget;
            return SkillWouldAffectMonster(skill, currentTarget, isUlt);
        }

        if (skill.Shape is "nova" or "ring" or "summon")
            return SkillWouldAffectMonster(skill, null, isUlt);

        if (!_autoHelperTargeting) return false;

        target = BestAutoHelperTarget(GameConfig.AutoHelperTargetRange, m => SkillWouldAffectMonster(skill, m, isUlt));
        return target is not null;
    }

    private bool ShouldAutoHelperCastBuff(SkillDef skill)
    {
        if (skill.Buff == "heal")
            return Player.Hp < Player.MaxHp * GameConfig.AutoHelperHealHpFraction;

        if (skill.Buff is not null && IsBuffActive(skill.Buff))
            return false;

        return BestAutoHelperTarget(GameConfig.AutoHelperTargetRange) is not null;
    }

    private Actor? CurrentPlayerTarget()
    {
        if (Player.TargetId == 0) return null;
        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId);
        return target is not null && IsTargetableByPlayer(target, GameConfig.AutoHelperTargetRange)
            ? target
            : null;
    }

    private bool SkillWouldAffectMonster(SkillDef skill, Actor? target, bool isUlt)
    {
        switch (skill.Shape)
        {
            case "single":
                return target is not null && CanSkillReachTarget(skill, target);

            case "area":
            case "barrage":
                if (target is null || !CanSkillReachTarget(skill, target)) return false;
                return AnyMonsterOnTiles(CircleTiles(target.X, target.Y, SkillRadius(skill.Radius, isUlt)));

            case "field":
                if (target is null || !CanSkillReachTarget(skill, target)) return false;
                return AnyMonsterOnTiles(CircleTiles(target.X, target.Y, Math.Max(skill.SummonRadius, 0)));

            case "nova":
                return AnyMonsterOnTiles(CircleTiles(Player.X, Player.Y, SkillRadius(skill.Radius, isUlt)),
                    skipPlayerTile: true, requiredMonsterId: target?.Id ?? 0);

            case "ring":
                return AnyMonsterOnTiles(
                    RingTiles(Player.X, Player.Y, skill.RingInner,
                        Math.Max(SkillRadius(skill.Radius, isUlt), skill.RingInner + 1)),
                    requiredMonsterId: target?.Id ?? 0);

            case "summon":
                return AnyMonsterOnTiles(CircleTiles(Player.X, Player.Y, Math.Max(skill.SummonRadius, 1)),
                    skipPlayerTile: true, requiredMonsterId: target?.Id ?? 0);

            case "beam":
                return BeamWouldHitMonster(skill, target);

            case "cone":
                return ConeWouldHitMonster(skill, target, isUlt);

            case "chain":
                return target is not null && CanSkillReachTarget(skill, target);

            default:
                return false;
        }
    }

    private bool CanSkillReachTarget(SkillDef skill, Actor target)
    {
        var range = skill.Range > 0 ? skill.Range : SkillFootprintRange(skill);
        return IsTargetableByPlayer(target, Math.Max(range, 1));
    }

    private static int SkillFootprintRange(SkillDef skill) => skill.Shape switch
    {
        "cone" => Math.Max(skill.Radius, 1),
        "nova" or "ring" => Math.Max(skill.Radius, 1),
        "summon" => Math.Max(skill.SummonRadius, 1),
        _ => GameConfig.AutoHelperTargetRange
    };

    private bool BeamWouldHitMonster(SkillDef skill, Actor? target)
    {
        var (dx, dy) = DirDelta(Player.Facing, target);
        for (var i = 1; i <= Math.Max(skill.Range, 1); i++)
        {
            var tx = Player.X + dx * i;
            var ty = Player.Y + dy * i;
            var px = Player.X + dx * (i - 1);
            var py = Player.Y + dy * (i - 1);
            if (DiagonalCornerBlocked(Floor, px, py, tx, ty)) break;
            if (Floor.IsBlocked(tx, ty)) break;
            if (MonsterAt(tx, ty) is not null) return true;
        }
        return false;
    }

    private bool ConeWouldHitMonster(SkillDef skill, Actor? target, bool isUlt)
    {
        var (dx, dy) = DirDelta(Player.Facing, target);
        foreach (var (tx, ty) in ConeTiles(Player.X, Player.Y, dx, dy, Math.Max(SkillRadius(skill.Radius, isUlt), 1)))
        {
            var victim = MonsterAt(tx, ty);
            if (victim is not null && IsTargetableByPlayer(victim, Math.Max(skill.Radius, 1)))
                return true;
        }
        return false;
    }

    private bool AnyMonsterOnTiles(IEnumerable<(int X, int Y)> tiles, bool skipPlayerTile = false, int requiredMonsterId = 0)
    {
        foreach (var (tx, ty) in tiles)
        {
            if (skipPlayerTile && tx == Player.X && ty == Player.Y) continue;
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            if (requiredMonsterId != 0 && victim.Id != requiredMonsterId) continue;
            return true;
        }
        return false;
    }

    // MG-02: alcance do auto vem do papel (archer > mage > knight), não mais da arma (cosmética).
    private bool CanPlayerAutoAttack(Actor target) =>
        IsTargetableByPlayer(target, RoleTuning.AutoRange);

    private bool IsTargetableByPlayer(Actor target, int maxRange) =>
        target.Hp > 0
        && target.Floor == Player.Floor
        && Chebyshev(Player, target) <= maxRange
        && HasLineOfSight(Player.X, Player.Y, target.X, target.Y);

    private void TickPlayerCombat()
    {
        if (Player.Hp <= 0) return;
        if (Player.TargetId == 0 || NowMs < _autoAttackReadyAtMs) return;
        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId && m.Hp > 0 && m.Floor == Player.Floor);
        if (target is null)
        {
            Player.TargetId = 0;
            return;
        }

        if (!CanPlayerAutoAttack(target)) return;

        _autoAttackReadyAtMs = NowMs + AutoAttackInterval();
        Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        var missile = Waifus.WeaponMissile(Waifu.Weapon, Waifu.Element);
        if (missile > 0)
            Emit("projectile", Player.X, Player.Y, target.X, target.Y, missile);

        var attackElement = string.IsNullOrWhiteSpace(EquipmentStats.WeaponElement)
            ? Waifu.Element
            : EquipmentStats.WeaponElement;
        DealDamageToMonster(target, PlayerAttack() * RoleAutoMult(), attackElement, hitEffect: missile > 0 ? 0 : 216);
    }

    private void TryCastSkill(int slot)
    {
        if (slot is < 0 or > 4 || Player.Hp <= 0 || Player.IsStunned(NowMs)) return;
        var skill = CurrentSkillBar()[slot];
        var isUlt = slot == 4;

        if (isUlt)
        {
            if (_gauge < GameConfig.UltimateGaugeMax) return;
        }
        else if (NowMs < _skillReadyAtMs[slot]) return;

        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId && m.Hp > 0 && m.Floor == Player.Floor)
                     ?? NearestMonster(skill.Range > 0 ? skill.Range : GameConfig.AutoHelperTargetRange);

        // skills que precisam de alvo só exigem QUE EXISTA um alvo travado/próximo — não que ele
        // esteja em alcance. Se estiver longe, a magia dispara na reta da Kaeli até o limite/parede.
        if (skill.Shape is "single" or "area" or "chain" or "barrage" && target is null) return;
        if (skill.Shape == "chain" && !CanSkillReachTarget(skill, target!)) return;

        // ponto de mira: o alvo travado, mas limitado ao alcance da skill e parando antes da parede
        var aimX = target?.X ?? Player.X;
        var aimY = target?.Y ?? Player.Y;
        if (skill.Shape is "single" or "area" or "barrage" or "field" && target is not null)
        {
            (aimX, aimY) = AimAlongLine(target.X, target.Y, skill.Range);
            if (aimX == Player.X && aimY == Player.Y) return; // parede colada: nem sai (não gasta CD)
        }

        if (isUlt)
        {
            _gauge = 0;
            // Eco Núcleo de Trovão: usar a ultimate devolve a Carga cheia.
            if (HasEcho("thunder_core")) _staticCharge = GameConfig.RynnaChargeMax;
        }
        else _skillReadyAtMs[slot] = NowMs + (long)(
            skill.CooldownMs
            * Loadout.Mastery.CooldownMult
            * (1 - EquipmentStats.CooldownReduction));

        Emit("skill_cast", Player.X, Player.Y, 0, 0, 0, skill.Name);
        if (target is not null)
            Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        // maestria: slots 1-4 multiplicam o Power; a ultimate amplifica duração/cura (ultmod)
        var ultScale = isUlt ? Loadout.Mastery.UltimatePowerMult : 1.0;
        var damage = PlayerAttack() * RoleSkillMult() * skill.Power * EquipmentStats.SkillPowerMultiplier
                     * (isUlt ? 1.0 : Loadout.Mastery.SlotPowerMult(slot));
        switch (skill.Shape)
        {
            case "buff":
                if (skill.Buff == "heal")
                {
                    HealPlayer((int)Math.Ceiling(Player.MaxHp * GameConfig.NaturesEmbraceHealFraction * ultScale));
                }
                else if (skill.Buff is not null)
                {
                    _buffsUntilMs[skill.Buff] = NowMs + (long)(skill.BuffMs * ultScale);
                }
                Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                break;

            case "single":
            {
                if (skill.MissileId > 0) Emit("projectile", Player.X, Player.Y, aimX, aimY, skill.MissileId);
                Emit("effect", aimX, aimY, 0, 0, skill.EffectId);
                // acerta o alvo travado se a reta chegou nele; senão o que estiver onde a magia parou
                var victim = aimX == target!.X && aimY == target.Y ? target : MonsterAt(aimX, aimY);
                if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                break;
            }

            case "area":
            {
                if (skill.MissileId > 0) Emit("projectile", Player.X, Player.Y, aimX, aimY, skill.MissileId);
                foreach (var (tx, ty) in CircleTiles(aimX, aimY, SkillRadius(skill.Radius, isUlt)))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "nova":
            {
                foreach (var (tx, ty) in CircleTiles(Player.X, Player.Y, SkillRadius(skill.Radius, isUlt)))
                {
                    if (tx == Player.X && ty == Player.Y) continue;
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "beam":
            {
                var (dx, dy) = DirDelta(Player.Facing, target);
                for (var i = 1; i <= skill.Range; i++)
                {
                    var tx = Player.X + dx * i;
                    var ty = Player.Y + dy * i;
                    var px = Player.X + dx * (i - 1);
                    var py = Player.Y + dy * (i - 1);
                    if (DiagonalCornerBlocked(Floor, px, py, tx, ty)) break;
                    if (Floor.IsBlocked(tx, ty)) break;
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "cone":
            {
                var (dx, dy) = DirDelta(Player.Facing, target);
                var reach = Math.Max(SkillRadius(skill.Radius, isUlt), 1);
                foreach (var (tx, ty) in ConeTiles(Player.X, Player.Y, dx, dy, reach))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "summon":
            {
                // construct dropped on the caster's tile; pulses area damage for SummonMs.
                var pulseMs = Math.Max(skill.SummonPulseMs, GameConfig.TickMs);
                var slotMult = isUlt ? ultScale : Loadout.Mastery.SlotPowerMult(slot);
                _summons.Add(new PlayerSummon
                {
                    Floor = _currentFloor, X = Player.X, Y = Player.Y,
                    Element = skill.Element, Fx = skill.EffectId,
                    Radius = Math.Max(skill.SummonRadius, 1),
                    DamagePerPulse = PlayerAttack() * RoleSkillMult() * skill.SummonPower
                        * EquipmentStats.SkillPowerMultiplier * slotMult,
                    PulseMs = pulseMs,
                    NextPulseAtMs = NowMs + pulseMs,
                    ExpireAtMs = NowMs + Math.Max(skill.SummonMs, pulseMs),
                });
                Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                break;
            }

            case "chain":
            {
                if (target is null) break;
                var hit = new HashSet<int> { target.Id };
                var current = target;
                var hops = Math.Max(skill.ChainJumps, 1);
                var falloff = Math.Clamp(skill.ChainFalloff, 0, 0.9);
                var jumpRange = Math.Max(skill.ChainRange, 1);
                int fromX = Player.X, fromY = Player.Y;
                var hopDamage = damage;
                for (var h = 0; h < hops && current is not null; h++)
                {
                    if (skill.MissileId > 0) Emit("projectile", fromX, fromY, current.X, current.Y, skill.MissileId);
                    Emit("effect", current.X, current.Y, 0, 0, skill.EffectId);
                    HitMonster(current, hopDamage, skill, ultScale);
                    fromX = current.X; fromY = current.Y;
                    hopDamage *= 1 - falloff;
                    current = NearestUnhitMonster(fromX, fromY, jumpRange, hit);
                    if (current is not null) hit.Add(current.Id);
                }
                break;
            }

            case "ring":
            {
                // raio externo escalado, mas nunca colapsa sobre o buraco central (mantém anel válido).
                var outer = Math.Max(SkillRadius(skill.Radius, isUlt), skill.RingInner + 1);
                foreach (var (tx, ty) in RingTiles(Player.X, Player.Y, skill.RingInner, outer))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "field":
            {
                // pinta tiles-perigo no chao (ao redor do alvo, ou de si mesma se sem alvo).
                var dmg = PlayerAttack() * RoleSkillMult() * skill.SummonPower * EquipmentStats.SkillPowerMultiplier
                    * (isUlt ? ultScale : Loadout.Mastery.SlotPowerMult(slot));
                var tickMs = Math.Max(skill.SummonPulseMs, GameConfig.TickMs);
                foreach (var (tx, ty) in CircleTiles(aimX, aimY, Math.Max(skill.SummonRadius, 0)))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    _fields.Add(new GroundField
                    {
                        Floor = _currentFloor, X = tx, Y = ty, Element = skill.Element, Fx = skill.EffectId,
                        DamagePerTick = dmg, SlowFactor = skill.SlowFactor, SlowMs = skill.SlowMs,
                        TickMs = tickMs, NextTickAtMs = NowMs + tickMs,
                        ExpireAtMs = NowMs + Math.Max(skill.SummonMs, tickMs),
                    });
                }
                break;
            }

            case "barrage":
            {
                // golpes em multiplos tempos no ponto alvo (chuva/rajada que cai em sequencia).
                var strikes = Math.Max(skill.Strikes, 1);
                var interval = Math.Max(skill.StrikeIntervalMs, GameConfig.TickMs);
                var dotPerTick = PlayerAttack() * RoleSkillMult() * skill.DotPower * EquipmentStats.SkillPowerMultiplier;
                for (var k = 0; k < strikes; k++)
                    _pendingStrikes.Add(new ScheduledStrike
                    {
                        Floor = _currentFloor, X = aimX, Y = aimY,
                        AtMs = NowMs + skill.StrikeDelayMs + (long)k * interval,
                        Element = skill.Element, Fx = skill.EffectId, Damage = damage,
                        Radius = SkillRadius(skill.Radius, isUlt), RingInner = skill.RingInner,
                        StunMs = skill.StunMs, SlowFactor = skill.SlowFactor, SlowMs = skill.SlowMs,
                        DotTicks = skill.DotTicks, DotTickMs = skill.DotTickMs, DotPower = dotPerTick,
                    });
                Emit("effect", aimX, aimY, 0, 0, skill.EffectId); // telegrafo inicial
                break;
            }
        }
    }

    private void HitMonster(Actor monster, double damage, SkillDef skill, double buffScale = 1.0)
    {
        if (skill.Power > 0)
            DealDamageToMonster(monster, damage, skill.Element, 0, fromSkill: true);
        if (monster.Hp <= 0) return;

        switch (skill.Buff)
        {
            case "taunt":
                AcquirePlayer(monster);
                break;
            case "exposed":
                monster.ExposedUntilMs = NowMs + (long)(skill.BuffMs * buffScale);
                break;
            case "sapped":
                monster.SappedUntilMs = NowMs + (long)(skill.BuffMs * buffScale);
                break;
        }

        if (monster.Hp > 0 && skill.StunMs > 0)
        {
            monster.StunUntilMs = NowMs + skill.StunMs;
            Emit("effect", monster.X, monster.Y, 0, 0, 32); // stun stars
        }

        if (monster.Hp > 0 && skill.DotTicks > 0 && skill.DotPower > 0)
            ApplyDotToMonster(monster, skill.Element, skill.EffectId,
                PlayerAttack() * RoleSkillMult() * skill.DotPower * EquipmentStats.SkillPowerMultiplier * buffScale,
                skill.DotTicks, skill.DotTickMs > 0 ? skill.DotTickMs : GameConfig.ConditionDefaultTickMs);

        if (monster.Hp > 0 && skill.SlowMs > 0 && skill.SlowFactor < 1)
            ApplyMonsterSlow(monster, skill.SlowFactor, skill.SlowMs);
    }

    /// <summary>Slows a monster's movement (reuses the chiller/reaction slow fields).</summary>
    private void ApplyMonsterSlow(Actor monster, double factor, int ms)
    {
        monster.SlowUntilMs = NowMs + ms;
        monster.SlowFactor = Math.Clamp(factor, GameConfig.SlowFactorFloor, 1.0);
    }

    /// <summary>Adds (or refreshes) a damage-over-time on a monster. Same element never stacks:
    /// reapplying keeps the stronger per-tick and the longer duration (no infinite ramp).</summary>
    private void ApplyDotToMonster(Actor monster, string element, int fx, double dmgPerTick, int ticks, int tickMs)
    {
        var perTick = Math.Max(dmgPerTick, 1);
        tickMs = Math.Max(tickMs, GameConfig.TickMs);
        foreach (var existing in monster.Dots)
        {
            if (existing.Element != element) continue;
            existing.DamagePerTick = Math.Max(existing.DamagePerTick, perTick);
            existing.TicksLeft = Math.Max(existing.TicksLeft, ticks);
            existing.TickMs = tickMs;
            existing.NextTickAtMs = Math.Min(existing.NextTickAtMs, NowMs + tickMs);
            return;
        }
        monster.Dots.Add(new MonsterDot
        {
            Element = element, Fx = fx, DamagePerTick = perTick,
            TicksLeft = ticks, TickMs = tickMs, NextTickAtMs = NowMs + tickMs,
        });
    }

    /// <summary>Ticks the DoTs on one monster. Reuses DealDamageToMonster so death/xp/posture/
    /// reactions all behave identically to a normal hit (no crit, no lifesteal for ticks).</summary>
    private void TickMonsterDots(Actor monster)
    {
        for (var i = monster.Dots.Count - 1; i >= 0; i--)
        {
            var dot = monster.Dots[i];
            if (NowMs < dot.NextTickAtMs) continue;
            dot.NextTickAtMs = NowMs + dot.TickMs;
            dot.TicksLeft--;
            DealDamageToMonster(monster, dot.DamagePerTick, dot.Element, dot.Fx,
                fromSkill: false, canCrit: false, canLifeSteal: false);
            if (monster.Hp <= 0) { monster.Dots.Clear(); return; }
            if (dot.TicksLeft <= 0)
            {
                monster.Dots.RemoveAt(i);
                OnConditionExpiredCard(monster, dot); // G-04 Detonação: expira → estouro em área
                if (monster.Hp <= 0) return;
            }
        }
    }

    /// <summary>Ticks player-summoned constructs: each pulses area damage on its tile, then expires.</summary>
    private void TickPlayerSummons()
    {
        if (_summons.Count == 0) return;
        for (var i = _summons.Count - 1; i >= 0; i--)
        {
            var summon = _summons[i];
            if (NowMs >= summon.ExpireAtMs) { _summons.RemoveAt(i); continue; }
            if (summon.Floor != _currentFloor || NowMs < summon.NextPulseAtMs) continue;
            summon.NextPulseAtMs = NowMs + summon.PulseMs;
            Emit("effect", summon.X, summon.Y, 0, 0, summon.Fx);
            foreach (var (tx, ty) in CircleTiles(summon.X, summon.Y, summon.Radius))
            {
                var victim = MonsterAt(tx, ty);
                if (victim is null) continue;
                if (tx != summon.X || ty != summon.Y) Emit("effect", tx, ty, 0, 0, summon.Fx);
                DealDamageToMonster(victim, summon.DamagePerPulse, summon.Element, 0,
                    fromSkill: false, canCrit: false, canLifeSteal: false);
            }
        }
    }

    /// <summary>Nearest living monster on the current floor within range, excluding ids in <paramref name="exclude"/>. For chain jumps.</summary>
    private Actor? NearestUnhitMonster(int x, int y, int range, HashSet<int> exclude)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || exclude.Contains(m.Id)) continue;
            var d = Chebyshev(x, y, m.X, m.Y);
            if (d <= range && d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    /// <summary>Hollow diamond: tiles whose Manhattan distance is in (inner, outer]. inner 0 = solid (= CircleTiles).</summary>
    private IEnumerable<(int X, int Y)> RingTiles(int cx, int cy, int inner, int outer)
    {
        for (var dy = -outer; dy <= outer; dy++)
            for (var dx = -outer; dx <= outer; dx++)
            {
                var dist = Math.Abs(dx) + Math.Abs(dy);
                if (dist > outer * GameConfig.AoeRoundingFactor || dist <= inner) continue;
                var x = cx + dx;
                var y = cy + dy;
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
    }

    /// <summary>Resolves one scheduled multi-time strike on its footprint (circle or hollow ring).</summary>
    private void ResolveStrike(ScheduledStrike s)
    {
        if (s.Floor != _currentFloor) return;
        var tiles = s.RingInner > 0 ? RingTiles(s.X, s.Y, s.RingInner, s.Radius) : CircleTiles(s.X, s.Y, s.Radius);
        foreach (var (tx, ty) in tiles)
        {
            Emit("effect", tx, ty, 0, 0, s.Fx);
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            DealDamageToMonster(victim, s.Damage, s.Element, 0, fromSkill: true, canCrit: false, canLifeSteal: false);
            if (victim.Hp <= 0) continue;
            if (s.StunMs > 0) victim.StunUntilMs = NowMs + s.StunMs;
            if (s.SlowMs > 0 && s.SlowFactor < 1) ApplyMonsterSlow(victim, s.SlowFactor, s.SlowMs);
            if (s.DotTicks > 0 && s.DotPower > 0)
                ApplyDotToMonster(victim, s.Element, s.Fx, s.DotPower, s.DotTicks,
                    s.DotTickMs > 0 ? s.DotTickMs : GameConfig.ConditionDefaultTickMs);
        }
    }

    /// <summary>Ticks player-painted ground fields: each damages/slows the monster on its tile, then expires.</summary>
    private void TickFields()
    {
        if (_fields.Count == 0) return;
        for (var i = _fields.Count - 1; i >= 0; i--)
        {
            var field = _fields[i];
            if (NowMs >= field.ExpireAtMs) { _fields.RemoveAt(i); continue; }
            if (field.Floor != _currentFloor || NowMs < field.NextTickAtMs) continue;
            field.NextTickAtMs = NowMs + field.TickMs;
            Emit("effect", field.X, field.Y, 0, 0, field.Fx);
            var victim = MonsterAt(field.X, field.Y);
            if (victim is null) continue;
            if (field.DamagePerTick > 0)
                DealDamageToMonster(victim, field.DamagePerTick, field.Element, 0,
                    fromSkill: false, canCrit: false, canLifeSteal: false);
            if (victim.Hp > 0 && field.SlowMs > 0 && field.SlowFactor < 1)
                ApplyMonsterSlow(victim, field.SlowFactor, field.SlowMs);
        }
    }

    /// <summary>Resolves multi-time strikes whose scheduled moment has arrived.</summary>
    private void TickPendingStrikes()
    {
        if (_pendingStrikes.Count == 0) return;
        for (var i = _pendingStrikes.Count - 1; i >= 0; i--)
        {
            var strike = _pendingStrikes[i];
            if (NowMs < strike.AtMs) continue;
            _pendingStrikes.RemoveAt(i);
            ResolveStrike(strike);
        }
    }

    private void DealDamageToMonster(Actor monster, double raw, string element, int hitEffect,
        bool fromSkill = false, bool canCrit = true, bool canLifeSteal = true, bool fromTrait = false)
    {
        // "acerto direto" = auto-attack ou hit de skill (não DoT/campo/invocação/burst de trait).
        // É o que move o estado das passivas (combo, carga, marca, presa, estilhaço).
        var directHit = (fromSkill || canCrit) && !fromTrait;

        var roll = raw * (GameConfig.DamageRollMin + _rng.NextDouble() * (GameConfig.DamageRollMax - GameConfig.DamageRollMin));
        if (NowMs < monster.ExposedUntilMs)
            roll *= GameConfig.ExposedWeaknessDamageMultiplier;

        // trait: deadeye soma crit por distância; executioner/slayer multiplicam o roll
        var critChance = CritChance();
        if (_trait.Kind == "deadeye" && Chebyshev(Player, monster) >= (int)_trait.Param)
            critChance += _trait.Value * _traitMult;

        // K-04: pré-dano das passivas assinatura (ramp/execução/bônus + crit garantido)
        var forceCrit = false;
        if (!fromTrait)
            ApplyTraitPreDamage(monster, element, directHit, ref roll, ref forceCrit);

        var crit = canCrit && (forceCrit || _rng.Chance(critChance));
        if (crit) roll *= GameConfig.CritMultiplier + EquipmentStats.CritDamage;

        if (_trait.Kind == "executioner" && monster.Hp < monster.MaxHp * _trait.Param)
            roll *= 1 + _trait.Value * _traitMult;
        if (_trait.Kind == "slayer" && monster.Species!.BestiaryClass == _trait.Tag)
            roll *= 1 + _trait.Value * _traitMult;

        // element bonus card
        if (element == Waifu.Element) roll *= 1 + CardValue("elementPercent");
        if (element == CurrentStance.Element)
        {
            if (element == EquipmentStats.WeaponElement)
                roll *= 1 + GameConfig.EquipmentWeaponElementMatchDamageBonus;
            if (element == EquipmentStats.Element && EquipmentStats.ElementDamageBonus > 0)
                roll *= 1 + EquipmentStats.ElementDamageBonus;
        }

        // bestiary mastery bonus
        var rank = BestiaryRank(monster.Species!.StableId);
        roll *= 1 + rank * GameConfig.BestiaryDamageBonusPerRank;

        // tibia armor + elemental resistance
        var afterArmor = roll - _rng.Range(0, Math.Max(monster.Species.Armor / 2, 0)) / Math.Max(monster.StatMult, 1);
        var resist = monster.Species.Elements.GetValueOrDefault(element, 0);
        afterArmor *= (100 - resist) / 100.0;

        // F-E: echo break amplifies every hit landed during the stagger window
        var staggered = monster.IsStaggered(NowMs);
        if (staggered)
        {
            afterArmor *= monster.StaggerMultiplier;
            if (NowMs >= monster.PostureBonusReadyAtMs)
            {
                afterArmor += monster.MaxHp * GameConfig.PostureMaxHpBonusPct;
                monster.PostureBonusReadyAtMs = NowMs + GameConfig.PostureMaxHpBonusCooldownMs;
            }
        }

        var final = Math.Max((int)afterArmor, resist >= 100 ? 0 : 1);
        if (final <= 0)
        {
            Emit("text", monster.X, monster.Y, 0, 0, 0, "IMUNE");
            return;
        }

        // G-08B: barreira do escudeiro absorve antes da vida (some o golpe se cobrir tudo).
        if (monster.MonsterShield > 0)
        {
            var absorbed = Math.Min(monster.MonsterShield, final);
            monster.MonsterShield -= absorbed;
            final -= (int)absorbed;
            if (final <= 0)
            {
                Emit("text", monster.X, monster.Y, 0, 0, 0, "BLOQUEADO");
                return;
            }
        }

        monster.Hp -= final;
        Emit("damage", monster.X, monster.Y, 0, 0, final, "", monster.Id, crit);
        if (hitEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, hitEffect);
        if (crit) Emit("effect", monster.X, monster.Y, 0, 0, 173);

        var lifesteal = canLifeSteal ? CardValue("lifesteal") + GameConfig.BaselineLifesteal : 0;
        if (canLifeSteal && EquipmentStats.LifeStealAmount > 0
            && _rng.Chance(EquipmentStats.LifeStealChance))
            lifesteal += EquipmentStats.LifeStealAmount;
        if (lifesteal > 0) HealPlayer((int)Math.Max(final * lifesteal, 0));

        // traits de reserva pós-dano: seiva vital (lifesteal de skill) e mordida do norte (slow de gelo)
        if (fromSkill && _trait.Kind == "skill_lifesteal")
            HealPlayer((int)Math.Max(final * _trait.Value * _traitMult, 0));
        if (_trait.Kind == "chiller" && element == "ice" && monster.Hp > 0)
        {
            monster.SlowUntilMs = NowMs + (long)_trait.Param;
            monster.SlowFactor = Math.Max(1 - _trait.Value * _traitMult, GameConfig.SlowFactorFloor);
        }

        // K-04: pós-dano das passivas assinatura (marcas, stacks, carga, estilhaço, contágio)
        if (!fromTrait)
            ApplyTraitPostDamage(monster, final, element, directHit);
        // G-04: pós-dano das cartas de mecânica (enche ult, golpe extra) — mesmo seam, sem dispatch novo.
        if (!fromTrait)
            ApplyCardPostDamage(monster, directHit);

        // F-E: posture build (boss only) and elemental reactions (any target)
        if (monster.Hp > 0 && monster.PostureMax > 0 && !staggered)
            AddPosture(monster, element, fromSkill);
        if (monster.Hp > 0)
            ApplyElementMarkAndReactions(monster, element, final);

        // aggro: damaged monsters retaliate
        if (monster.TargetId == 0) AcquirePlayer(monster);

        if (monster.Hp <= 0) KillMonster(monster);
    }

    // ================= G-08B: keyword interaction (mob × tags de G-04) =================
    // Um mob pode resistir (0-100%) ou amplificar (negativo) uma keyword de carta. Multiplica a
    // magnitude do efeito daquela tag aplicado AO mob. Determinístico: o multiplicador é puro; o
    // arredondamento de stacks inteiros usa _rng SÓ quando há resistência configurada (não perturba
    // o stream de RNG dos mobs sem keyword resist, preservando determinismo das runs existentes).

    /// <summary>Fração da keyword que afeta o mob: 1 = normal, 0 = imune (100), >1 = amplifica (negativo).</summary>
    private static double KeywordResistMult(Actor m, string tag)
    {
        var dict = m.Species?.KeywordResistances;
        if (dict is null || dict.Count == 0 || !dict.TryGetValue(tag, out var pct)) return 1.0;
        return Math.Max(1 - pct / 100.0, 0);
    }

    private static bool HasKeywordResist(Actor m, string tag) =>
        m.Species?.KeywordResistances is { Count: > 0 } d && d.ContainsKey(tag);

    /// <summary>Escala um ganho de stacks inteiro pela resistência de keyword. Sem entrada → devolve o
    /// valor cru (não toca _rng). Com entrada → parte inteira + 1 extra probabilístico pela fração.</summary>
    private int KeywordScaledStacks(Actor m, string tag, int baseStacks)
    {
        if (baseStacks <= 0 || !HasKeywordResist(m, tag)) return baseStacks;
        var scaled = baseStacks * KeywordResistMult(m, tag);
        var whole = (int)Math.Floor(scaled);
        var frac = scaled - whole;
        if (frac > 0 && _rng.NextDouble() < frac) whole++;
        return whole;
    }

    // ================= K-04: passivas assinatura =================
    // Uma família mecânica por Kaeli. Determinístico: só NowMs/_rng da run, contadores estáveis e
    // seleção de alvo por menor distância com desempate por menor id. _traitMult (maestria) sempre
    // amplifica o efeito principal. Bursts internos chamam DealDamageToMonster com fromTrait:true
    // para não re-disparar a própria passiva (a guarda Killed evita dupla contagem de morte).

    /// <summary>Pré-dano: ramp/execução/bônus que multiplicam o roll, e crit garantido (Seren).</summary>
    private void ApplyTraitPreDamage(Actor monster, string element, bool directHit, ref double roll, ref bool forceCrit)
    {
        switch (_trait.Kind)
        {
            case "discipline": // Seren — combo no mesmo alvo, 3º acerto = Corte Perfeito
            {
                if (!directHit) break;
                if (_comboTargetId != monster.Id || NowMs > _comboExpireMs)
                {
                    _comboTargetId = monster.Id;
                    _comboHits = 0;
                }
                _comboHits++;
                // Eco Cadência Sem Fim: reset mais severo, ramp sem teto.
                _comboExpireMs = NowMs + (HasEcho("endless_cadence")
                    ? GameConfig.EchoEndlessCadenceResetMs : GameConfig.SerenDisciplineResetMs);
                var ramp = HasEcho("endless_cadence")
                    ? _comboHits * _trait.Value
                    : Math.Min(_comboHits * _trait.Value, _trait.Param);
                // G-08B: keyword "combo" — resiste/amplifica o ramp de Disciplina contra este alvo.
                roll *= 1 + ramp * _traitMult * KeywordResistMult(monster, "combo");
                // Eco Execução Perfeita: Corte a cada 2º e o crit garantido executa alvos fracos.
                var cutEvery = HasEcho("perfect_execution")
                    ? GameConfig.EchoPerfectCutEvery : GameConfig.SerenPerfectCutEvery;
                if (_comboHits % cutEvery == 0)
                {
                    forceCrit = true;
                    if (HasEcho("perfect_execution")
                        && monster.Hp < monster.MaxHp * GameConfig.EchoPerfectExecuteHpFraction)
                    {
                        Emit("text", monster.X, monster.Y, 0, 0, 0, "EXECUÇÃO");
                        roll = Math.Max(roll, monster.MaxHp * 10); // estouro letal através da armadura
                    }
                }
                break;
            }

            case "decay": // Velvet — limiar de execução sobe com os stacks de Decadência
            {
                var threshold = Math.Min(
                    _trait.Param + ActiveDecayStacks(monster) * GameConfig.VelvetThresholdPerStack,
                    GameConfig.VelvetThresholdCap);
                if (monster.Hp < monster.MaxHp * threshold)
                    roll *= 1 + _trait.Value * _traitMult;
                break;
            }

            case "shatter": // Lunara — dano bônus contra alvo já lento
                if (element == "ice" && NowMs < monster.SlowUntilMs)
                    roll *= 1 + _trait.Value * _traitMult;
                break;

            case "contagion": // Rin — Eco Pira: o dano cresce com o nº de inimigos queimando
                if (HasEcho("pyre"))
                {
                    var burning = _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor && IsBurning(m));
                    roll *= 1 + Math.Min(burning * GameConfig.EchoPyreDamagePerBurning, GameConfig.EchoPyreMaxBonus);
                }
                break;

            case "prey": // Gaia — ramp por tempo de caça contra a Presa
            {
                if (!directHit) break;
                if (_preyId == 0 || !IsMonsterAlive(_preyId)) SetPrey(monster);
                // Eco Matilha: marca uma segunda Presa quando há mais alvos.
                if (HasEcho("pack") && (_preyId2 == 0 || !IsMonsterAlive(_preyId2)))
                    SetSecondPrey(monster);
                if (monster.Id == _preyId || monster.Id == _preyId2)
                {
                    // Eco Caça Eterna: ramp e teto dobrados.
                    var rampPerSec = _trait.Value * (HasEcho("eternal_hunt") ? GameConfig.EchoEternalHuntRampMult : 1);
                    var cap = _trait.Param * (HasEcho("eternal_hunt") ? GameConfig.EchoEternalHuntCapMult : 1);
                    var huntSec = (NowMs - _preyStartMs) / 1000.0;
                    // G-08B: keyword "prey" — resiste/amplifica o ramp de caça contra este alvo.
                    roll *= 1 + Math.Min(huntSec * rampPerSec, cap) * _traitMult * KeywordResistMult(monster, "prey");
                    // Eco Raízes Profundas: cada acerto na Presa a enraíza e crava veneno de terra.
                    if (HasEcho("deep_roots") && monster.Hp > 0)
                    {
                        monster.SlowUntilMs = NowMs + GameConfig.EchoDeepRootsSlowMs;
                        monster.SlowFactor = GameConfig.EchoDeepRootsSlowFactor;
                        ApplyDotToMonster(monster, "earth", GameConfig.ConditionTickFx["poison"],
                            PlayerAttack() * RoleSkillMult() * GameConfig.EchoDeepRootsDotPower,
                            GameConfig.EchoDeepRootsDotTicks, GameConfig.EchoDeepRootsDotTickMs);
                    }
                }
                break;
            }
        }
    }

    /// <summary>Pós-dano: marcas, stacks, carga, contágio e estilhaço (alvo ainda vivo).</summary>
    private void ApplyTraitPostDamage(Actor monster, int final, string element, bool directHit)
    {
        switch (_trait.Kind)
        {
            case "judgment": // Eloa — Pecado acumula; ao Julgar, o próximo acerto detona
                if (!directHit || monster.Hp <= 0) break;
                // Eco Sentença: Julga com menos Pecados.
                var sinToJudge = HasEcho("sentence")
                    ? GameConfig.EchoSentenceStacksToJudge : GameConfig.EloaSinStacksToJudge;
                if (ActiveSinStacks(monster) >= sinToJudge)
                {
                    monster.SinStacks = 0;
                    monster.SinUntilMs = 0;
                    EloaDetonate(monster, final);
                }
                else
                {
                    // G-08B: keyword "sin" — alvo resistente acumula Pecado mais devagar (imune = nunca).
                    var sinGain = KeywordScaledStacks(monster, "sin", 1);
                    if (sinGain > 0)
                    {
                        monster.SinStacks = ActiveSinStacks(monster) + sinGain;
                        monster.SinUntilMs = NowMs + GameConfig.EloaSinDurationMs;
                    }
                }
                break;

            case "decay": // Velvet — cada acerto empilha Decadência (DoT) e sobe o limiar
                if (!directHit || monster.Hp <= 0) break;
                // G-08B: keyword "curse" — alvo resistente recebe menos stacks de Maldição (imune = nenhum).
                var decayGain = KeywordScaledStacks(monster, "curse", 1);
                if (decayGain <= 0) break;
                monster.DecayStacks = Math.Min(ActiveDecayStacks(monster) + decayGain, GameConfig.VelvetDecayMaxStacks);
                monster.DecayUntilMs = NowMs + GameConfig.VelvetDecayDurationMs;
                var decayPower = PlayerAttack() * RoleSkillMult() * GameConfig.VelvetDecayDamagePerStack * monster.DecayStacks;
                ApplyDotToMonster(monster, "death", GameConfig.ConditionTickFx["curse"],
                    decayPower, GameConfig.VelvetDecayTicks, GameConfig.VelvetDecayTickMs);
                // Eco Pacto de Sangue: a carga de Maldição ergue escudo em vez de Velvet curar.
                if (HasEcho("blood_pact"))
                    GainEchoShield(decayPower * GameConfig.VelvetDecayTicks * GameConfig.EchoBloodPactShieldFraction);
                break;

            case "contagion": // Rin — acerto incendeia; tick de burn cura (pacto)
                // Eco Fogo Selvagem: qualquer elemento incendeia, não só fogo.
                if (directHit && monster.Hp > 0 && (element == "fire" || HasEcho("wildfire")))
                    ApplyContagionBurn(monster);
                else if (!directHit && element == "fire")
                    HealPlayer((int)Math.Max(final * _trait.Value * _traitMult, 0));
                break;

            case "static_charge": // Rynna — acertos enchem a carga; cheia, descarrega
                if (!directHit || monster.Hp <= 0) break;
                // Eco Tempestade Perpétua: a Carga enche o dobro de rápido.
                // G-08B: keyword "charge" — alvo resistente enche a Carga mais devagar.
                _staticCharge += GameConfig.RynnaChargePerHit
                    * (HasEcho("perpetual_storm") ? GameConfig.EchoPerpetualChargeMult : 1)
                    * KeywordResistMult(monster, "charge");
                if (_staticCharge >= GameConfig.RynnaChargeMax)
                {
                    // Eco Tempestade Perpétua: a Descarga retém metade da Carga.
                    _staticCharge = HasEcho("perpetual_storm")
                        ? GameConfig.RynnaChargeMax * GameConfig.EchoPerpetualDischargeRetain : 0;
                    RynnaDischarge(monster, final);
                }
                break;

            case "shatter": // Lunara — acerto no lento dá haste e conta pro estilhaço
                if (directHit && element == "ice" && monster.Hp > 0) ApplyShatter(monster);
                break;
        }
    }

    /// <summary>Gaia/Rin: ao matar um alvo, a caça salta de presa e o incêndio se propaga.</summary>
    private void OnMonsterKilledTrait(Actor monster)
    {
        if (_trait.Kind == "prey" && (monster.Id == _preyId || monster.Id == _preyId2))
        {
            monster.IsPrey = false;
            // Eco Matilha: bônus de caça maior ao executar.
            _preyHuntBonusUntilMs = NowMs + (HasEcho("pack")
                ? GameConfig.EchoPackHuntBonusMs : GameConfig.GaiaHuntBonusMs);
            if (monster.Id == _preyId2) _preyId2 = 0; // a Matilha re-marca a 2ª no próximo acerto
            if (monster.Id == _preyId)
            {
                _preyId = 0;
                var next = NearestLivingMonster(monster.X, monster.Y, m => m.Id != monster.Id && m.Id != _preyId2);
                if (next is not null) SetPrey(next);
            }
        }
        else if (_trait.Kind == "contagion" && IsBurning(monster))
        {
            SpreadBurnFrom(monster);
        }
    }

    /// <summary>Rin: salto periódico do incêndio independente de mortes.</summary>
    private void TickTraitTimers()
    {
        if (_trait.Kind != "contagion" || NowMs < _contagionNextJumpMs) return;
        _contagionNextJumpMs = NowMs + GameConfig.RinContagionIntervalMs;
        var src = FirstBurningMonster();
        if (src is null) return;
        SpreadBurnFrom(src);
        // Eco Fogo Selvagem: a queimadura não expira enquanto houver alvo em chamas.
        if (HasEcho("wildfire")) RefreshAllBurns();
    }

    /// <summary>Rin — Fogo Selvagem: renova a duração dos incêndios ativos (não acumula potência).</summary>
    private void RefreshAllBurns()
    {
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor) continue;
            foreach (var d in m.Dots)
                if (d.Element == "fire")
                {
                    d.TicksLeft = GameConfig.RinContagionBurnTicks;
                    d.NextTickAtMs = Math.Min(d.NextTickAtMs, NowMs + GameConfig.RinContagionBurnTickMs);
                }
        }
    }

    // ================= G-04: mecânica de cartas (raro/eco) =================
    // Hooks-irmãos das passivas de Kaeli: leem _cards (stacks) + tags, sem dispatch novo no engine.
    // Determinístico (só NowMs/_rng da run, contadores estáveis, desempate por id, soma de stacks
    // independe da ordem do dicionário). Bursts chamam DealDamageToMonster com fromTrait:true para
    // não re-disparar os próprios hooks (a guarda !fromTrait fecha trait e carta de uma vez).

    /// <summary>Stacks somados das cartas com um Kind de mecânica (0 se nenhuma equipada).</summary>
    private int CardKindStacks(string kind)
    {
        var total = 0;
        foreach (var (cardId, stacks) in _cards)
            if (Cards.ById[cardId].Kind == kind) total += stacks;
        return total;
    }

    private int CountEchoSpectres() => _summons.Count(s => s.IsEchoSpectre);

    /// <summary>G-04B: um Eco está equipado (cap de 1 stack → presença do Kind entre as cartas).</summary>
    private bool HasEcho(string kind) => CardKindStacks(kind) > 0;

    /// <summary>Ergue escudo de Eco (sobre-vida), limitado a uma fração da vida máxima.</summary>
    private void GainEchoShield(double amount)
    {
        if (amount <= 0) return;
        var cap = Player.MaxHp * GameConfig.EchoShieldCapFraction;
        _echoShield = Math.Min(_echoShield + amount, cap);
    }

    /// <summary>Absorve dano do escudo de Eco antes da vida; devolve o que sobrou para a vida levar.</summary>
    private int AbsorbWithEchoShield(int damage)
    {
        if (_echoShield <= 0 || damage <= 0) return damage;
        var absorbed = Math.Min(_echoShield, damage);
        _echoShield -= absorbed;
        return damage - (int)absorbed;
    }

    /// <summary>Pós-dano das cartas: enche a ultimate (Eco Sobrecarregado) e golpe extra (Golpe Duplo).
    /// Só conta acerto direto (auto/skill), igual às passivas.</summary>
    private void ApplyCardPostDamage(Actor monster, bool directHit)
    {
        if (!directHit) return;

        var surge = CardKindStacks("echo_surge");
        if (surge > 0)
            _gauge = Math.Min(
                _gauge + GameConfig.CardEchoSurgeGaugePerHit * surge * _gaugeRate,
                GameConfig.UltimateGaugeMax);

        var doubleStrike = CardKindStacks("double_strike");
        if (doubleStrike > 0 && monster.Hp > 0)
        {
            _cardDoubleStrikeHits++;
            if (_cardDoubleStrikeHits >= GameConfig.CardDoubleStrikeEvery)
            {
                _cardDoubleStrikeHits = 0;
                Emit("text", monster.X, monster.Y, 0, 0, 0, "GOLPE DUPLO");
                DealDamageToMonster(monster,
                    PlayerAttack() * RoleSkillMult() * GameConfig.CardDoubleStrikeDamageMult * doubleStrike,
                    CurrentStance.Element, 0,
                    fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            }
        }
    }

    /// <summary>Hooks de carta no on-kill (Velvet Colheita/Praga, Rin Holocausto). Cada Eco é
    /// checado de forma independente; lê o estado do alvo (Decadência/queimadura) antes da limpeza.</summary>
    private void OnMonsterKilledCard(Actor monster)
    {
        // Velvet — Colheita: morto sob Decadência ergue um espectro que pulsa dano (máx N).
        if (HasEcho("harvest") && ActiveDecayStacks(monster) > 0
            && CountEchoSpectres() < GameConfig.CardHarvestMaxSpectres)
        {
            var pulseMs = GameConfig.CardHarvestSpectrePulseMs;
            _summons.Add(new PlayerSummon
            {
                Floor = monster.Floor, X = monster.X, Y = monster.Y,
                Element = "death", Fx = GameConfig.CardHarvestSpectreFx,
                Radius = GameConfig.CardHarvestSpectreRadius,
                DamagePerPulse = PlayerAttack() * RoleSkillMult() * GameConfig.CardHarvestSpectreDamageMult,
                PulseMs = pulseMs, NextPulseAtMs = NowMs + pulseMs,
                ExpireAtMs = NowMs + GameConfig.CardHarvestSpectreDurationMs,
                IsEchoSpectre = true,
            });
            Emit("text", monster.X, monster.Y, 0, 0, 0, "COLHEITA");
            Emit("effect", monster.X, monster.Y, 0, 0, GameConfig.CardHarvestSpectreFx);
        }

        // Velvet — Praga Viral: ao morrer, a Decadência salta com seus stacks ao vivo mais próximo.
        if (HasEcho("viral_plague"))
        {
            var stacks = ActiveDecayStacks(monster);
            var next = stacks > 0 ? NearestLivingMonster(monster.X, monster.Y, m => m.Id != monster.Id) : null;
            if (next is not null)
            {
                next.DecayStacks = Math.Min(stacks, GameConfig.VelvetDecayMaxStacks);
                next.DecayUntilMs = NowMs + GameConfig.VelvetDecayDurationMs;
                ApplyDotToMonster(next, "death", GameConfig.ConditionTickFx["curse"],
                    PlayerAttack() * RoleSkillMult() * GameConfig.VelvetDecayDamagePerStack * next.DecayStacks,
                    GameConfig.VelvetDecayTicks, GameConfig.VelvetDecayTickMs);
                Emit("projectile", monster.X, monster.Y, next.X, next.Y, 11); // death missile
                Emit("text", next.X, next.Y, 0, 0, 0, "PRAGA");
            }
        }

        // Rin — Holocausto: alvo que morre em chamas explode num estouro de fogo em área.
        if (HasEcho("holocaust") && IsBurning(monster))
        {
            var burst = PlayerAttack() * RoleSkillMult() * GameConfig.EchoHolocaustDamageMult;
            Emit("text", monster.X, monster.Y, 0, 0, 0, "HOLOCAUSTO");
            foreach (var (tx, ty) in CircleTiles(monster.X, monster.Y, GameConfig.EchoHolocaustRadius))
            {
                Emit("effect", tx, ty, 0, 0, GameConfig.ConditionTickFx["fire"]);
                var victim = MonsterAt(tx, ty);
                if (victim is not null && victim.Id != monster.Id)
                    DealDamageToMonster(victim, burst, "fire", 0,
                        fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            }
        }
    }

    /// <summary>Detonação: uma condição que expira naturalmente (não por morte) explode em área.</summary>
    private void OnConditionExpiredCard(Actor monster, MonsterDot dot)
    {
        if (monster.Hp <= 0) return;
        var stacks = CardKindStacks("detonate");
        if (stacks <= 0) return;
        var burst = PlayerAttack() * RoleSkillMult() * GameConfig.CardDetonateDamageMult * stacks;
        Emit("text", monster.X, monster.Y, 0, 0, 0, "DETONAÇÃO");
        foreach (var (tx, ty) in CircleTiles(monster.X, monster.Y, GameConfig.CardDetonateRadius))
        {
            Emit("effect", tx, ty, 0, 0, dot.Fx);
            var victim = MonsterAt(tx, ty);
            if (victim is not null)
                DealDamageToMonster(victim, burst, dot.Element, 0,
                    fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
        }
    }

    // Eloa — detona o Selo numa pequena área e cura a Serafim por fração do estouro.
    private void EloaDetonate(Actor center, int triggerDamage)
    {
        var burst = triggerDamage * _trait.Value * _traitMult;
        // Eco Sentença: cada Julgamento amplia o próximo estouro (acumula até um teto).
        if (HasEcho("sentence"))
        {
            burst *= 1 + _eloaSentenceStacks * GameConfig.EchoSentenceBurstPerStack;
            _eloaSentenceStacks = Math.Min(_eloaSentenceStacks + 1, GameConfig.EchoSentenceMaxStacks);
        }
        var chain = HasEcho("chain_judgment"); // espalha Pecado nos atingidos
        Emit("text", center.X, center.Y, 0, 0, 0, "JULGADO");
        foreach (var (tx, ty) in CircleTiles(center.X, center.Y, GameConfig.EloaJudgmentRadius))
        {
            Emit("effect", tx, ty, 0, 0, 40); // holy area fx
            var victim = MonsterAt(tx, ty);
            if (victim is null) continue;
            DealDamageToMonster(victim, burst, "holy", 0,
                fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            if (chain && victim.Id != center.Id && victim.Hp > 0)
            {
                victim.SinStacks = Math.Min(
                    ActiveSinStacks(victim) + GameConfig.EchoEloaChainSinSeed, GameConfig.EloaSinStacksToJudge);
                victim.SinUntilMs = NowMs + GameConfig.EloaSinDurationMs;
            }
        }
        // Eco Mártir: a cura do Julgamento vira escudo acima da vida, em vez de curar.
        var grace = Math.Max(burst * _trait.Param, 0);
        if (HasEcho("martyr")) GainEchoShield(grace);
        else HealPlayer((int)grace);
    }

    // Rynna — corrente curta que paralisa os alvos próximos e acelera a ultimate.
    private void RynnaDischarge(Actor origin, int triggerDamage)
    {
        Emit("text", origin.X, origin.Y, 0, 0, 0, "DESCARGA");
        var dmg = PlayerAttack() * RoleSkillMult() * GameConfig.RynnaDischargeDamageMult;
        // Eco Núcleo de Trovão: a Descarga enche a ultimate muito mais rápido.
        var gaugeBonus = GameConfig.RynnaParalyzeGaugeBonus
            * (HasEcho("thunder_core") ? GameConfig.EchoThunderCoreGaugeMult : 1);
        var overload = HasEcho("overload"); // paralyze vira DoT de eletrocussão
        var hit = new HashSet<int>();
        var current = origin;
        int fromX = Player.X, fromY = Player.Y;
        for (var h = 0; h < GameConfig.RynnaDischargeChainJumps && current is not null; h++)
        {
            hit.Add(current.Id);
            Emit("projectile", fromX, fromY, current.X, current.Y, 5); // energy
            current.StunUntilMs = Math.Max(current.StunUntilMs, NowMs + GameConfig.RynnaParalyzeMs);
            Emit("effect", current.X, current.Y, 0, 0, 32); // stun stars
            _gauge = Math.Min(_gauge + gaugeBonus, GameConfig.UltimateGaugeMax);
            if (overload)
                ApplyDotToMonster(current, "energy", GameConfig.ConditionTickFx["energy"],
                    PlayerAttack() * RoleSkillMult() * GameConfig.EchoOverloadDotPower,
                    GameConfig.EchoOverloadDotTicks, GameConfig.EchoOverloadDotTickMs);
            fromX = current.X; fromY = current.Y;
            DealDamageToMonster(current, dmg, "energy", 12,
                fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
            current = NearestUnhitMonster(fromX, fromY, GameConfig.RynnaDischargeChainRange, hit);
        }
    }

    // Lunara — haste breve ao bater no lento; o 3º acerto estilhaça e consome o slow.
    private void ApplyShatter(Actor monster)
    {
        // Eco Dança da Lua: estilhaça já no 2º acerto; a haste do trait quase não expira em combate.
        var shatterHits = HasEcho("moon_dance") ? GameConfig.EchoMoonDanceShatterHits : GameConfig.LunaraShatterHits;
        if (NowMs < monster.SlowUntilMs)
        {
            _traitHasteUntilMs = NowMs + (HasEcho("moon_dance") ? GameConfig.LunaraHasteMs * 15 : GameConfig.LunaraHasteMs);
            _traitHasteFactor = GameConfig.LunaraHasteFactor;
            // G-08B: keyword "frost" — alvo resistente acumula acertos de gelo mais devagar (imune = nunca estilhaça).
            monster.FrostHits += KeywordScaledStacks(monster, "frost", 1);
            if (monster.FrostHits >= shatterHits)
            {
                monster.FrostHits = 0;
                Emit("text", monster.X, monster.Y, 0, 0, 0, "ESTILHAÇO");
                Emit("effect", monster.X, monster.Y, 0, 0, 44); // ice fx
                DealDamageToMonster(monster, PlayerAttack() * RoleSkillMult() * GameConfig.LunaraShatterDamageMult, "ice", 0,
                    fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
                // Eco Estilhaço em Cadeia: repassa o estouro aos lentos próximos.
                if (HasEcho("chain_shatter")) ChainShatterFrom(monster);
                monster.SlowUntilMs = NowMs; // consome o slow
                return;
            }
        }
        else
        {
            monster.FrostHits = KeywordScaledStacks(monster, "frost", 1);
        }
        monster.SlowUntilMs = NowMs + (long)_trait.Param;
        // Eco Inverno Eterno: lentidão mais forte (sem piso).
        monster.SlowFactor = HasEcho("eternal_winter")
            ? GameConfig.EchoEternalWinterSlowFactor : GameConfig.LunaraSlowFactor;
    }

    /// <summary>Lunara — Estilhaço em Cadeia: o estouro salta para os lentos próximos (fração do dano).</summary>
    private void ChainShatterFrom(Actor source)
    {
        var burst = PlayerAttack() * RoleSkillMult() * GameConfig.LunaraShatterDamageMult * GameConfig.EchoChainShatterDamageMult;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || m.Id == source.Id) continue;
            if (NowMs >= m.SlowUntilMs) continue; // só lentos
            if (Chebyshev(source.X, source.Y, m.X, m.Y) > GameConfig.EchoChainShatterRange) continue;
            Emit("projectile", source.X, source.Y, m.X, m.Y, 29); // ice missile
            Emit("effect", m.X, m.Y, 0, 0, 44);
            DealDamageToMonster(m, burst, "ice", 0,
                fromSkill: true, canCrit: false, canLifeSteal: false, fromTrait: true);
        }
    }

    private void ApplyContagionBurn(Actor monster)
    {
        // G-08B: keyword "burn" — alvo resistente queima menos; imune (100) não pega fogo.
        var mult = KeywordResistMult(monster, "burn");
        if (mult <= 0) return;
        ApplyDotToMonster(monster, "fire", GameConfig.ConditionTickFx["fire"],
            PlayerAttack() * RoleSkillMult() * GameConfig.RinContagionBurnPower * mult,
            GameConfig.RinContagionBurnTicks, GameConfig.RinContagionBurnTickMs);
    }

    private void SpreadBurnFrom(Actor source)
    {
        var dst = NearestLivingMonster(source.X, source.Y,
            m => m.Id != source.Id && !IsBurning(m)
                 && Chebyshev(source.X, source.Y, m.X, m.Y) <= (int)_trait.Param);
        if (dst is null) return;
        ApplyContagionBurn(dst);
        Emit("projectile", source.X, source.Y, dst.X, dst.Y, 4); // fire arc
    }

    private void SetPrey(Actor m)
    {
        foreach (var mon in _monsters)
            if (mon.IsPrey && mon.Id != m.Id && mon.Id != _preyId2) mon.IsPrey = false; // Matilha preserva a 2ª
        _preyId = m.Id;
        _preyStartMs = NowMs;
        m.IsPrey = true;
        Emit("text", m.X, m.Y, 0, 0, 0, "PRESA");
    }

    /// <summary>Gaia — Matilha: marca uma segunda Presa simultânea (compartilha o ramp da caça).</summary>
    private void SetSecondPrey(Actor primary)
    {
        var second = NearestLivingMonster(primary.X, primary.Y, m => m.Id != _preyId && m.Id != primary.Id);
        if (second is null) return;
        _preyId2 = second.Id;
        second.IsPrey = true;
        Emit("text", second.X, second.Y, 0, 0, 0, "PRESA");
    }

    private int ActiveSinStacks(Actor m)
    {
        if (NowMs >= m.SinUntilMs) m.SinStacks = 0;
        return m.SinStacks;
    }

    private int ActiveDecayStacks(Actor m)
    {
        if (NowMs >= m.DecayUntilMs) m.DecayStacks = 0;
        return m.DecayStacks;
    }

    private bool IsMonsterAlive(int id) => _monsters.Any(m => m.Id == id && m.Hp > 0);

    private static bool IsBurning(Actor m) => m.Dots.Any(d => d.Element == "fire");

    private Actor? FirstBurningMonster()
    {
        Actor? best = null;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || !IsBurning(m)) continue;
            if (best is null || m.Id < best.Id) best = m;
        }
        return best;
    }

    private Actor? NearestLivingMonster(int x, int y, Func<Actor, bool> predicate)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != _currentFloor || !predicate(m)) continue;
            var d = Chebyshev(x, y, m.X, m.Y);
            if (d < bestDist || (d == bestDist && (best is null || m.Id < best.Id)))
            {
                bestDist = d;
                best = m;
            }
        }
        return best;
    }

    // ---- F-E: boss posture (echo break) ----

    private void AddPosture(Actor boss, string element, bool fromSkill)
    {
        var gain = fromSkill ? GameConfig.PostureGainPerSkill : GameConfig.PostureGainPerAuto;
        if (boss.Species!.Elements.GetValueOrDefault(element, 0) < 0)
            gain *= GameConfig.PostureWeaknessMult; // hitting a weakness breaks faster
        // G-08B: keyword "posture" — alvo resistente é mais difícil de quebrar (negativo = quebra mais rápido).
        gain *= KeywordResistMult(boss, "posture");
        boss.Posture += gain;
        boss.PostureLastHitMs = NowMs;
        if (boss.Posture >= boss.PostureMax) TriggerEchoBreak(boss);
    }

    private void TriggerEchoBreak(Actor boss)
    {
        var idx = Math.Min(boss.PostureCycle, GameConfig.PostureDamageMultipliers.Length - 1);
        boss.StaggerMultiplier = GameConfig.PostureDamageMultipliers[idx];
        boss.StaggerUntilMs = NowMs + GameConfig.PostureStaggerMs;
        boss.StunUntilMs = Math.Max(boss.StunUntilMs, boss.StaggerUntilMs);
        boss.PostureBonusReadyAtMs = 0; // first stagger hit may grant the maxHP bonus immediately
        boss.PostureCycle++;
        boss.PostureMax = boss.PostureBaseMax * (1 + boss.PostureCycle * GameConfig.PostureMaxGrowthPerCycle);
        boss.Posture = 0;
        Emit("effect", boss.X, boss.Y, 0, 0, 35); // big explosion
        Emit("text", boss.X, boss.Y, 0, 0, 0, "QUEBRADO!");
    }

    /// <summary>Posture bleeds back down once the boss stops taking pressure (no free fill).</summary>
    private void TickPostureDecay()
    {
        foreach (var m in _monsters)
        {
            if (m.PostureMax <= 0 || m.Hp <= 0 || m.Posture <= 0) continue;
            if (m.IsStaggered(NowMs) || NowMs - m.PostureLastHitMs < GameConfig.PostureDecayDelayMs) continue;
            var decay = m.PostureMax * GameConfig.PostureDecayFractionPerSec * GameConfig.TickMs / 1000.0;
            m.Posture = Math.Max(0, m.Posture - decay);
        }
    }

    // ---- F-E: elemental reactions ----

    private void ApplyElementMarkAndReactions(Actor monster, string element, int hitDamage)
    {
        if (!ElementReactions.IsReactive(element)) return;

        var mark = monster.ActiveMark(NowMs);
        if (mark.Length > 0 && mark != element
            && ElementReactions.Lookup(mark, element) is { } reaction)
        {
            monster.ElementMark = "";
            monster.ElementMarkUntilMs = 0;
            TriggerReaction(monster, reaction, hitDamage);
            return;
        }

        monster.ElementMark = element;
        monster.ElementMarkUntilMs = NowMs + GameConfig.ElementMarkDurationMs;
    }

    private void TriggerReaction(Actor monster, ReactionDef reaction, int hitDamage)
    {
        var damage = Math.Max((int)(hitDamage * reaction.DamageFraction), 1);
        Emit("effect", monster.X, monster.Y, 0, 0, reaction.Fx);
        Emit("text", monster.X, monster.Y, 0, 0, 0, reaction.Name);

        if (reaction.Radius > 0)
        {
            foreach (var (tx, ty) in CircleTiles(monster.X, monster.Y, reaction.Radius))
            {
                var victim = MonsterAt(tx, ty);
                if (victim is null) continue;
                if (tx != monster.X || ty != monster.Y) Emit("effect", tx, ty, 0, 0, reaction.Fx);
                DealReactionDamage(victim, damage, killIfDead: victim.Id != monster.Id);
            }
        }
        else
        {
            DealReactionDamage(monster, damage, killIfDead: false);
        }

        if (monster.Hp <= 0) return;
        if (reaction.StunMs > 0)
        {
            monster.StunUntilMs = Math.Max(monster.StunUntilMs, NowMs + reaction.StunMs);
            Emit("effect", monster.X, monster.Y, 0, 0, 32); // stun stars
        }
        if (reaction.SlowMs > 0)
        {
            monster.SlowUntilMs = NowMs + reaction.SlowMs;
            monster.SlowFactor = reaction.SlowFactor;
        }
    }

    /// <summary>
    /// Flat reaction damage: never re-marks or re-triggers (no recursion). The primary target
    /// is left to <see cref="DealDamageToMonster"/>'s trailing kill check; splash victims kill here.
    /// </summary>
    private void DealReactionDamage(Actor victim, int amount, bool killIfDead)
    {
        if (victim.Hp <= 0) return;
        victim.Hp -= amount;
        Emit("damage", victim.X, victim.Y, 0, 0, amount, "", victim.Id, true);
        if (victim.TargetId == 0) AcquirePlayer(victim);
        if (killIfDead && victim.Hp <= 0) KillMonster(victim);
    }

    private int BestiaryRank(string species)
    {
        var kills = _bestiaryKills.GetValueOrDefault(species, 0);
        var rank = 0;
        foreach (var threshold in GameConfig.BestiaryRankKills)
            if (kills >= threshold) rank++;
        return rank;
    }

    private void KillMonster(Actor monster, bool overkill = false)
    {
        // guarda contra dupla contagem: bursts de trait (julgamento, estilhaço, descarga) podem
        // matar o mesmo alvo já marcado pra morrer pelo acerto que os disparou.
        if (monster.Killed) return;
        monster.Killed = true;
        monster.Hp = 0;
        _kills++;
        var speciesId = monster.Species!.StableId;
        KillsBySpecies[speciesId] = KillsBySpecies.GetValueOrDefault(speciesId) + 1;

        // K-04: caça da Gaia salta de presa; incêndio da Rin se propaga ao morrer um alvo em chamas
        OnMonsterKilledTrait(monster);
        // G-04: Colheita da Velvet — espectro ao matar sob Decadência (lê o estado antes de limpar).
        OnMonsterKilledCard(monster);

        Emit("death", monster.X, monster.Y, 0, 0, monster.Species.Corpse, monster.Species.Name, monster.Id);

        // G-08B: bomber/suicida — estoura em área ao morrer perto do player.
        if (GameConfig.BehaviorProfile(monster.Species.BehaviorId) is { ExplodeRadius: > 0 } bomber)
            BomberExplode(monster, bomber);

        // xp + gauge
        var xpScale = monster.Species.IsAuthored ? 1 : Tier.StatMultiplier;
        var xp = (long)(monster.Species.Experience * xpScale * (1 + CardValue("xpPercent")));
        GainXp(Math.Max(xp, 1));
        _gauge = Math.Min(_gauge + GameConfig.GaugeFillPerKill * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);

        if (!monster.IsSummon) DropLoot(monster); // summons give xp but no loot (anti-farm)

        // G-09: o mímico (baú-Eco corrompido) garante material de gear ao cair.
        if (monster.IsMimic)
            for (var i = 0; i < GameConfig.CursedChestMaterialDrops; i++) GrantGearMaterial(monster.X, monster.Y);

        // G-06: derrotar um elite de sala comum é um beat — concede uma escolha de carta pesada.
        if (monster.IsElite && !monster.IsSummon) OfferCardBeat();

        // LM-03 (3) condição de fim: o modo decide o que uma morte significa (Dungeon: boss = vitória).
        _modeRules.OnMonsterKilled(this, monster);
    }

    /// <summary>G-08B: estouro do bomber ao morrer — pinta a área e fere o player se estiver no raio.
    /// Determinístico: dano derivado do kit (maior MaxDamage) × escala, sem _rng.</summary>
    private void BomberExplode(Actor bomber, MonsterBehaviorProfile profile)
    {
        Emit("effect", bomber.X, bomber.Y, 0, 0, GameConfig.BomberExplodeFx);
        foreach (var (tx, ty) in CircleTiles(bomber.X, bomber.Y, profile.ExplodeRadius))
            if (tx != bomber.X || ty != bomber.Y)
                Emit("effect", tx, ty, 0, 0, GameConfig.BomberExplodeFx);

        if (Player.Hp <= 0) return;
        if (Chebyshev(bomber.X, bomber.Y, Player.X, Player.Y) > profile.ExplodeRadius) return;

        var baseDamage = bomber.Species!.Attacks.Count > 0
            ? bomber.Species.Attacks.Max(a => a.MaxDamage)
            : Math.Max(bomber.MaxHp / 10, 1);
        var element = bomber.Species.Attacks.FirstOrDefault()?.DamageType ?? "physical";
        var damage = Math.Max((int)(baseDamage * profile.ExplodeDamageScale * bomber.StatMult), 1);
        DamagePlayer(damage, element, bomber);
    }

    private void DropLoot(Actor monster)
    {
        if (monster.Species!.IsAuthored)
        {
            DropKaezanLoot(monster);
            return;
        }

        var junkGold = 0L;
        foreach (var entry in monster.Species!.Loot)
        {
            if (!_rng.Chance(entry.Chance / 100000.0)) continue;
            var count = entry.MaxCount > 1 ? _rng.Range(1, entry.MaxCount) : 1;
            if (entry.Name.Contains("gold coin", StringComparison.OrdinalIgnoreCase))
            {
                var gold = (long)(count * (1 + CardValue("goldPercent")) * Tier.StatMultiplier);
                _gold += gold;
                EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", monster.X, monster.Y, isGold: true);
                continue;
            }
            // comida/poção/equip são coletados na hora (voam até o player); o resto (lixo) vira ouro
            if (_data.IsFood(entry.ItemId) || _data.PotionHealFraction(entry.ItemId) > 0
                || _data.IsEquippableLoot(entry.ItemId))
            {
                CollectLoot(entry.ItemId, entry.Name, count, monster.X, monster.Y);
                continue;
            }
            junkGold += (long)(_items?.Value(entry.ItemId) ?? _data.ItemValue(entry.ItemId)) * count;
        }

        if (junkGold > 0)
        {
            var gold = (long)(junkGold * (1 + CardValue("goldPercent")));
            _gold += gold;
            EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", monster.X, monster.Y, isGold: true);
        }

        if (monster.IsBossActor
            && GameConfig.TierMountLookTypes.TryGetValue(Tier.Tier, out var mountLookType)
            && _rng.Chance(GameConfig.BossMountDropChance))
        {
            var itemId = GameConfig.MountItemId(mountLookType);
            if (_data.Items.TryGetValue(itemId, out var mount))
                CollectLoot(mount.ItemId, mount.Name, 1, monster.X, monster.Y);
        }
    }

    private void DropKaezanLoot(Actor monster)
    {
        var rank = monster.IsBossActor ? "boss" : monster.Species!.Rank;
        var (minGold, maxGold) = GameConfig.KaezanDropGoldRange(Tier.Tier, rank);
        var gold = (long)(_rng.Range(minGold, maxGold) * (1 + CardValue("goldPercent")));
        _gold += gold;
        EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", monster.X, monster.Y, isGold: true);

        var itemChance = monster.IsBossActor
            ? 1.0
            : rank == "elite"
                ? GameConfig.KaezanEliteItemDropChance
                : GameConfig.KaezanCommonItemDropChance;
        if (!_rng.Chance(itemChance)) return;

        if (rank == "boss"
            && _rng.Chance(GameConfig.KaezanBossRelicDropChance)
            && TryPickKaezanDropItem(rank, relicOnly: true, out var relic))
        {
            CollectLoot(relic.ItemId, relic.Name, 1, monster.X, monster.Y);
            return;
        }

        if (TryPickKaezanDropItem(rank, relicOnly: false, out var item))
            CollectLoot(item.ItemId, item.Name, 1, monster.X, monster.Y);
    }

    private bool TryPickKaezanDropItem(string rank, bool relicOnly, out ItemType item)
    {
        item = default!;
        if (_items is null) return false;

        var pool = _items.All.Values
            .Where(candidate =>
                candidate.IsAuthored
                && candidate.Tier == Tier.Tier
                && candidate.ItemId >= GameConfig.AuthoredItemIdBase
                && candidate.Slot is not null
                && (relicOnly
                    ? candidate.Tag == GameConfig.AuthoredItemTagRelic
                    : candidate.Tag != GameConfig.AuthoredItemTagRelic))
            .OrderBy(candidate => candidate.ItemId)
            .ToList();
        if (pool.Count == 0) return false;

        var classItems = pool
            .Where(candidate => candidate.AllowedClassIds?.Contains(Waifu.ClassId, StringComparer.OrdinalIgnoreCase) == true)
            .ToList();
        var genericItems = pool
            .Where(candidate => candidate.AllowedClassIds is null || candidate.AllowedClassIds.Count == 0)
            .ToList();

        var classWeight = rank switch
        {
            "boss" => GameConfig.KaezanBossClassDropWeight,
            "elite" => GameConfig.KaezanEliteClassDropWeight,
            "chest" => GameConfig.KaezanChestClassDropWeight,
            _ => GameConfig.KaezanCommonClassDropWeight
        };
        var preferred = _rng.Chance(classWeight) ? classItems : genericItems;
        var fallback = ReferenceEquals(preferred, classItems) ? genericItems : classItems;
        var source = preferred.Count > 0 ? preferred : fallback.Count > 0 ? fallback : pool;
        item = _rng.Pick(source);
        return true;
    }

    /// <summary>
    /// Credita um item de loot imediatamente (sem cair no chão): consumíveis curam na hora,
    /// o resto vai pra mochila da run. Sempre dispara o efeito de voo do item até o player.
    /// </summary>
    private void CollectLoot(int itemId, string name, int count, int fromX, int fromY)
    {
        var potionFraction = _data.PotionHealFraction(itemId);
        if (potionFraction > 0)
            HealPlayer((int)Math.Ceiling(Player.MaxHp * potionFraction) * count);
        else if (_data.IsFood(itemId))
            HealPlayer((int)Math.Ceiling(Player.MaxHp * GameConfig.FoodHealPct) * count);
        else
            AddLootedItem(itemId, name, count);

        EmitLootFly(itemId, name, fromX, fromY, isGold: false);
    }

    /// <summary>Evento visual: o item/moeda voa em arco da origem até o player. crit=true marca ouro (cor dourada).</summary>
    private void EmitLootFly(int spriteItemId, string label, int fromX, int fromY, bool isGold) =>
        Emit("loot", fromX, fromY, 0, 0, spriteItemId, label, 0, isGold);

    private void TryUsePotion()
    {
        if (Player.Hp <= 0 || _potionCharges <= 0 || NowMs < _potionReadyAtMs) return;
        if (Player.Hp >= Player.MaxHp) return; // não desperdiça carga com vida cheia
        var heal = (int)Math.Ceiling(Player.MaxHp * GameConfig.PotionSlotHealFraction(Tier.Tier));
        HealPlayer(heal);
        _potionCharges--;
        _potionReadyAtMs = NowMs + GameConfig.PotionCooldownMs;
        Emit("effect", Player.X, Player.Y, 0, 0, 12); // sparkles
        Emit("heal", Player.X, Player.Y, 0, 0, heal);
    }

    private void GainXp(long xp)
    {
        _runXp += xp;
        while (_runLevel < GameConfig.MaxRunLevel && _runXp >= GameConfig.XpForRunLevel(_runLevel))
        {
            _runXp -= GameConfig.XpForRunLevel(_runLevel);
            _runLevel++;
            Emit("levelup", Player.X, Player.Y, 0, 0, _runLevel);
            Emit("effect", Player.X, Player.Y, 0, 0, 182); // magic powder
            // G-06: level-up = status pequeno automático (sem tela). As escolhas pesadas vêm dos
            // beats (elite/andar/santuário), não de cada level.
            GrantAutoStatus();
        }
    }

    /// <summary>
    /// G-06: progresso da run em [0,1] medido pela fração de escolhas já concedidas — usado pra
    /// escalar a raridade da oferta (começo monta a engine, fim define). Determinístico.
    /// </summary>
    private double RunChoiceProgress => GameConfig.MaxCardChoicesPerRun <= 1
        ? 1.0
        : Math.Clamp((_choicesOffered - 1) / (double)(GameConfig.MaxCardChoicesPerRun - 1), 0, 1);

    /// <summary>
    /// G-06: concede uma escolha de carta pesada num beat fixo (elite derrotado, andar limpo, sala
    /// Santuário). Respeita o teto de escolhas por run e reusa a fila quando já há oferta aberta.
    /// </summary>
    private void OfferCardBeat(bool blessed = false)
    {
        if (Ended is not null || _choicesOffered >= GameConfig.MaxCardChoicesPerRun) return;
        if (AvailableCardPool(null).Count == 0) return;
        _choicesOffered++;
        // G-09: oferta abençoada (baú amaldiçoado) só vale para a oferta aberta na hora; as enfileiradas
        // usam ponderação normal (mantém o teto/cadência sem acumular bênçãos).
        if (_pendingOffer is null) OfferCards(blessed);
        else _queuedOffers++;
    }

    /// <summary>
    /// G-06: status pequeno automático do level-up — sorteia uma carta comum (respeitando bans/caps)
    /// e aplica um stack na hora, sem abrir tela de escolha. Determinístico via _rng da run.
    /// </summary>
    private void GrantAutoStatus()
    {
        var pool = Cards.All
            .Where(c => c.Rarity == Cards.Common && !_bannedCards.Contains(c.Id))
            .Where(c => _cards.GetValueOrDefault(c.Id) < GameConfig.MaxStacksForRarity(c.Rarity))
            .ToList();
        if (pool.Count == 0) return;
        var pick = _rng.Pick(pool);
        ApplyCardStack(pick.Id);
        Emit("text", Player.X, Player.Y, 0, 0, 0, pick.Name);
    }

    /// <summary>Aplica um stack de carta e seu efeito imediato (ex. maxhp cura o bônus ganho).</summary>
    private void ApplyCardStack(string cardId)
    {
        _cards[cardId] = _cards.GetValueOrDefault(cardId) + 1;
        if (cardId == "card:maxhp")
        {
            var def = Cards.ById[cardId];
            var bonus = (int)(Waifu.BaseHp * def.Value);
            Player.MaxHp += bonus;
            HealPlayer(bonus);
        }
    }

    private void OfferCards(bool blessed = false)
    {
        // G-04: pool ciente de raridade. Eco filtra pela Kaeli ativa; cada raridade tem seu cap de
        // stacks. A amostragem é ponderada por raridade (sem repor), determinística via _rng da run.
        // G-09: blessed (baú amaldiçoado) pondera como o fim da run — favorece raro/eco.
        _offerBlessed = blessed;
        var offer = BuildCardOffer();
        if (offer.Count == 0) { _offerBlessed = false; return; }
        _pendingOffer = offer;
        _cardOfferStartedTick = TickCount;
    }

    /// <summary>G-09: progresso de ponderação da oferta atual — abençoada salta para o fim da curva.</summary>
    private double OfferProgress => _offerBlessed
        ? Math.Max(RunChoiceProgress, GameConfig.BlessedOfferProgress)
        : RunChoiceProgress;

    /// <summary>Monta uma oferta usando o pool de cartas disponivel para a run.</summary>
    private List<CardOfferDto> BuildCardOffer(IReadOnlySet<string>? temporaryExcluded = null)
    {
        var offer = DrawCardOffer(AvailableCardPool(temporaryExcluded));

        if (temporaryExcluded is not null)
        {
            var desiredCount = Math.Min(GameConfig.CardChoicesPerOffer, AvailableCardPool(null).Count);
            if (offer.Count < desiredCount)
            {
                var picked = offer.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
                var fallback = AvailableCardPool(null)
                    .Where(c => temporaryExcluded.Contains(c.Id) && !picked.Contains(c.Id))
                    .ToList();

                while (offer.Count < desiredCount && fallback.Count > 0)
                {
                    var pick = WeightedPickByRarity(fallback);
                    fallback.Remove(pick);
                    offer.Add(ToOfferDto(pick));
                }
            }
        }

        return offer;
    }

    private List<CardDef> AvailableCardPool(IReadOnlySet<string>? temporaryExcluded) =>
        Cards.All
            .Where(c => !_bannedCards.Contains(c.Id))
            .Where(c => temporaryExcluded is null || !temporaryExcluded.Contains(c.Id))
            .Where(c => _cards.GetValueOrDefault(c.Id) < GameConfig.MaxStacksForRarity(c.Rarity))
            .Where(c => c.WaifuId is null || c.WaifuId == Waifu.Id)
            .ToList();

    private List<CardOfferDto> DrawCardOffer(List<CardDef> pool)
    {
        var offer = new List<CardOfferDto>();
        for (var n = 0; n < GameConfig.CardChoicesPerOffer && pool.Count > 0; n++)
        {
            var pick = WeightedPickByRarity(pool);
            pool.Remove(pick);
            offer.Add(ToOfferDto(pick));
        }
        return offer;
    }

    /// <summary>Amostra uma carta do pool com peso por raridade. Ordem estavel do pool + _rng -> deterministico.</summary>
    private CardDef WeightedPickByRarity(List<CardDef> pool)
    {
        // G-06: pesos escalados pelo progresso da run (raro/eco ganham peso perto do fim).
        // G-09: ofertas abençoadas (baú amaldiçoado) usam o progresso saltado de OfferProgress.
        var progress = OfferProgress;
        var total = 0.0;
        foreach (var c in pool) total += GameConfig.CardRarityWeight(c.Rarity, progress);
        var roll = _rng.NextDouble() * total;
        foreach (var c in pool)
        {
            roll -= GameConfig.CardRarityWeight(c.Rarity, progress);
            if (roll <= 0) return c;
        }
        return pool[^1]; // guarda contra erro de ponto flutuante
    }

    private CardOfferDto ToOfferDto(CardDef c) => new(
        c.Id, c.Name, c.Description, _cards.GetValueOrDefault(c.Id),
        c.Rarity, c.TagList, GameConfig.MaxStacksForRarity(c.Rarity));

    // G-10: melhor carta da oferta atual para o auto-pick — maior raridade, desempate por ordem
    // estável (índice na oferta). Determinístico.
    private string BestOfferCardId()
    {
        var offer = _pendingOffer!;
        var bestId = offer[0].Id;
        var bestRank = RarityRank(offer[0].Rarity);
        for (var i = 1; i < offer.Count; i++)
        {
            var rank = RarityRank(offer[i].Rarity);
            if (rank > bestRank)
            {
                bestRank = rank;
                bestId = offer[i].Id;
            }
        }
        return bestId;
    }

    private static int RarityRank(string rarity) => rarity switch
    {
        "echo" => 3,
        "rare" => 2,
        _ => 1
    };

    private void ChooseCard(string cardId)
    {
        if (_pendingOffer is null || !_pendingOffer.Any(o => o.Id == cardId)) return;
        _pendingOffer = null;
        ApplyCardStack(cardId);

        if (_queuedOffers > 0)
        {
            _queuedOffers--;
            OfferCards();
        }
        else _offerBlessed = false;
    }

    private void RerollCards()
    {
        if (_pendingOffer is null) return;
        // G-09: rerolls grátis primeiro; esgotados, vira reroll pago (a "loja" do altar da run).
        var paid = _cardRerollsRemaining <= 0;
        if (paid && _gold < GameConfig.CardRerollGoldCost) return;

        var currentIds = _pendingOffer.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        if (AvailableCardPool(currentIds).Count == 0) return;

        var offer = BuildCardOffer(currentIds);
        if (offer.Count == 0) return;

        if (paid)
        {
            _gold -= GameConfig.CardRerollGoldCost;
            EmitLootFly(GameConfig.GoldCoinItemId, $"-{GameConfig.CardRerollGoldCost} gold", Player.X, Player.Y, isGold: true);
        }
        else _cardRerollsRemaining--;
        _pendingOffer = offer;
        _cardOfferStartedTick = TickCount;
        Emit("text", Player.X, Player.Y, 0, 0, 0, "REROLL");
    }

    private void BanCard(string cardId)
    {
        if (_pendingOffer is null || !_pendingOffer.Any(o => o.Id == cardId)) return;
        if (!_bannedCards.Add(cardId)) return;

        var kept = _pendingOffer.Where(o => o.Id != cardId).ToList();
        var keptIds = kept.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var pool = AvailableCardPool(keptIds);
        while (kept.Count < GameConfig.CardChoicesPerOffer && pool.Count > 0)
        {
            var pick = WeightedPickByRarity(pool);
            pool.Remove(pick);
            kept.Add(ToOfferDto(pick));
        }

        _pendingOffer = kept.Count > 0 ? kept : null;
        _cardOfferStartedTick = TickCount;
        Emit("text", Player.X, Player.Y, 0, 0, 0, "BANIDA");

        if (_pendingOffer is null && _queuedOffers > 0)
        {
            _queuedOffers--;
            OfferCards();
        }
    }

    private void HealPlayer(int amount)
    {
        if (amount <= 0 || Player.Hp <= 0) return;
        Player.Hp = Math.Min(Player.Hp + amount, Player.MaxHp);
    }

    private void TickPlayerRegen()
    {
        if (Player.Hp <= 0) return;
        // regen baseline (sem depender de card) + bônus do card de regen
        var baselinePct = GameConfig.BaselineRegenPctPerSec
                          + GameConfig.BaselineRegenPctPerRunLevel * (_runLevel - 1);
        var regen = CardValue("regenPerSec") + Player.MaxHp * baselinePct;
        if (regen <= 0) return;
        _regenCarry += regen * GameConfig.TickMs / 1000.0;
        if (_regenCarry >= 1)
        {
            var whole = (int)_regenCarry;
            _regenCarry -= whole;
            HealPlayer(whole);
        }
    }

    // ---- conditions on the player (T-53/T-14: tibia DoT + slow) ----

    private void ApplyConditionToPlayer(MonsterCondition cond, Actor source)
    {
        var tickMs = cond.TickMs > 0 ? cond.TickMs : GameConfig.ConditionDefaultTickMs;
        var ticks = cond.DurationMs > 0 ? Math.Max(cond.DurationMs / tickMs, 1) : GameConfig.ConditionMaxTicks;
        ticks = Math.Min(ticks, GameConfig.ConditionMaxTicks);
        var perTick = Math.Max(
            (int)(cond.TotalDamage / (double)ticks * source.StatMult
                  * (source.Species?.IsAuthored == true ? 1 : GameConfig.MonsterDamageTuning)), 1);

        // reapplying never stacks: it replaces only if the new condition is at least as strong
        var existing = _playerConditions.FirstOrDefault(c => c.Type == cond.Type);
        if (existing is not null)
        {
            if ((long)perTick * ticks < (long)existing.DamagePerTick * existing.TicksLeft) return;
            _playerConditions.Remove(existing);
        }

        _playerConditions.Add(new ActiveCondition
        {
            Type = cond.Type, DamagePerTick = perTick, TicksLeft = ticks,
            TickMs = tickMs, NextTickAtMs = NowMs + tickMs,
        });
        if (GameConfig.ConditionTickFx.TryGetValue(cond.Type, out var fx))
            Emit("effect", Player.X, Player.Y, 0, 0, fx);
    }

    private void TickPlayerConditions()
    {
        if (Player.Hp <= 0 || _playerConditions.Count == 0) return;
        var resist = Math.Min(CardValue("conditionResist"), GameConfig.ConditionResistCap);
        for (var i = _playerConditions.Count - 1; i >= 0; i--)
        {
            var cond = _playerConditions[i];
            if (NowMs < cond.NextTickAtMs) continue;
            cond.NextTickAtMs += cond.TickMs;
            if (--cond.TicksLeft <= 0) _playerConditions.RemoveAt(i);

            var damage = Math.Max((int)(cond.DamagePerTick * (1 - resist)), 1);
            Player.Hp -= damage;
            _gauge = Math.Min(_gauge + damage * GameConfig.GaugeFillPerDamageTaken * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);
            Emit("damage", Player.X, Player.Y, 0, 0, damage, cond.Type, Player.Id);
            if (GameConfig.ConditionTickFx.TryGetValue(cond.Type, out var fx))
                Emit("effect", Player.X, Player.Y, 0, 0, fx);

            if (Player.Hp <= 0)
            {
                Player.Hp = 0;
                Emit("effect", Player.X, Player.Y, 0, 0, 18); // mort area
                EndRun(false, $"morta por {GameConfig.ConditionLabelPt.GetValueOrDefault(cond.Type, cond.Type)}");
                return;
            }
        }
    }

    /// <summary>Condition/slow payloads that ride on an attack that connected with the player.</summary>
    private void ApplyAttackSideEffects(Actor monster, MonsterAttack attack)
    {
        if (Player.Hp <= 0) return;
        if (attack.Condition is { } cond) ApplyConditionToPlayer(cond, monster);
        if (attack.SpeedChange < 0)
        {
            var duration = attack.DurationMs > 0 ? Math.Min(attack.DurationMs, GameConfig.SlowDurationCapMs) : GameConfig.SlowDurationCapMs;
            _playerSlowUntilMs = NowMs + duration;
            _playerSlowFactor = Math.Clamp(1 + attack.SpeedChange / GameConfig.SpeedChangeReference, GameConfig.SlowFactorFloor, 1.0);
            Emit("text", Player.X, Player.Y, 0, 0, 0, "LENTIDÃO");
        }
    }

    // ---- combat: monsters ----

    private void TickMonsters()
    {
        // indexed loop with a fixed upper bound: summons spawned this tick are appended
        // at the end of the list and only start acting next tick (stable, deterministic order)
        var count = _monsters.Count;
        for (var idx = 0; idx < count; idx++)
        {
            var monster = _monsters[idx];
            if (monster.Hp <= 0 || monster.Floor != _currentFloor) continue;
            if (monster.Dots.Count > 0) { TickMonsterDots(monster); if (monster.Hp <= 0) continue; }
            var species = monster.Species!;
            var dist = Chebyshev(monster, Player);
            var hasLos = HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y);

            // voices (tibia flavor)
            if (species.Voices.Count > 0 && NowMs >= monster.NextVoiceAtMs)
            {
                monster.NextVoiceAtMs = NowMs + GameConfig.VoiceIntervalMs + _rng.Next(6000);
                if (dist <= 9 && _rng.Chance(GameConfig.VoiceChancePercent / 100.0))
                    Emit("voice", monster.X, monster.Y, 0, 0, 0, _rng.Pick(species.Voices), monster.Id);
            }

            // acquire target (requires line of sight — no aggro through cave walls)
            if (monster.TargetId == 0 && Player.Hp > 0
                && dist <= GameConfig.MonsterAggroRange
                && hasLos)
                AcquirePlayer(monster);

            if (monster.TargetId != 0)
            {
                if (hasLos)
                    monster.LastSawPlayerAtMs = NowMs;

                if (dist > GameConfig.AggroDropRange)
                {
                    if (monster.AggroOutOfRangeSinceMs == 0)
                        monster.AggroOutOfRangeSinceMs = NowMs;
                }
                else
                {
                    monster.AggroOutOfRangeSinceMs = 0;
                }

                var tooFarTooLong = monster.AggroOutOfRangeSinceMs > 0
                                     && NowMs - monster.AggroOutOfRangeSinceMs >= GameConfig.AggroDropOutOfRangeMs;
                var unseenTooLong = !hasLos
                                    && NowMs - monster.LastSawPlayerAtMs >= GameConfig.AggroDropNoLosMs;
                if (tooFarTooLong || unseenTooLong)
                    DropAggro(monster);
            }

            if (monster.IsStunned(NowMs)) continue;

            if (monster.TargetId == 0 || Player.Hp <= 0)
            {
                Wander(monster);
                continue;
            }

            TryMonsterAttacks(monster);
            TickMonsterDefenses(monster);
            TickMonsterSummons(monster);
            TickMonsterShield(monster);

            if (monster.IsMoving(NowMs)) continue;

            // low-health flight (tibia runHealth: dragons & co. retreat while still attacking)
            if (species.RunOnHealth > 0 && monster.Hp <= species.RunOnHealth * monster.StatMult)
            {
                StepAway(monster, Player.X, Player.Y);
                continue;
            }

            // chase: move toward player keeping targetDistance for ranged species
            if (CanAttackPlayer(monster, dist, hasLos)
                && _rng.Chance(Math.Clamp(species.StaticAttackChance, 0, 100) / 100.0))
            {
                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);
                continue;
            }

            var desired = Math.Max(species.TargetDistance, 1);
            if (dist > desired)
                StepToward(monster, Player.X, Player.Y);
            else if (dist < desired && species.TargetDistance > 1 && _rng.Chance(0.5))
                StepAway(monster, Player.X, Player.Y);
            else
                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);
        }
    }

    private void AcquirePlayer(Actor monster)
    {
        monster.TargetId = Player.Id;
        monster.LastSawPlayerAtMs = NowMs;
        monster.AggroOutOfRangeSinceMs = 0;
        // Lunara — Inverno Eterno: o inimigo já entra lento ao ver Lunara.
        if (HasEcho("eternal_winter") && monster.Hp > 0 && NowMs >= monster.SlowUntilMs)
        {
            monster.SlowUntilMs = NowMs + GameConfig.EchoEternalWinterAggroSlowMs;
            monster.SlowFactor = GameConfig.EchoEternalWinterSlowFactor;
        }
    }

    private static void DropAggro(Actor monster)
    {
        monster.TargetId = 0;
        monster.LastSawPlayerAtMs = 0;
        monster.AggroOutOfRangeSinceMs = 0;
    }

    private bool CanAttackPlayer(Actor monster, int dist, bool hasLos)
    {
        foreach (var attack in monster.Species!.Attacks)
        {
            if (attack.Kind == "melee")
            {
                if (dist <= GameConfig.MeleeRange) return true;
                continue;
            }

            var range = attack.Range > 0 ? attack.Range : (attack.Radius > 0 || attack.Length > 0 ? 7 : 1);
            if (dist <= range && hasLos) return true;
        }
        return false;
    }

    private void TryMonsterAttacks(Actor monster)
    {
        var species = monster.Species!;
        for (var i = 0; i < species.Attacks.Count; i++)
        {
            var attack = species.Attacks[i];
            if (NowMs < monster.AttackReadyAtMs[i]) continue;
            var dist = Chebyshev(monster, Player);

            if (attack.IsHealing)
            {
                // support kit: heal the most wounded ally in range (the "Echo Doc" pattern)
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                var range = attack.Range > 0 ? attack.Range : 7;
                var ally = MostWoundedAlly(monster, range);
                if (ally is null) continue;
                var amount = (int)(_rng.Range(Math.Min(attack.MinDamage, attack.MaxDamage),
                    Math.Max(attack.MinDamage, attack.MaxDamage)) * monster.StatMult);
                HealMonster(ally, amount);
                if (attack.AreaEffect > 0) Emit("effect", ally.X, ally.Y, 0, 0, attack.AreaEffect);
                continue;
            }

            if (attack.Kind == "charge")
            {
                // G-08B: investida — precisa de espaço para correr (não já colado) e linha de visão.
                if (monster.IsMoving(NowMs)) continue;
                var chargeRange = attack.Range > 0 ? attack.Range : GameConfig.ChargeMaxTiles + 1;
                if (dist < 2 || dist > chargeRange) continue;
                if (!HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y)) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                ChargeAt(monster, attack);
                continue;
            }

            if (attack.Kind == "melee")
            {
                if (dist > 1) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                HitPlayerWithAttack(monster, attack);
                Emit("effect", Player.X, Player.Y, 0, 0, 1); // blood
            }
            else
            {
                var range = attack.Range > 0 ? attack.Range : (attack.Radius > 0 || attack.Length > 0 ? 7 : 1);
                if (dist > range) continue;
                if (!HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y)) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;

                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);

                if (attack.ShootEffect > 0)
                    Emit("projectile", monster.X, monster.Y, Player.X, Player.Y, attack.ShootEffect);

                if (attack.Length > 0)
                {
                    // wave (e.g. dragon fire): cone toward player
                    var (dx, dy) = DirDelta(monster.Facing, Player);
                    var hitPlayer = false;
                    foreach (var (tx, ty) in ConeTiles(monster.X, monster.Y, dx, dy, Math.Min(attack.Length, 5)))
                    {
                        if (attack.AreaEffect > 0) Emit("effect", tx, ty, 0, 0, attack.AreaEffect);
                        if (tx == Player.X && ty == Player.Y) hitPlayer = true;
                    }
                    if (hitPlayer) HitPlayerWithAttack(monster, attack);
                }
                else if (attack.Radius > 0)
                {
                    var cx = attack.Target ? Player.X : monster.X;
                    var cy = attack.Target ? Player.Y : monster.Y;
                    var hitPlayer = false;
                    foreach (var (tx, ty) in CircleTiles(cx, cy, attack.Radius))
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
        }
    }

    /// <summary>An attack connected with the player: damage (if any) + condition/slow payloads.</summary>
    private void HitPlayerWithAttack(Actor monster, MonsterAttack attack)
    {
        if (attack.MaxDamage > 0 || attack.Kind == "melee" || attack.Kind == "charge")
            DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
        ApplyAttackSideEffects(monster, attack);
    }

    /// <summary>G-08B: charger — avança em linha reta até o alvo (para a 1 tile), depois golpeia se colar.
    /// O deslocamento é um único passo "longo" que o cliente interpola como um dash. Determinístico:
    /// a chance já foi rolada pelo chamador; aqui só caminha tiles livres.</summary>
    private void ChargeAt(Actor monster, MonsterAttack attack)
    {
        var startX = monster.X;
        var startY = monster.Y;
        var cx = startX;
        var cy = startY;
        var floor = _floors[monster.Floor];
        for (var step = 0; step < GameConfig.ChargeMaxTiles; step++)
        {
            if (Chebyshev(cx, cy, Player.X, Player.Y) <= 1) break; // para a 1 tile do alvo
            var dx = Math.Sign(Player.X - cx);
            var dy = Math.Sign(Player.Y - cy);
            if (dx == 0 && dy == 0) break;
            if (dx != 0 && dy != 0 && (floor.IsBlocked(cx + dx, cy) || floor.IsBlocked(cx, cy + dy))) break;
            var nx = cx + dx;
            var ny = cy + dy;
            if (floor.IsBlocked(nx, ny)) break;
            var occ = OccupiedBy(monster.Floor, nx, ny);
            if (occ is not null && occ.Id != monster.Id) break;
            cx = nx;
            cy = ny;
        }

        if (cx != startX || cy != startY)
        {
            monster.FromX = startX;
            monster.FromY = startY;
            monster.X = cx;
            monster.Y = cy;
            monster.StepStartMs = NowMs;
            monster.StepDurMs = GameConfig.ChargeDashMs;
            monster.Facing = FacingFrom(cx - startX, cy - startY, monster.Facing);
            Emit("effect", startX, startY, 0, 0, GameConfig.ChargeFx);
            Emit("effect", cx, cy, 0, 0, GameConfig.ChargeFx);
        }

        if (Chebyshev(monster, Player) <= 1)
        {
            HitPlayerWithAttack(monster, attack);
            Emit("effect", Player.X, Player.Y, 0, 0, 1); // blood
        }
    }

    private Actor? MostWoundedAlly(Actor healer, int range)
    {
        Actor? best = null;
        var bestMissing = 0;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Floor != healer.Floor) continue;
            if (Chebyshev(m, healer) > range) continue;
            var missing = m.MaxHp - m.Hp;
            if (missing > bestMissing) { bestMissing = missing; best = m; }
        }
        return best;
    }

    private void HealMonster(Actor monster, int amount)
    {
        var cap = Math.Max((int)(monster.MaxHp * GameConfig.MonsterHealCapFraction), 1);
        var healed = Math.Min(Math.Clamp(amount, 1, cap), monster.MaxHp - monster.Hp);
        if (healed <= 0) return;
        monster.Hp += healed;
        Emit("heal", monster.X, monster.Y, 0, 0, healed, "", monster.Id);
    }

    private void TickMonsterDefenses(Actor monster)
    {
        var defenses = monster.Species!.Defenses;
        for (var i = 0; i < defenses.Count; i++)
        {
            var defense = defenses[i];
            if (NowMs < monster.DefenseReadyAtMs[i]) continue;
            monster.DefenseReadyAtMs[i] = NowMs + defense.IntervalMs;
            if (!_rng.Chance(Math.Min(defense.Chance, 100) / 100.0)) continue;

            if (defense.Kind == "healing")
            {
                if (monster.Hp >= monster.MaxHp) continue;
                var amount = (int)(_rng.Range(Math.Min(defense.MinValue, defense.MaxValue),
                    Math.Max(defense.MinValue, defense.MaxValue)) * monster.StatMult);
                HealMonster(monster, amount);
                if (defense.AreaEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, defense.AreaEffect);
            }
            else if (defense.SpeedChange > 0)
            {
                monster.HasteUntilMs = NowMs + (defense.DurationMs > 0 ? defense.DurationMs : GameConfig.DefaultHasteDurationMs);
                monster.HasteFactor = Math.Clamp(1 + defense.SpeedChange / GameConfig.SpeedChangeReference, 1.0, GameConfig.HasteFactorCap);
                if (defense.AreaEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, defense.AreaEffect);
            }
        }
    }

    private void TickMonsterSummons(Actor monster)
    {
        var species = monster.Species!;
        if (species.Summons.Count == 0) return;
        for (var i = 0; i < species.Summons.Count; i++)
        {
            var summon = species.Summons[i];
            if (NowMs < monster.SummonReadyAtMs[i]) continue;
            monster.SummonReadyAtMs[i] = NowMs + Math.Max(summon.IntervalMs, GameConfig.SummonMinIntervalMs);
            if (!_monsterRegistry.Contains(summon.Name)) continue;
            if (CountOwnedSummons(monster.Id) >= Math.Max(species.MaxSummons, 1)) continue;
            if (CountAliveSummons() >= GameConfig.MaxAliveSummons) continue;
            if (!_rng.Chance(Math.Min(summon.Chance, 100) / 100.0)) continue;

            for (var c = 0; c < Math.Max(summon.Count, 1); c++)
            {
                if (CountOwnedSummons(monster.Id) >= Math.Max(species.MaxSummons, 1)) break;
                if (CountAliveSummons() >= GameConfig.MaxAliveSummons) break;
                if (SpawnSummon(monster, summon.Name) is null) break;
            }
        }
    }

    /// <summary>G-08B: escudeiro — ergue uma barreira de eco no aliado próximo mais ferido sem escudo.
    /// Determinístico: varre _monsters em ordem estável, desempate por menor id; sem _rng.</summary>
    private void TickMonsterShield(Actor shielder)
    {
        if (GameConfig.BehaviorProfile(shielder.Species!.BehaviorId) is not { ShieldFraction: > 0 } profile) return;
        if (NowMs < shielder.ShieldCastReadyAtMs) return;
        shielder.ShieldCastReadyAtMs = NowMs + Math.Max(profile.ShieldIntervalMs, GameConfig.TickMs);

        Actor? ally = null;
        var bestMissing = -1;
        foreach (var m in _monsters)
        {
            if (m.Hp <= 0 || m.Id == shielder.Id || m.Floor != shielder.Floor) continue;
            if (m.MonsterShield > 0) continue;
            if (Chebyshev(shielder.X, shielder.Y, m.X, m.Y) > profile.ShieldRadius) continue;
            var missing = m.MaxHp - m.Hp;
            if (missing > bestMissing || (missing == bestMissing && (ally is null || m.Id < ally.Id)))
            {
                bestMissing = missing;
                ally = m;
            }
        }
        if (ally is null) return;

        var amount = Math.Max(ally.MaxHp * Math.Min(profile.ShieldFraction, GameConfig.MonsterShieldCapFraction), 1);
        ally.MonsterShield = amount;
        Emit("effect", ally.X, ally.Y, 0, 0, GameConfig.MonsterShieldFx);
    }

    private int CountOwnedSummons(int ownerId) =>
        _monsters.Count(m => m.OwnerId == ownerId && m.Hp > 0);

    private int CountAliveSummons() =>
        _monsters.Count(m => m.IsSummon && m.Hp > 0);

    private Actor? SpawnSummon(Actor owner, string speciesName)
    {
        var species = _monsterRegistry.Get(speciesName);
        var floor = _floors[owner.Floor];
        // deterministic ring scan around the summoner (radius 1 then 2, fixed order)
        for (var radius = 1; radius <= 2; radius++)
            for (var dy = -radius; dy <= radius; dy++)
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                    var x = owner.X + dx;
                    var y = owner.Y + dy;
                    if (floor.IsBlocked(x, y) || OccupiedBy(owner.Floor, x, y) is not null) continue;

                    var actor = new Actor
                    {
                        Id = _nextActorId++,
                        Species = species,
                        Floor = owner.Floor,
                        X = x, Y = y, FromX = x, FromY = y,
                        Hp = (int)(species.Health * owner.StatMult),
                        MaxHp = (int)(species.Health * owner.StatMult),
                        AttackReadyAtMs = new long[Math.Max(species.Attacks.Count, 1)],
                        DefenseReadyAtMs = new long[species.Defenses.Count],
                        SummonReadyAtMs = new long[species.Summons.Count],
                        NextWanderAtMs = _rng.Range(0, GameConfig.MonsterWanderIntervalMs),
                        StatMult = owner.StatMult,
                        OwnerId = owner.Id,
                        Facing = (Dir)_rng.Next(4)
                    };
                    AcquirePlayer(actor);
                    _monsters.Add(actor);
                    Emit("effect", x, y, 0, 0, 11); // teleport poof
                    return actor;
                }
        return null;
    }

    private int RollMonsterDamage(Actor monster, MonsterAttack attack)
    {
        var min = Math.Min(attack.MinDamage, attack.MaxDamage);
        var max = Math.Max(attack.MinDamage, attack.MaxDamage);
        var roll = _rng.Range(min, max) * monster.StatMult;
        var tuning = monster.Species?.IsAuthored == true ? 1 : GameConfig.MonsterDamageTuning;
        return Math.Max((int)(roll * tuning), 1);
    }

    private void DamagePlayer(int damage, string damageType, Actor source)
    {
        if (Player.Hp <= 0) return;
        var traitReduction = _trait.Kind switch
        {
            "fortress" => _trait.Value * _traitMult,
            "bulwark" when Player.Hp < Player.MaxHp * _trait.Param => _trait.Value * _traitMult,
            _ => 0
        };
        // Seren — Postura Imortal: com o combo alto, a Postura do Zênite corta o dano recebido.
        var immortal = HasEcho("immortal_stance") && NowMs <= _comboExpireMs
                       && _comboHits >= GameConfig.EchoImmortalComboThreshold
            ? GameConfig.EchoImmortalDamageReduction : 0;
        var reduction = Math.Min(
            CardValue("damageReduction") + EquipmentStats.DamageReduction
            + Loadout.Mastery.DamageReductionBonus + traitReduction + immortal,
            GameConfig.PlayerDamageReductionCap);
        var resistance = EquipmentStats.Resistance(damageType);
        var final = damage * (1 - reduction) * (1 - resistance);
        if (NowMs < source.SappedUntilMs)
            final *= GameConfig.SappedStrengthDamageMultiplier;
        if (IsBuffActive("shield")) final *= 0.5;
        var value = Math.Max((int)final, 1);

        // Eloa Mártir / Velvet Pacto: o escudo de Eco absorve antes da vida.
        value = AbsorbWithEchoShield(value);
        if (value <= 0)
        {
            Emit("text", Player.X, Player.Y, 0, 0, 0, "ESCUDO");
            return;
        }

        Player.Hp -= value;
        _gauge = Math.Min(_gauge + value * GameConfig.GaugeFillPerDamageTaken * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);
        Emit("damage", Player.X, Player.Y, 0, 0, value, damageType, Player.Id);

        if (Player.Hp <= 0)
        {
            Player.Hp = 0;
            Emit("effect", Player.X, Player.Y, 0, 0, 18); // mort area
            EndRun(false, $"morta por {source.Species?.Name ?? "?"}");
        }
    }

    private void Wander(Actor monster)
    {
        if (monster.IsMoving(NowMs) || NowMs < monster.NextWanderAtMs) return;
        monster.NextWanderAtMs = NowMs + GameConfig.MonsterWanderIntervalMs + _rng.Next(2000);
        if (!_rng.Chance(0.4)) return;
        var dx = _rng.Range(-1, 1);
        var dy = _rng.Range(-1, 1);
        TryStep(monster, dx, dy, MonsterSpeed(monster));
    }

    private void StepToward(Actor monster, int tx, int ty)
    {
        var dx = Math.Sign(tx - monster.X);
        var dy = Math.Sign(ty - monster.Y);
        var speed = MonsterSpeed(monster);
        var forward = new List<(int Dx, int Dy)>(3);
        AddStepCandidate(forward, dx, dy);
        AddStepCandidate(forward, dx, 0);
        AddStepCandidate(forward, 0, dy);
        if (TryBestMonsterStep(monster, tx, ty, speed, forward)) return;

        var lateral = new List<(int Dx, int Dy)>(2);
        if (_rng.Next(2) == 0)
        {
            AddStepCandidate(lateral, -dy, dx);
            AddStepCandidate(lateral, dy, -dx);
        }
        else
        {
            AddStepCandidate(lateral, dy, -dx);
            AddStepCandidate(lateral, -dy, dx);
        }
        TryBestMonsterStep(monster, tx, ty, speed, lateral);
    }

    private static void AddStepCandidate(List<(int Dx, int Dy)> candidates, int dx, int dy)
    {
        if ((dx != 0 || dy != 0) && !candidates.Contains((dx, dy)))
            candidates.Add((dx, dy));
    }

    private bool TryBestMonsterStep(Actor monster, int tx, int ty, int speed,
        List<(int Dx, int Dy)> candidates)
    {
        var open = candidates
            .Where(c => CanStep(monster, c.Dx, c.Dy))
            .Select(c => new
            {
                Step = c,
                Distance = Chebyshev(monster.X + c.Dx, monster.Y + c.Dy, tx, ty),
                Neighbors = AdjacentLivingMonsters(monster, monster.X + c.Dx, monster.Y + c.Dy)
            })
            .ToList();
        if (open.Count == 0) return false;

        var bestDistance = open.Min(c => c.Distance);
        var sameDistance = open.Where(c => c.Distance == bestDistance).ToList();
        var leastCrowded = sameDistance.Min(c => c.Neighbors);
        var choice = sameDistance.First(c => c.Neighbors == leastCrowded);
        return TryStep(monster, choice.Step.Dx, choice.Step.Dy, speed);
    }

    private int AdjacentLivingMonsters(Actor actor, int x, int y) =>
        _monsters.Count(m => m.Id != actor.Id && m.Hp > 0 && m.Floor == actor.Floor
                             && Chebyshev(m.X, m.Y, x, y) <= 1);

    private void StepAway(Actor monster, int tx, int ty)
    {
        var dx = -Math.Sign(tx - monster.X);
        var dy = -Math.Sign(ty - monster.Y);
        TryStep(monster, dx, dy, MonsterSpeed(monster));
    }

    // ---- pickup / POIs ----

    private void AddLootedItem(int itemId, string name, int count)
    {
        var existing = ItemsLooted.FirstOrDefault(item => item.ItemId == itemId);
        if (existing is null)
        {
            ItemsLooted.Add(new RewardItemDto(itemId, name, count));
            return;
        }

        ItemsLooted.Remove(existing);
        ItemsLooted.Add(existing with { Count = existing.Count + count });
    }

    private void TryInteract(int x, int y)
    {
        if (Player.Hp <= 0 || Chebyshev(Player.X, Player.Y, x, y) > 1) return;
        var poi = _pois.FirstOrDefault(p => p.Floor == _currentFloor && p.X == x && p.Y == y && !p.Used);
        if (poi is null) return;

        if (poi.Kind == "ladder")
        {
            DescendToFloor(1);
            return;
        }

        if (poi.Kind == "sanctuary")
        {
            // G-06: altar do Santuário de Eco — beat de escolha garantido (sinalizado no minimapa).
            poi.Used = true;
            Emit("effect", x, y, 0, 0, 49); // holy/energy burst
            Emit("text", x, y, 0, 0, 0, "SANTUÁRIO DE ECO");
            OfferCardBeat();
            return;
        }

        // G-09: baú = altar de Eco / loja da run. Variantes: mímico (luta), amaldiçoado (ganância) e comum.
        poi.Used = true;
        _chestsOpened++;

        if (poi.Variant == "mimic")
        {
            OpenMimic(x, y);
            return;
        }

        Emit("effect", x, y, 0, 0, 3); // poff
        var cursed = poi.Variant == "cursed";
        if (cursed) ApplyChestCurse(x, y);

        // loot do baú (mantém o baú gratificante além da carta do altar)
        GrantChestLoot(x, y);

        // material de Eco (cresce a conta): amaldiçoado garante N, comum por chance
        if (cursed)
            for (var i = 0; i < GameConfig.CursedChestMaterialDrops; i++) GrantGearMaterial(x, y);
        else if (_rng.Chance(GameConfig.ChestMaterialDropChance))
            GrantGearMaterial(x, y);

        // altar: abre uma oferta de carta (overlay reusa reroll/banir/loja). Amaldiçoado = oferta abençoada.
        OfferCardBeat(blessed: cursed);
    }

    /// <summary>G-09: loot bruto do baú (ouro + itens equipáveis do tier), voando até o jogador.</summary>
    private void GrantChestLoot(int x, int y)
    {
        var gold = (long)(_rng.Range(40, 120) * Tier.StatMultiplier * (1 + CardValue("goldPercent")));
        _gold += gold;
        EmitLootFly(GameConfig.GoldCoinItemId, $"+{gold} gold", x, y, isGold: true);
        Emit("effect", x, y, 0, 0, 29); // fireworks

        if (Tier.CommonMobs.Concat(Tier.EliteMobs).All(name => _monsterRegistry.Get(name).IsAuthored))
        {
            for (var i = 0; i < GameConfig.KaezanChestItemDrops; i++)
                if (TryPickKaezanDropItem("chest", relicOnly: false, out var item))
                    CollectLoot(item.ItemId, item.Name, 1, x, y);
        }
        else
        {
            var lootPool = Tier.CommonMobs.Concat(Tier.EliteMobs)
                .SelectMany(name => _monsterRegistry.Get(name).Loot)
                .Where(l => _data.IsEquippableLoot(l.ItemId))
                .ToList();
            for (var i = 0; i < 2 && lootPool.Count > 0; i++)
            {
                var entry = _rng.Pick(lootPool);
                CollectLoot(entry.ItemId, entry.Name, 1, x, y);
            }
        }
    }

    /// <summary>G-09: material de Eco do tier (cresce a conta, alimenta a tela de equipamento da Kaeli).</summary>
    private void GrantGearMaterial(int x, int y)
    {
        var name = GameConfig.GearMaterialName(Tier.Tier);
        AddLootedItem(GameConfig.GearMaterialItemId(Tier.Tier), name, 1);
        EmitLootFly(GameConfig.GearMaterialFlySpriteId, name, x, y, isGold: false);
    }

    /// <summary>G-09: emboscada de comuns no baú (custo de risco). Determinístico via _rng.</summary>
    private void SpawnChestAmbush(int x, int y, int count)
    {
        var room = Floor.Rooms.FirstOrDefault(r => r.Contains(x, y)) ?? Floor.Rooms[0];
        for (var i = 0; i < count; i++)
        {
            var mob = SpawnMonster(_currentFloor, _rng.Pick(Tier.CommonMobs), room);
            if (mob is not null)
            {
                AcquirePlayer(mob);
                Emit("effect", mob.X, mob.Y, 0, 0, 11); // teleport
            }
        }
    }

    /// <summary>G-09: baú amaldiçoado — emboscada + maldição (lentidão) no jogador. Ganância vs. segurança.</summary>
    private void ApplyChestCurse(int x, int y)
    {
        Emit("effect", x, y, 0, 0, 18); // mort area
        Emit("text", x, y, 0, 0, 0, "BAÚ AMALDIÇOADO!");
        SpawnChestAmbush(x, y, GameConfig.CursedChestAmbush);
        _playerSlowUntilMs = NowMs + GameConfig.CursedChestSlowMs;
        _playerSlowFactor = GameConfig.CursedChestSlowFactor;
    }

    /// <summary>G-09: mímico — baú-Eco corrompido que nasce em cima do baú, reforçado, e parte pra cima.</summary>
    private void OpenMimic(int x, int y)
    {
        Emit("effect", x, y, 0, 0, 11); // teleport
        Emit("text", x, y, 0, 0, 0, "MÍMICO!");
        var room = Floor.Rooms.FirstOrDefault(r => r.Contains(x, y)) ?? Floor.Rooms[0];
        var mimic = SpawnMonster(_currentFloor, _rng.Pick(Tier.EliteMobs), room, isElite: true);
        if (mimic is null) return;
        mimic.X = mimic.FromX = x;
        mimic.Y = mimic.FromY = y;
        mimic.MaxHp = mimic.Hp = (int)(mimic.Hp * GameConfig.MimicHpScale);
        mimic.IsMimic = true;
        AcquirePlayer(mimic);
    }

    private void DescendToFloor(int floorIndex)
    {
        _currentFloor = floorIndex;
        Player.Floor = floorIndex;
        _floorEnteredMs = NowMs; // G-10: re-arma o atraso do auto-loot ao chegar no novo andar
        var entry = _floors[floorIndex].Entry;
        Player.X = entry.X; Player.Y = entry.Y;
        Player.FromX = entry.X; Player.FromY = entry.Y;
        Player.StepDurMs = 0;
        Player.TargetId = 0;
        _manualTargetId = 0;
        MapDirty = true;
        Emit("effect", entry.X, entry.Y, 0, 0, 11);

        // G-06: limpar um andar é um beat — concede uma escolha (milestone de progressão antecipável).
        if (GameConfig.OfferChoiceOnFloorClear) OfferCardBeat();
    }

    // ---- run end ----

    internal void EndRun(bool victory, string reason)
    {
        if (Ended is not null) return;
        var goldEarned = victory ? _gold : (long)(_gold * GameConfig.DefeatGoldKeptFraction);
        var kaeros = victory ? GameConfig.VictoryKaerosBase + GameConfig.VictoryKaerosPerTier * (Tier.Tier - 1) : 0;
        var accountXp = (victory ? GameConfig.AccountXpPerVictory : GameConfig.AccountXpPerDefeat)
                        + GameConfig.AccountXpPerRunLevel * _runLevel;

        Ended = new RunEndDto(
            victory, reason, goldEarned, accountXp, kaeros,
            _kills, _runLevel, NowMs,
            ItemsLooted.ToList(), []);
    }

    // ---- geometry helpers ----

    /// <summary>Bresenham walk over blocked tiles (endpoints excluded).</summary>
    private bool HasLineOfSight(int x0, int y0, int x1, int y1)
    {
        var floor = Floor;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        int x = x0, y = y0;
        while (x != x1 || y != y1)
        {
            var px = x;
            var py = y;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            if (DiagonalCornerBlocked(floor, px, py, x, y)) return false;
            if ((x != x1 || y != y1) && floor.IsBlocked(x, y)) return false;
        }
        return true;
    }

    /// <summary>
    /// Anda a reta player→(tx,ty) e devolve o tile mais distante alcançável: limitado a
    /// <paramref name="maxRange"/> (Chebyshev) e parando antes de qualquer parede. Devolve o tile
    /// do alvo se ele estiver no alcance e visível. Usado para a magia "disparar na reta da Kaeli".
    /// </summary>
    private (int x, int y) AimAlongLine(int tx, int ty, int maxRange)
    {
        var floor = Floor;
        int x0 = Player.X, y0 = Player.Y;
        int dx = Math.Abs(tx - x0), dy = Math.Abs(ty - y0);
        int sx = x0 < tx ? 1 : -1, sy = y0 < ty ? 1 : -1;
        var err = dx - dy;
        int x = x0, y = y0, lastX = x0, lastY = y0;
        while (x != tx || y != ty)
        {
            var px = x;
            var py = y;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            if (DiagonalCornerBlocked(floor, px, py, x, y)) break;
            if (floor.IsBlocked(x, y)) break;
            if (Chebyshev(x0, y0, x, y) > maxRange) break;
            lastX = x; lastY = y;
        }
        return (lastX, lastY);
    }

    private static bool DiagonalCornerBlocked(DungeonFloor floor, int fromX, int fromY, int toX, int toY)
    {
        var dx = Math.Sign(toX - fromX);
        var dy = Math.Sign(toY - fromY);
        return dx != 0 && dy != 0
            && (floor.IsBlocked(fromX + dx, fromY)
                || floor.IsBlocked(fromX, fromY + dy));
    }

    private static int Chebyshev(Actor a, Actor b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static int Chebyshev(int x1, int y1, int x2, int y2) => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    private Actor? MonsterAt(int x, int y) =>
        _monsters.FirstOrDefault(m => m.Floor == _currentFloor && m.Hp > 0 && m.X == x && m.Y == y);

    private Actor? NearestMonster(int maxRange)
    {
        Actor? best = null;
        var bestDist = int.MaxValue;
        foreach (var m in _monsters)
        {
            if (!IsTargetableByPlayer(m, maxRange)) continue;
            var d = Chebyshev(m, Player);
            if (d <= maxRange && d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    private (int dx, int dy) DirDelta(Dir facing, Actor? target)
    {
        if (target is not null)
        {
            var dx = Math.Sign(target.X - Player.X);
            var dy = Math.Sign(target.Y - Player.Y);
            if (dx != 0 || dy != 0) return (dx, dy);
        }
        return facing switch
        {
            Dir.North => (0, -1),
            Dir.East => (1, 0),
            Dir.South => (0, 1),
            _ => (-1, 0)
        };
    }

    private IEnumerable<(int X, int Y)> CircleTiles(int cx, int cy, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var x = cx + dx;
                var y = cy + dy;
                if (Math.Abs(dx) + Math.Abs(dy) > radius * GameConfig.AoeRoundingFactor) continue; // rounded diamond (MG-04)
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
    }

    private IEnumerable<(int X, int Y)> ConeTiles(int ox, int oy, int dx, int dy, int reach)
    {
        // leque frontal simétrico com a MESMA abertura angular em qualquer das 8 direções.
        // forward = projeção na direção do alvo; perp = componente perpendicular ao eixo.
        // O cone independe de o alvo estar em diagonal ou em linha reta.
        for (var ry = -reach; ry <= reach; ry++)
            for (var rx = -reach; rx <= reach; rx++)
            {
                if (rx == 0 && ry == 0) continue;
                if (Math.Max(Math.Abs(rx), Math.Abs(ry)) > reach) continue;
                var forward = rx * dx + ry * dy;
                if (forward <= 0) continue;                 // só tiles à frente
                var perp = Math.Abs(rx * dy - ry * dx);
                if (perp > forward) continue;               // meia-abertura de 45° (cone de 90°)
                var x = ox + rx;
                var y = oy + ry;
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
    }

    private void Emit(string kind, int x, int y, int toX = 0, int toY = 0, int value = 0,
        string text = "", int actorId = 0, bool crit = false) =>
        _events.Add(new EventDto(kind, x, y, toX, toY, value, text, actorId, crit));

    // ---- snapshot ----

    private MapDto BuildMap()
    {
        var floor = Floor;
        var atmo = Biomes.ForTier(Tier.Tier).Atmosphere;
        return new MapDto(
            _currentFloor, floor.W, floor.H,
            floor.Ground, floor.Wall, floor.Decor, floor.Blocked,
            floor.Entry.X, floor.Entry.Y,
            floor.LadderDown?.X, floor.LadderDown?.Y,
            _pois.Where(p => p.Floor == _currentFloor)
                // G-09: mímico viaja como variante vazia (só "cursed" é telegrafado pro cliente).
                .Select(p => new PoiDto(p.Id, p.Kind, p.X, p.Y, PoiSpriteId(p.Kind),
                    p.Variant == "cursed" ? "cursed" : "", p.Used))
                .ToList(),
            // G-07: tipos de sala (ícones do minimapa) + paleta cosmética do estrato.
            floor.Rooms.Select(r => new RoomDto(r.X, r.Y, r.W, r.H, r.Role)).ToList(),
            new BiomeDto(
                atmo.Name,
                atmo.TintR, atmo.TintG, atmo.TintB, atmo.TintStrength,
                atmo.FogR, atmo.FogG, atmo.FogB, atmo.FogStrength,
                atmo.Vignette,
                atmo.ParticleR, atmo.ParticleG, atmo.ParticleB, atmo.ParticleDensity, atmo.ParticleDrift));
    }

    private static ushort PoiSpriteId(string kind) => kind switch
    {
        "chest" => DungeonGenerator.ChestId,
        "sanctuary" => DungeonGenerator.SanctuaryId,
        _ => DungeonGenerator.LadderDownId,
    };

    private List<string> BuildActiveConditions()
    {
        var conditions = _playerConditions.Select(c => c.Type).ToList();
        if (NowMs < _playerSlowUntilMs) conditions.Add("slow");
        return conditions;
    }

    /// <summary>K-04: estado vivo da passiva assinatura (lado do jogador) para o HUD.</summary>
    private TraitStateDto BuildTraitState()
    {
        var kind = _trait.Kind;
        var name = _trait.Name;
        switch (kind)
        {
            case "discipline": // Seren — combo no mesmo alvo
            {
                var hits = NowMs <= _comboExpireMs ? _comboHits : 0;
                var steps = _trait.Value > 0 ? Math.Ceiling(_trait.Param / _trait.Value) : 0;
                var bonus = Math.Min(hits * _trait.Value, _trait.Param) * _traitMult;
                return new TraitStateDto(kind, name, hits, steps,
                    hits > 0 ? $"x{hits} (+{Math.Round(bonus * 100)}%)" : "—");
            }
            case "static_charge": // Rynna — barra de carga
                return new TraitStateDto(kind, name, Math.Round(_staticCharge), GameConfig.RynnaChargeMax,
                    $"{Math.Round(_staticCharge)}/{GameConfig.RynnaChargeMax:0}");
            case "contagion": // Rin — inimigos em chamas
            {
                var burning = _monsters.Count(m => m.Hp > 0 && m.Floor == _currentFloor && IsBurning(m));
                return new TraitStateDto(kind, name, burning, 0, burning > 0 ? $"{burning} em chamas" : "—");
            }
            case "prey": // Gaia — ramp de caça contra a Presa
            {
                var prey = _monsters.FirstOrDefault(m => m.Id == _preyId && m.Hp > 0);
                if (prey is null) return new TraitStateDto(kind, name, 0, 0, "sem presa");
                var ramp = Math.Min((NowMs - _preyStartMs) / 1000.0 * _trait.Value, _trait.Param) * _traitMult;
                return new TraitStateDto(kind, name, Math.Round(ramp * 100), 0, $"+{Math.Round(ramp * 100)}%");
            }
            case "shatter": // Lunara — haste do Estilhaçar
                return new TraitStateDto(kind, name, 0, 0, NowMs < _traitHasteUntilMs ? "HASTE" : "—");
            default: // judgment / decay: o estado vivo aparece como marca por-alvo
                return new TraitStateDto(kind, name, 0, 0, "");
        }
    }

    /// <summary>K-04: marca por-alvo (stacks/tag) que o render desenha sobre o monstro.</summary>
    private (int Stacks, string Tag) MonsterTraitState(Actor m)
    {
        switch (_trait.Kind)
        {
            case "judgment":
                var sin = NowMs < m.SinUntilMs ? m.SinStacks : 0;
                return (sin, sin >= GameConfig.EloaSinStacksToJudge ? "judged" : "");
            case "decay":
                return (NowMs < m.DecayUntilMs ? m.DecayStacks : 0, "");
            case "shatter":
                var slowed = NowMs < m.SlowUntilMs;
                return (slowed ? m.FrostHits : 0, slowed ? "frozen" : "");
            case "prey":
                return (0, m.IsPrey ? "prey" : "");
            default:
                return (0, "");
        }
    }

    private SnapshotDto BuildSnapshot()
    {
        var skillBar = CurrentSkillBar();
        var skills = new List<SkillStateDto>(5);
        for (var i = 0; i < skillBar.Length; i++)
        {
            var skill = skillBar[i];
            var isUlt = i == 4;
            var remaining = isUlt ? 0 : Math.Max(_skillReadyAtMs[i] - NowMs, 0);
            var ready = isUlt ? _gauge >= GameConfig.UltimateGaugeMax : remaining == 0;
            var cooldownTotal = isUlt
                ? skill.CooldownMs
                : (int)(skill.CooldownMs
                        * Loadout.Mastery.CooldownMult
                        * (1 - EquipmentStats.CooldownReduction));
            skills.Add(new SkillStateDto(
                skill.Id, skill.Name, skill.Element, skill.Description,
                remaining, cooldownTotal, ready));
        }

        var boss = _monsters.FirstOrDefault(m => m.IsBossActor && m.Floor == _currentFloor);

        var player = new PlayerDto(
            Player.Id, Player.X, Player.Y, (int)Player.Facing, Player.Hp, Player.MaxHp,
            Player.FromX, Player.FromY, Player.StepDurMs, Player.StepStartMs,
            new OutfitDto(Loadout.Skin.LookType, Loadout.Skin.Head, Loadout.Skin.Body,
                Loadout.Skin.Legs, Loadout.Skin.Feet,
                // os addons vêm exclusivamente da skin escolhida (0 = nenhum); ascensão não os força
                Loadout.Skin.Addons,
                Loadout.Skin.MountLookType > 0 ? Loadout.Skin.MountLookType : EquipmentStats.MountLookType),
            Player.TargetId, _gauge, skills,
            PlayerClass.Id, PlayerClass.Name,
            CurrentStance.Id, CurrentStance.Name, CurrentStance.Element, PlayerClass.CanToggleStance,
            Math.Max(_autoAttackReadyAtMs - NowMs, 0),
            new AutoHelperSettingsDto(
                _autoHelperTargeting, _autoHelperSkills, _autoHelperUltimate,
                _autoHelperTargetPreference, _autoHelperMovementMode, _defaultAutoHelperMovementMode,
                _autoHelperAutoHeal, _autoHelperHealPct, _autoHelperNavMode, _autoHelperAutoCards),
            _buffsUntilMs.Where(b => NowMs < b.Value).Select(b => b.Key).ToList(),
            BuildActiveConditions(),
            new EquipmentStatsDto(
                EquipmentStats.AttackBonus,
                EquipmentStats.MaxHpBonus,
                EquipmentStats.DamageReduction,
                EquipmentStats.MoveSpeedPercent,
                EquipmentStats.SkillPowerMultiplier,
                EquipmentStats.CritChance,
                EquipmentStats.CritDamage,
                EquipmentStats.CooldownReduction),
            _potionCharges, GameConfig.PotionChargesPerRun, GameConfig.PotionSlotItemId(Tier.Tier),
            Math.Max(_potionReadyAtMs - NowMs, 0), GameConfig.PotionCooldownMs,
            GameConfig.PotionSlotHealFraction(Tier.Tier),
            BuildTraitState());

        var monsters = _monsters
            .Where(m => m.Hp > 0 && m.Floor == _currentFloor)
            .Select(m =>
            {
                var (traitStacks, traitTag) = MonsterTraitState(m);
                return new MonsterDto(
                    m.Id, m.Species!.Name, m.X, m.Y, (int)m.Facing, m.Hp, m.MaxHp,
                    m.FromX, m.FromY, m.StepDurMs, m.StepStartMs,
                    new OutfitDto(m.Species.Outfit.LookType, m.Species.Outfit.Head, m.Species.Outfit.Body,
                        m.Species.Outfit.Legs, m.Species.Outfit.Feet, m.Species.Outfit.Addons),
                    m.IsBossActor, m.IsStunned(NowMs), m.ActiveMark(NowMs),
                    traitStacks, traitTag);
            })
            .ToList();

        var items = _groundItems
            .Where(i => i.Floor == _currentFloor)
            .Select(i => new GroundItemDto(i.Id, i.X, i.Y, i.ItemId, i.Count))
            .ToList();

        var run = new RunStateDto(
            Tier.Tier, Tier.Name, Seed,
            _runLevel, _runXp, GameConfig.XpForRunLevel(_runLevel),
            _gold, _kills,
            _cards.Select(c =>
            {
                var def = Cards.ById[c.Key];
                return new CardStackDto(c.Key, def.Name, c.Value, def.Rarity, def.TagList);
            }).ToList(),
            _pendingOffer,
            _cardRerollsRemaining, _bannedCards.Count, GameConfig.CardRerollGoldCost,
            boss?.Hp, boss?.MaxHp, boss?.Species!.Name,
            boss is { PostureMax: > 0 } ? boss.Posture : null,
            boss is { PostureMax: > 0 } ? boss.PostureMax : null,
            boss is not null && boss.IsStaggered(NowMs),
            boss?.PostureCycle ?? 0,
            NowMs, Ended,
            ItemsLooted.ToList(),
            CurrentNavTargetDto());

        return new SnapshotDto(TickCount, NowMs, _currentFloor, player, monsters, items, _events.ToList(), run);
    }

    public void RequestMapRefresh() => MapDirty = true;
}
