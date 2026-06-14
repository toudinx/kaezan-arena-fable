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
    public double StatMult = 1.0;

    // monster kit (T-53): reactive defenses, summon timers, self-haste
    public long[] DefenseReadyAtMs = [];
    public long[] SummonReadyAtMs = [];
    public int OwnerId; // > 0 when this actor was summoned by another monster
    public long HasteUntilMs;
    public double HasteFactor = 1.0;

    // slow applied by the player (trait chiller)
    public long SlowUntilMs;
    public double SlowFactor = 1.0;

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
    public string Kind = "chest"; // chest | ladder
    public int Floor, X, Y;
    public bool Used;
}

public enum CommandKind { SetMoveDir, SetTarget, CastSkill, ToggleStance, Interact, ChooseCard, Abandon }

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
    private readonly MonsterRegistry _monsterRegistry;
    private readonly Rng _rng;
    private readonly IReadOnlyDictionary<string, long> _bestiaryKills;

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
    private List<CardOfferDto>? _pendingOffer;
    private long _cardOfferStartedTick;
    private int _queuedOffers;
    public Dictionary<string, int> KillsBySpecies { get; } = [];
    public List<RewardItemDto> ItemsLooted { get; } = [];
    private int _chestsOpened;
    public int ChestsOpened => _chestsOpened;

    // player combat state
    private readonly long[] _skillReadyAtMs = new long[4];
    private string _stanceId;
    private long _autoAttackReadyAtMs;
    private int _moveDirX, _moveDirY; // held movement direction (-1..1)
    private int _bufferedMoveDirX, _bufferedMoveDirY;
    private bool _hasBufferedMoveDir;
    private long _moveDirChangedAtMs;
    private readonly Dictionary<string, long> _buffsUntilMs = [];
    private double _regenCarry;
    private readonly List<ActiveCondition> _playerConditions = [];
    private long _playerSlowUntilMs;
    private double _playerSlowFactor = 1.0;

    public RunEndDto? Ended { get; private set; }
    public bool MapDirty { get; private set; } = true;

    public GameWorld(long seed, DungeonTier tier, WaifuDef waifu, int ascension,
        GameData data, MonsterRegistry monsterRegistry, IReadOnlyDictionary<string, long> bestiaryKills,
        EquipmentStats? equipmentStats = null, KaeliLoadout? loadout = null)
    {
        Seed = seed;
        Tier = tier;
        Waifu = waifu;
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
                     * (_trait.Kind == "overcharge" ? 1 + _trait.Value * _traitMult : 1);
        _data = data;
        _monsterRegistry = monsterRegistry;
        _bestiaryKills = bestiaryKills;
        _rng = new Rng((ulong)seed);

        _floors = [DungeonGenerator.Generate(_rng, 0, false), DungeonGenerator.Generate(_rng, 1, true)];

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

        SpawnFloorMonsters(0);
        SpawnFloorMonsters(1);
        SpawnPois();
    }

    // ================= spawn =================

    private void SpawnFloorMonsters(int floorIndex)
    {
        var floor = _floors[floorIndex];
        foreach (var room in floor.Rooms)
        {
            switch (room.Role)
            {
                case "entry": continue;
                case "boss":
                    SpawnBossRoom(floorIndex, room);
                    continue;
            }

            // echo-spots style budget spawn: commons cost 2, elites cost 5
            var sizeFactor = room.W * room.H / 49.0;
            var budget = (int)(GameConfig.SpawnBudgetBase * (1 + (Tier.Tier - 1) * GameConfig.SpawnBudgetTierGrowth) * Math.Clamp(sizeFactor, 0.6, 1.4));
            if (room.Role == "treasure") budget = budget / 2 + 2;
            var guard = 0;
            while (budget > 0 && guard++ < 50)
            {
                var elite = budget >= 5 && _rng.Chance(0.25);
                var name = elite ? _rng.Pick(Tier.EliteMobs) : _rng.Pick(Tier.CommonMobs);
                if (SpawnMonster(floorIndex, name, room) is not null)
                    budget -= elite ? 5 : 2;
                else break;
            }
        }
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

    private Actor? SpawnMonster(int floorIndex, string speciesName, Room room, bool isBoss = false)
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
                StatMult = mult,
                Facing = (Dir)_rng.Next(4)
            };
            _monsters.Add(actor);
            return actor;
        }
        return null;
    }

    private void SpawnPois()
    {
        var nextPoi = 1;
        for (var f = 0; f < _floors.Length; f++)
        {
            foreach (var (cx, cy) in _floors[f].Chests)
                _pois.Add(new Poi { Id = nextPoi++, Kind = "chest", Floor = f, X = cx, Y = cy });
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

            if (cardPause && cmd.Kind is not (CommandKind.SetMoveDir or CommandKind.ChooseCard or CommandKind.Abandon))
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
                Player.TargetId = _monsters.Any(m => m.Id == cmd.A && m.Hp > 0) ? cmd.A : 0;
                break;
            case CommandKind.CastSkill:
                TryCastSkill(cmd.A);
                break;
            case CommandKind.ToggleStance:
                ToggleStance();
                break;
            case CommandKind.Interact:
                TryInteract(cmd.A, cmd.B);
                break;
            case CommandKind.ChooseCard:
                ChooseCard(cmd.S ?? "");
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

            if (Ended is null && _pendingOffer is not null
                && (TickCount - _cardOfferStartedTick) * GameConfig.TickMs >= GameConfig.CardOfferTimeoutMs)
                ChooseCard(_pendingOffer[0].Id);

            if (Ended is null && _pendingOffer is null)
            {
                if (cardPauseAtStart)
                    _simulationMs += GameConfig.TickMs;

                TickPlayerMovement();
                TickPlayerCombat();

                if (_pendingOffer is null)
                {
                    TickPlayerRegen();
                    TickPlayerConditions();
                    TickMonsters();
                    TickPostureDecay();
                    TickPickup();
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

    private static Dir FacingFrom(int dx, int dy) =>
        Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? Dir.East : Dir.West) : (dy >= 0 ? Dir.South : Dir.North);

    private bool CanStep(Actor actor, int dx, int dy)
    {
        if (dx == 0 && dy == 0) return false;
        var nx = actor.X + dx;
        var ny = actor.Y + dy;
        var floor = _floors[actor.Floor];
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
        actor.Facing = FacingFrom(dx, dy);
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
        var crit = GameConfig.CritChance + CardValue("critChance") + Loadout.Mastery.CritChanceBonus;
        if (IsBuffActive("crit")) crit += 0.20;
        return crit;
    }

    private long AutoAttackInterval()
    {
        var interval = GameConfig.PlayerAutoAttackMs / (1 + CardValue("atkSpeedPercent"));
        if (IsBuffActive("atkspeed")) interval /= 1.40;
        if (IsBuffActive("aegis")) interval /= GameConfig.SentinelAegisAttackSpeedMultiplier;
        return (long)Math.Max(interval, 400);
    }

    // ---- combat: player ----

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

        var range = Waifus.WeaponRange(Waifu.Weapon);
        if (Chebyshev(Player, target) > range) return;
        // ranged auto-attacks respect walls, same as monsters (GameWorld:1293)
        if (range > GameConfig.MeleeRange && !HasLineOfSight(Player.X, Player.Y, target.X, target.Y)) return;

        _autoAttackReadyAtMs = NowMs + AutoAttackInterval();
        Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        var missile = Waifus.WeaponMissile(Waifu.Weapon, Waifu.Element);
        if (missile > 0)
            Emit("projectile", Player.X, Player.Y, target.X, target.Y, missile);

        DealDamageToMonster(target, PlayerAttack(), Waifu.Element, hitEffect: missile > 0 ? 0 : 216);
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
                     ?? NearestMonster(skill.Range > 0 ? skill.Range : 7);

        // skills que precisam de alvo só exigem QUE EXISTA um alvo travado/próximo — não que ele
        // esteja em alcance. Se estiver longe, a magia dispara na reta da Kaeli até o limite/parede.
        if (skill.Shape is "single" or "area" && target is null) return;

        // ponto de mira: o alvo travado, mas limitado ao alcance da skill e parando antes da parede
        var aimX = target?.X ?? Player.X;
        var aimY = target?.Y ?? Player.Y;
        if (skill.Shape is "single" or "area" && target is not null)
        {
            (aimX, aimY) = AimAlongLine(target.X, target.Y, skill.Range);
            if (aimX == Player.X && aimY == Player.Y) return; // parede colada: nem sai (não gasta CD)
        }

        if (isUlt) _gauge = 0;
        else _skillReadyAtMs[slot] = NowMs + (long)(skill.CooldownMs * Loadout.Mastery.CooldownMult);

        Emit("skill_cast", Player.X, Player.Y, 0, 0, 0, skill.Name);
        if (target is not null)
            Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        // maestria: slots 1-4 multiplicam o Power; a ultimate amplifica duração/cura (ultmod)
        var ultScale = isUlt ? Loadout.Mastery.UltimatePowerMult : 1.0;
        var damage = PlayerAttack() * skill.Power * (isUlt ? 1.0 : Loadout.Mastery.SlotPowerMult(slot));
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
                foreach (var (tx, ty) in CircleTiles(aimX, aimY, skill.Radius))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
                break;
            }

            case "nova":
            {
                foreach (var (tx, ty) in CircleTiles(Player.X, Player.Y, skill.Radius))
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
                var reach = Math.Max(skill.Radius, 1);
                foreach (var (tx, ty) in ConeTiles(Player.X, Player.Y, dx, dy, reach))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, ultScale);
                }
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
    }

    private void DealDamageToMonster(Actor monster, double raw, string element, int hitEffect,
        bool fromSkill = false)
    {
        var roll = raw * (GameConfig.DamageRollMin + _rng.NextDouble() * (GameConfig.DamageRollMax - GameConfig.DamageRollMin));
        if (NowMs < monster.ExposedUntilMs)
            roll *= GameConfig.ExposedWeaknessDamageMultiplier;

        // trait: deadeye soma crit por distância; executioner/slayer multiplicam o roll
        var critChance = CritChance();
        if (_trait.Kind == "deadeye" && Chebyshev(Player, monster) >= (int)_trait.Param)
            critChance += _trait.Value * _traitMult;
        var crit = _rng.Chance(critChance);
        if (crit) roll *= GameConfig.CritMultiplier;

        if (_trait.Kind == "executioner" && monster.Hp < monster.MaxHp * _trait.Param)
            roll *= 1 + _trait.Value * _traitMult;
        if (_trait.Kind == "slayer" && monster.Species!.BestiaryClass == _trait.Tag)
            roll *= 1 + _trait.Value * _traitMult;

        // element bonus card
        if (element == Waifu.Element) roll *= 1 + CardValue("elementPercent");

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

        monster.Hp -= final;
        Emit("damage", monster.X, monster.Y, 0, 0, final, "", monster.Id, crit);
        if (hitEffect > 0) Emit("effect", monster.X, monster.Y, 0, 0, hitEffect);
        if (crit) Emit("effect", monster.X, monster.Y, 0, 0, 173);

        var lifesteal = CardValue("lifesteal") + GameConfig.BaselineLifesteal;
        if (lifesteal > 0) HealPlayer((int)Math.Max(final * lifesteal, 0));

        // traits pós-dano: seiva vital (lifesteal de skill) e mordida do norte (slow de gelo)
        if (fromSkill && _trait.Kind == "skill_lifesteal")
            HealPlayer((int)Math.Max(final * _trait.Value * _traitMult, 0));
        if (_trait.Kind == "chiller" && element == "ice" && monster.Hp > 0)
        {
            monster.SlowUntilMs = NowMs + (long)_trait.Param;
            monster.SlowFactor = Math.Max(1 - _trait.Value * _traitMult, GameConfig.SlowFactorFloor);
        }

        // F-E: posture build (boss only) and elemental reactions (any target)
        if (monster.Hp > 0 && monster.PostureMax > 0 && !staggered)
            AddPosture(monster, element, fromSkill);
        if (monster.Hp > 0)
            ApplyElementMarkAndReactions(monster, element, final);

        // aggro: damaged monsters retaliate
        if (monster.TargetId == 0) AcquirePlayer(monster);

        if (monster.Hp <= 0) KillMonster(monster);
    }

    // ---- F-E: boss posture (echo break) ----

    private void AddPosture(Actor boss, string element, bool fromSkill)
    {
        var gain = fromSkill ? GameConfig.PostureGainPerSkill : GameConfig.PostureGainPerAuto;
        if (boss.Species!.Elements.GetValueOrDefault(element, 0) < 0)
            gain *= GameConfig.PostureWeaknessMult; // hitting a weakness breaks faster
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
        if (monster.Hp <= 0 && !overkill && _kills > 0)
        {
            // already processed in same tick chain — guard double kill
        }
        monster.Hp = 0;
        _kills++;
        var speciesId = monster.Species!.StableId;
        KillsBySpecies[speciesId] = KillsBySpecies.GetValueOrDefault(speciesId) + 1;

        Emit("death", monster.X, monster.Y, 0, 0, monster.Species.Corpse, monster.Species.Name, monster.Id);

        // xp + gauge
        var xpScale = monster.Species.IsAuthored ? 1 : Tier.StatMultiplier;
        var xp = (long)(monster.Species.Experience * xpScale * (1 + CardValue("xpPercent")));
        GainXp(Math.Max(xp, 1));
        _gauge = Math.Min(_gauge + GameConfig.GaugeFillPerKill * (1 + CardValue("gaugePercent")) * _gaugeRate, GameConfig.UltimateGaugeMax);

        if (!monster.IsSummon) DropLoot(monster); // summons give xp but no loot (anti-farm)

        if (monster.IsBossActor)
            EndRun(true, $"{monster.Species.Name} derrotado");
    }

    private void DropLoot(Actor monster)
    {
        var junkGold = 0L;
        foreach (var entry in monster.Species!.Loot)
        {
            if (!_rng.Chance(entry.Chance / 100000.0)) continue;
            var count = entry.MaxCount > 1 ? _rng.Range(1, entry.MaxCount) : 1;
            if (entry.Name.Contains("gold coin", StringComparison.OrdinalIgnoreCase))
            {
                var gold = (long)(count * (1 + CardValue("goldPercent")) * Tier.StatMultiplier);
                _gold += gold;
                Emit("gold", monster.X, monster.Y, 0, 0, (int)gold);
                continue;
            }
            // comida, poção de vida e equip caem como item no chão; o resto (lixo) é auto-vendido em ouro
            if (_data.IsFood(entry.ItemId) || _data.PotionHealFraction(entry.ItemId) > 0
                || _data.IsEquippableLoot(entry.ItemId))
            {
                _groundItems.Add(new GroundItem
                {
                    Id = _nextActorId++,
                    Floor = monster.Floor,
                    X = monster.X, Y = monster.Y,
                    ItemId = entry.ItemId,
                    Count = count,
                    Name = entry.Name
                });
                continue;
            }
            junkGold += (long)_data.ItemValue(entry.ItemId) * count;
        }

        if (junkGold > 0)
        {
            var gold = (long)(junkGold * (1 + CardValue("goldPercent")));
            _gold += gold;
            Emit("gold", monster.X, monster.Y, 0, 0, (int)gold);
        }

        if (monster.IsBossActor
            && GameConfig.TierMountLookTypes.TryGetValue(Tier.Tier, out var mountLookType)
            && _rng.Chance(GameConfig.BossMountDropChance))
        {
            var itemId = GameConfig.MountItemId(mountLookType);
            if (_data.Items.TryGetValue(itemId, out var mount))
            {
                AddLootedItem(mount.ItemId, mount.Name, 1);
                Emit("pickup", monster.X, monster.Y, 0, 0, mount.ItemId, mount.Name);
            }
        }
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
            if (_pendingOffer is null) OfferCards();
            else _queuedOffers++;
        }
    }

    private void OfferCards()
    {
        var available = Cards.All
            .Where(c => _cards.GetValueOrDefault(c.Id) < GameConfig.MaxCardStacks)
            .ToList();
        if (available.Count == 0) return;
        _rng.Shuffle(available);
        _pendingOffer = available
            .Take(GameConfig.CardChoicesPerOffer)
            .Select(c => new CardOfferDto(c.Id, c.Name, c.Description, _cards.GetValueOrDefault(c.Id)))
            .ToList();
        _cardOfferStartedTick = TickCount;
    }

    private void ChooseCard(string cardId)
    {
        if (_pendingOffer is null || !_pendingOffer.Any(o => o.Id == cardId)) return;
        _pendingOffer = null;
        _cards[cardId] = _cards.GetValueOrDefault(cardId) + 1;

        if (cardId == "card:maxhp")
        {
            var def = Cards.ById[cardId];
            var bonus = (int)(Waifu.BaseHp * def.Value);
            Player.MaxHp += bonus;
            HealPlayer(bonus);
        }

        if (_queuedOffers > 0)
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
        if (attack.MaxDamage > 0 || attack.Kind == "melee")
            DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
        ApplyAttackSideEffects(monster, attack);
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
        var reduction = Math.Min(
            CardValue("damageReduction") + EquipmentStats.DamageReduction
            + Loadout.Mastery.DamageReductionBonus + traitReduction,
            GameConfig.PlayerDamageReductionCap);
        var final = damage * (1 - reduction);
        if (NowMs < source.SappedUntilMs)
            final *= GameConfig.SappedStrengthDamageMultiplier;
        if (IsBuffActive("shield")) final *= 0.5;
        var value = Math.Max((int)final, 1);

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

    private void TickPickup()
    {
        if (Player.Hp <= 0) return;
        for (var i = _groundItems.Count - 1; i >= 0; i--)
        {
            var item = _groundItems[i];
            if (item.Floor != Player.Floor || item.X != Player.X || item.Y != Player.Y) continue;
            _groundItems.RemoveAt(i);

            // consumíveis curam na hora; o resto vai pra mochila
            var potionFraction = _data.PotionHealFraction(item.ItemId);
            if (potionFraction > 0)
            {
                var heal = (int)Math.Ceiling(Player.MaxHp * potionFraction) * item.Count;
                HealPlayer(heal);
                Emit("effect", Player.X, Player.Y, 0, 0, 12); // sparkles
                Emit("pickup", Player.X, Player.Y, 0, 0, item.ItemId, item.Name);
                continue;
            }
            if (_data.IsFood(item.ItemId))
            {
                var heal = (int)Math.Ceiling(Player.MaxHp * GameConfig.FoodHealPct) * item.Count;
                HealPlayer(heal);
                Emit("effect", Player.X, Player.Y, 0, 0, 12); // sparkles
                Emit("pickup", Player.X, Player.Y, 0, 0, item.ItemId, item.Name);
                continue;
            }

            AddLootedItem(item.ItemId, item.Name, item.Count);
            Emit("pickup", Player.X, Player.Y, 0, 0, item.ItemId, item.Name);
        }
    }

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

        // chest
        poi.Used = true;
        _chestsOpened++;
        Emit("effect", x, y, 0, 0, 3); // poff

        if (_rng.Chance(GameConfig.ChestAmbushPercent / 100.0))
        {
            Emit("text", x, y, 0, 0, 0, "EMBOSCADA!");
            var room = Floor.Rooms.FirstOrDefault(r => r.Contains(x, y)) ?? Floor.Rooms[0];
            for (var i = 0; i < 3; i++)
            {
                var mob = SpawnMonster(_currentFloor, _rng.Pick(Tier.CommonMobs), room);
                if (mob is not null)
                {
                    AcquirePlayer(mob);
                    Emit("effect", mob.X, mob.Y, 0, 0, 11); // teleport
                }
            }
            return;
        }

        // loot burst: gold + random items from the tier's mob loot tables
        var gold = (long)(_rng.Range(40, 120) * Tier.StatMultiplier * (1 + CardValue("goldPercent")));
        _gold += gold;
        Emit("gold", x, y, 0, 0, (int)gold);
        Emit("effect", x, y, 0, 0, 29); // fireworks

        var lootPool = Tier.CommonMobs.Concat(Tier.EliteMobs)
            .SelectMany(name => _monsterRegistry.Get(name).Loot)
            .Where(l => _data.IsEquippableLoot(l.ItemId))
            .ToList();
        for (var i = 0; i < 2 && lootPool.Count > 0; i++)
        {
            var entry = _rng.Pick(lootPool);
            _groundItems.Add(new GroundItem
            {
                Id = _nextActorId++, Floor = _currentFloor,
                X = x, Y = y, ItemId = entry.ItemId, Count = 1, Name = entry.Name
            });
        }
    }

    private void DescendToFloor(int floorIndex)
    {
        _currentFloor = floorIndex;
        Player.Floor = floorIndex;
        var entry = _floors[floorIndex].Entry;
        Player.X = entry.X; Player.Y = entry.Y;
        Player.FromX = entry.X; Player.FromY = entry.Y;
        Player.StepDurMs = 0;
        Player.TargetId = 0;
        MapDirty = true;
        Emit("effect", entry.X, entry.Y, 0, 0, 11);
    }

    // ---- run end ----

    private void EndRun(bool victory, string reason)
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
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
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
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            if (floor.IsBlocked(x, y)) break;
            if (Chebyshev(x0, y0, x, y) > maxRange) break;
            lastX = x; lastY = y;
        }
        return (lastX, lastY);
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
            if (m.Hp <= 0 || m.Floor != _currentFloor) continue;
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
                if (Math.Abs(dx) + Math.Abs(dy) > radius * 1.5) continue; // rounded square
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
        return new MapDto(
            _currentFloor, floor.W, floor.H,
            floor.Ground, floor.Wall, floor.Decor, floor.Blocked,
            floor.Entry.X, floor.Entry.Y,
            floor.LadderDown?.X, floor.LadderDown?.Y,
            _pois.Where(p => p.Floor == _currentFloor)
                .Select(p => new PoiDto(p.Id, p.Kind, p.X, p.Y,
                    p.Kind == "chest" ? DungeonGenerator.ChestId : DungeonGenerator.LadderDownId, p.Used))
                .ToList());
    }

    private List<string> BuildActiveConditions()
    {
        var conditions = _playerConditions.Select(c => c.Type).ToList();
        if (NowMs < _playerSlowUntilMs) conditions.Add("slow");
        return conditions;
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
            var cooldownTotal = isUlt ? skill.CooldownMs : (int)(skill.CooldownMs * Loadout.Mastery.CooldownMult);
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
                Loadout.Skin.Addons > 0
                    ? Loadout.Skin.Addons
                    : Ascension >= GameConfig.AddonTwoAscension ? 3 : Ascension >= GameConfig.AddonOneAscension ? 1 : 0,
                Loadout.Skin.MountLookType > 0 ? Loadout.Skin.MountLookType : EquipmentStats.MountLookType),
            Player.TargetId, _gauge, skills,
            PlayerClass.Id, PlayerClass.Name,
            CurrentStance.Id, CurrentStance.Name, CurrentStance.Element, PlayerClass.CanToggleStance,
            Math.Max(_autoAttackReadyAtMs - NowMs, 0),
            _buffsUntilMs.Where(b => NowMs < b.Value).Select(b => b.Key).ToList(),
            BuildActiveConditions(),
            new EquipmentStatsDto(
                EquipmentStats.AttackBonus,
                EquipmentStats.MaxHpBonus,
                EquipmentStats.DamageReduction,
                EquipmentStats.MoveSpeedPercent));

        var monsters = _monsters
            .Where(m => m.Hp > 0 && m.Floor == _currentFloor)
            .Select(m => new MonsterDto(
                m.Id, m.Species!.Name, m.X, m.Y, (int)m.Facing, m.Hp, m.MaxHp,
                m.FromX, m.FromY, m.StepDurMs, m.StepStartMs,
                new OutfitDto(m.Species.Outfit.LookType, m.Species.Outfit.Head, m.Species.Outfit.Body,
                    m.Species.Outfit.Legs, m.Species.Outfit.Feet, m.Species.Outfit.Addons),
                m.IsBossActor, m.IsStunned(NowMs), m.ActiveMark(NowMs)))
            .ToList();

        var items = _groundItems
            .Where(i => i.Floor == _currentFloor)
            .Select(i => new GroundItemDto(i.Id, i.X, i.Y, i.ItemId, i.Count))
            .ToList();

        var run = new RunStateDto(
            Tier.Tier, Tier.Name, Seed,
            _runLevel, _runXp, GameConfig.XpForRunLevel(_runLevel),
            _gold, _kills,
            _cards.Select(c => new CardStackDto(c.Key, Cards.ById[c.Key].Name, c.Value)).ToList(),
            _pendingOffer,
            boss?.Hp, boss?.MaxHp, boss?.Species!.Name,
            boss is { PostureMax: > 0 } ? boss.Posture : null,
            boss is { PostureMax: > 0 } ? boss.PostureMax : null,
            boss is not null && boss.IsStaggered(NowMs),
            boss?.PostureCycle ?? 0,
            NowMs, Ended);

        return new SnapshotDto(TickCount, NowMs, _currentFloor, player, monsters, items, _events.ToList(), run);
    }

    public void RequestMapRefresh() => MapDirty = true;
}
