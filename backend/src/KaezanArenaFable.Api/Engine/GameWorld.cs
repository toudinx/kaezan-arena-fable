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
    public bool IsBossActor;
    public double StatMult = 1.0;

    public bool IsMoving(long nowMs) => nowMs < StepStartMs + StepDurMs;
    public bool IsStunned(long nowMs) => nowMs < StunUntilMs;
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

public enum CommandKind { SetMoveDir, SetTarget, CastSkill, Interact, ChooseCard, Abandon }

public sealed record Command(CommandKind Kind, int A, int B, string? S);

/// <summary>
/// One dungeon run: server-authoritative world ticked at GameConfig.TickMs.
/// Movement, monster AI, combat, loot and run-cards all live here.
/// Deterministic for a given seed + command timeline.
/// </summary>
public sealed class GameWorld
{
    public readonly long Seed;
    public readonly DungeonTier Tier;
    public readonly WaifuDef Waifu;
    public readonly int Ascension;
    private readonly GameData _data;
    private readonly Rng _rng;
    private readonly IReadOnlyDictionary<string, long> _bestiaryKills;

    public long TickCount { get; private set; }
    private long NowMs => TickCount * GameConfig.TickMs;

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
    private int _queuedOffers;
    public Dictionary<string, int> KillsBySpecies { get; } = [];
    public List<RewardItemDto> ItemsLooted { get; } = [];
    private int _chestsOpened;
    public int ChestsOpened => _chestsOpened;

    // player combat state
    private readonly SkillDef[] _skills;
    private readonly long[] _skillReadyAtMs = new long[4];
    private long _autoAttackReadyAtMs;
    private int _moveDirX, _moveDirY; // held movement direction (-1..1)
    private readonly Dictionary<string, long> _buffsUntilMs = [];
    private double _regenCarry;

    public RunEndDto? Ended { get; private set; }
    public bool MapDirty { get; private set; } = true;

    public GameWorld(long seed, DungeonTier tier, WaifuDef waifu, int ascension,
        GameData data, IReadOnlyDictionary<string, long> bestiaryKills)
    {
        Seed = seed;
        Tier = tier;
        Waifu = waifu;
        Ascension = ascension;
        _data = data;
        _bestiaryKills = bestiaryKills;
        _rng = new Rng((ulong)seed);

        _floors = [DungeonGenerator.Generate(_rng, 0, false), DungeonGenerator.Generate(_rng, 1, true)];

        var hp = (int)(waifu.BaseHp * (1 + ascension * GameConfig.AscensionAtkBonus));
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

        _skills =
        [
            Waifus.Skills[waifu.Skill1],
            Waifus.Skills[waifu.Skill2],
            Waifus.Skills[waifu.Skill3],
            Waifus.Skills[waifu.Ultimate]
        ];

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
            boss.MaxHp = boss.Hp = (int)(boss.Hp * GameConfig.BossHpScale(Tier.Boss));
        }
        for (var i = 0; i < 2 + Tier.Tier / 2; i++)
            SpawnMonster(floorIndex, _rng.Pick(Tier.EliteMobs), room);
    }

    private Actor? SpawnMonster(int floorIndex, string speciesName, Room room, bool isBoss = false)
    {
        var species = _data.Get(speciesName);
        var floor = _floors[floorIndex];
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var x = _rng.Range(room.X, room.X + room.W - 1);
            var y = _rng.Range(room.Y, room.Y + room.H - 1);
            if (floor.IsBlocked(x, y) || OccupiedBy(floorIndex, x, y) is not null) continue;

            var mult = Tier.StatMultiplier;
            var actor = new Actor
            {
                Id = _nextActorId++,
                Species = species,
                Floor = floorIndex,
                X = x, Y = y, FromX = x, FromY = y,
                Hp = (int)(species.Health * mult),
                MaxHp = (int)(species.Health * mult),
                AttackReadyAtMs = new long[Math.Max(species.Attacks.Count, 1)],
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

    private void DrainCommands()
    {
        while (true)
        {
            Command cmd;
            lock (_commandLock)
            {
                if (_commands.Count == 0) return;
                cmd = _commands.Dequeue();
            }
            Apply(cmd);
        }
    }

    private void Apply(Command cmd)
    {
        if (Ended is not null) return;
        switch (cmd.Kind)
        {
            case CommandKind.SetMoveDir:
                _moveDirX = Math.Clamp(cmd.A, -1, 1);
                _moveDirY = Math.Clamp(cmd.B, -1, 1);
                break;
            case CommandKind.SetTarget:
                Player.TargetId = _monsters.Any(m => m.Id == cmd.A && m.Hp > 0) ? cmd.A : 0;
                break;
            case CommandKind.CastSkill:
                TryCastSkill(cmd.A);
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
            DrainCommands();
            TickPlayerMovement();
            TickPlayerCombat();
            TickPlayerRegen();
            TickMonsters();
            TickPickup();
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

    private int StepDuration(int speed, bool diagonal)
    {
        var ms = 1000.0 * GameConfig.GroundFriction / Math.Max(speed, 30);
        if (diagonal) ms *= GameConfig.DiagonalStepFactor;
        return (int)Math.Clamp(ms, GameConfig.MinStepMs, GameConfig.MaxStepMs);
    }

    private static Dir FacingFrom(int dx, int dy) =>
        Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? Dir.East : Dir.West) : (dy >= 0 ? Dir.South : Dir.North);

    private bool TryStep(Actor actor, int dx, int dy, int speed)
    {
        if (dx == 0 && dy == 0) return false;
        var nx = actor.X + dx;
        var ny = actor.Y + dy;
        var floor = _floors[actor.Floor];
        if (floor.IsBlocked(nx, ny) || OccupiedBy(actor.Floor, nx, ny) is not null)
            return false;

        actor.FromX = actor.X;
        actor.FromY = actor.Y;
        actor.X = nx;
        actor.Y = ny;
        actor.StepStartMs = NowMs;
        actor.StepDurMs = StepDuration(speed, dx != 0 && dy != 0);
        actor.Facing = FacingFrom(dx, dy);
        return true;
    }

    private int PlayerSpeed()
    {
        var speed = GameConfig.PlayerBaseSpeed * (1 + CardValue("moveSpeedPercent"));
        if (IsBuffActive("haste")) speed *= 1.30;
        return (int)speed;
    }

    private void TickPlayerMovement()
    {
        if (Player.Hp <= 0 || Player.IsMoving(NowMs) || Player.IsStunned(NowMs)) return;
        if (_moveDirX == 0 && _moveDirY == 0) return;

        // try the held direction; if diagonal is blocked, slide along an axis
        if (!TryStep(Player, _moveDirX, _moveDirY, PlayerSpeed()))
        {
            if (_moveDirX != 0 && TryStep(Player, _moveDirX, 0, PlayerSpeed())) return;
            if (_moveDirY != 0) TryStep(Player, 0, _moveDirY, PlayerSpeed());
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

    private double PlayerAttack()
    {
        var atk = Waifu.BaseAtk
                  * (1 + GameConfig.AtkPerRunLevel * (_runLevel - 1))
                  * (1 + Ascension * GameConfig.AscensionAtkBonus)
                  * (1 + CardValue("atkPercent"));
        if (IsBuffActive("atk")) atk *= 1.35;
        return atk;
    }

    private double CritChance()
    {
        var crit = GameConfig.CritChance + CardValue("critChance");
        if (IsBuffActive("crit")) crit += 0.20;
        return crit;
    }

    private long AutoAttackInterval()
    {
        var interval = GameConfig.PlayerAutoAttackMs / (1 + CardValue("atkSpeedPercent"));
        if (IsBuffActive("atkspeed")) interval /= 1.40;
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

        _autoAttackReadyAtMs = NowMs + AutoAttackInterval();
        Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        var missile = Waifus.WeaponMissile(Waifu.Weapon, Waifu.Element);
        if (missile > 0)
            Emit("projectile", Player.X, Player.Y, target.X, target.Y, missile);

        DealDamageToMonster(target, PlayerAttack(), Waifu.Element, hitEffect: missile > 0 ? 0 : 216);
    }

    private void TryCastSkill(int slot)
    {
        if (slot is < 0 or > 3 || Player.Hp <= 0 || Player.IsStunned(NowMs)) return;
        var skill = _skills[slot];
        var isUlt = slot == 3;

        if (isUlt)
        {
            if (_gauge < GameConfig.UltimateGaugeMax) return;
        }
        else if (NowMs < _skillReadyAtMs[slot]) return;

        var target = _monsters.FirstOrDefault(m => m.Id == Player.TargetId && m.Hp > 0 && m.Floor == Player.Floor)
                     ?? NearestMonster(skill.Range > 0 ? skill.Range : 7);

        // skills that need a target
        if (skill.Shape is "single" or "area" && target is null) return;
        if (skill.Shape is "single" && Chebyshev(Player, target!) > skill.Range) return;
        if (skill.Shape is "area" && Chebyshev(Player, target!) > skill.Range) return;

        if (isUlt) _gauge = 0;
        else _skillReadyAtMs[slot] = NowMs + skill.CooldownMs;

        Emit("skill_cast", Player.X, Player.Y, 0, 0, 0, skill.Name);
        if (target is not null)
            Player.Facing = FacingFrom(target.X - Player.X, target.Y - Player.Y);

        var damage = PlayerAttack() * skill.Power;
        var executeThreshold = skill.Id is "tessa_execute" or "mirai_bloodfang" ? 0.15 : 0.0;

        switch (skill.Shape)
        {
            case "buff":
                _buffsUntilMs[skill.Buff!] = NowMs + skill.BuffMs;
                Emit("effect", Player.X, Player.Y, 0, 0, skill.EffectId);
                break;

            case "single":
                if (skill.MissileId > 0) Emit("projectile", Player.X, Player.Y, target!.X, target.Y, skill.MissileId);
                Emit("effect", target!.X, target.Y, 0, 0, skill.EffectId);
                HitMonster(target, damage, skill, executeThreshold);
                break;

            case "area":
            {
                if (skill.MissileId > 0) Emit("projectile", Player.X, Player.Y, target!.X, target.Y, skill.MissileId);
                foreach (var (tx, ty) in CircleTiles(target!.X, target.Y, skill.Radius))
                {
                    Emit("effect", tx, ty, 0, 0, skill.EffectId);
                    var victim = MonsterAt(tx, ty);
                    if (victim is not null) HitMonster(victim, damage, skill, executeThreshold);
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
                    if (victim is not null) HitMonster(victim, damage, skill, executeThreshold);
                }
                if (skill.Buff is not null) _buffsUntilMs[skill.Buff] = NowMs + skill.BuffMs;
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
                    if (victim is not null) HitMonster(victim, damage, skill, executeThreshold);
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
                    if (victim is not null) HitMonster(victim, damage, skill, executeThreshold);
                }
                break;
            }
        }
    }

    private void HitMonster(Actor monster, double damage, SkillDef skill, double executeThreshold)
    {
        DealDamageToMonster(monster, damage, skill.Element, 0);
        if (monster.Hp > 0 && skill.StunMs > 0)
        {
            monster.StunUntilMs = NowMs + skill.StunMs;
            Emit("effect", monster.X, monster.Y, 0, 0, 32); // stun stars
        }
        if (monster.Hp > 0 && executeThreshold > 0 && monster.Hp <= monster.MaxHp * executeThreshold)
        {
            Emit("text", monster.X, monster.Y, 0, 0, 0, "EXECUTADO!");
            KillMonster(monster, overkill: true);
        }
    }

    private void DealDamageToMonster(Actor monster, double raw, string element, int hitEffect)
    {
        var roll = raw * (GameConfig.DamageRollMin + _rng.NextDouble() * (GameConfig.DamageRollMax - GameConfig.DamageRollMin));
        var crit = _rng.Chance(CritChance());
        if (crit) roll *= GameConfig.CritMultiplier;

        // element bonus card
        if (element == Waifu.Element) roll *= 1 + CardValue("elementPercent");

        // bestiary mastery bonus
        var rank = BestiaryRank(monster.Species!.Name);
        roll *= 1 + rank * GameConfig.BestiaryDamageBonusPerRank;

        // tibia armor + elemental resistance
        var afterArmor = roll - _rng.Range(0, Math.Max(monster.Species.Armor / 2, 0)) / Math.Max(monster.StatMult, 1);
        var resist = monster.Species.Elements.GetValueOrDefault(element, 0);
        afterArmor *= (100 - resist) / 100.0;

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

        var lifesteal = CardValue("lifesteal");
        if (lifesteal > 0) HealPlayer((int)Math.Max(final * lifesteal, 0));

        // aggro: damaged monsters retaliate
        if (monster.TargetId == 0) monster.TargetId = Player.Id;

        if (monster.Hp <= 0) KillMonster(monster);
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
        KillsBySpecies[monster.Species!.Name] = KillsBySpecies.GetValueOrDefault(monster.Species.Name) + 1;

        Emit("death", monster.X, monster.Y, 0, 0, monster.Species.Corpse, monster.Species.Name, monster.Id);

        // xp + gauge
        var xp = (long)(monster.Species.Experience * Tier.StatMultiplier * (1 + CardValue("xpPercent")));
        GainXp(Math.Max(xp, 1));
        _gauge = Math.Min(_gauge + GameConfig.GaugeFillPerKill * (1 + CardValue("gaugePercent")), GameConfig.UltimateGaugeMax);

        DropLoot(monster);

        if (monster.IsBossActor)
            EndRun(true, $"{monster.Species.Name} derrotado");
    }

    private void DropLoot(Actor monster)
    {
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
            _groundItems.Add(new GroundItem
            {
                Id = _nextActorId++,
                Floor = monster.Floor,
                X = monster.X, Y = monster.Y,
                ItemId = entry.ItemId,
                Count = count,
                Name = entry.Name
            });
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
        var regen = CardValue("regenPerSec");
        if (regen <= 0 || Player.Hp <= 0) return;
        _regenCarry += regen * GameConfig.TickMs / 1000.0;
        if (_regenCarry >= 1)
        {
            var whole = (int)_regenCarry;
            _regenCarry -= whole;
            HealPlayer(whole);
        }
    }

    // ---- combat: monsters ----

    private void TickMonsters()
    {
        foreach (var monster in _monsters)
        {
            if (monster.Hp <= 0 || monster.Floor != _currentFloor) continue;
            var species = monster.Species!;

            // voices (tibia flavor)
            if (species.Voices.Count > 0 && NowMs >= monster.NextVoiceAtMs)
            {
                monster.NextVoiceAtMs = NowMs + GameConfig.VoiceIntervalMs + _rng.Next(6000);
                if (Chebyshev(monster, Player) <= 9 && _rng.Chance(GameConfig.VoiceChancePercent / 100.0))
                    Emit("voice", monster.X, monster.Y, 0, 0, 0, _rng.Pick(species.Voices), monster.Id);
            }

            // acquire target (requires line of sight — no aggro through cave walls)
            if (monster.TargetId == 0 && Player.Hp > 0
                && Chebyshev(monster, Player) <= GameConfig.MonsterAggroRange
                && HasLineOfSight(monster.X, monster.Y, Player.X, Player.Y))
                monster.TargetId = Player.Id;

            if (monster.IsStunned(NowMs)) continue;

            if (monster.TargetId == 0 || Player.Hp <= 0)
            {
                Wander(monster);
                continue;
            }

            TryMonsterAttacks(monster);

            // chase: move toward player keeping targetDistance for ranged species
            if (monster.IsMoving(NowMs)) continue;
            var dist = Chebyshev(monster, Player);
            var desired = Math.Max(species.TargetDistance, 1);
            if (dist > desired)
                StepToward(monster, Player.X, Player.Y);
            else if (dist < desired && species.TargetDistance > 1 && _rng.Chance(0.5))
                StepAway(monster, Player.X, Player.Y);
            else
                monster.Facing = FacingFrom(Player.X - monster.X, Player.Y - monster.Y);
        }
    }

    private void TryMonsterAttacks(Actor monster)
    {
        var species = monster.Species!;
        for (var i = 0; i < species.Attacks.Count; i++)
        {
            var attack = species.Attacks[i];
            if (NowMs < monster.AttackReadyAtMs[i]) continue;
            var dist = Chebyshev(monster, Player);

            if (attack.Kind == "melee")
            {
                if (dist > 1) continue;
                monster.AttackReadyAtMs[i] = NowMs + attack.Interval;
                if (!_rng.Chance(Math.Min(attack.Chance, 100) / 100.0)) continue;
                DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
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
                    if (hitPlayer) DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
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
                    if (hitPlayer) DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
                }
                else
                {
                    if (attack.AreaEffect > 0) Emit("effect", Player.X, Player.Y, 0, 0, attack.AreaEffect);
                    DamagePlayer(RollMonsterDamage(monster, attack), attack.DamageType, monster);
                }
            }
        }
    }

    private int RollMonsterDamage(Actor monster, MonsterAttack attack)
    {
        var min = Math.Min(attack.MinDamage, attack.MaxDamage);
        var max = Math.Max(attack.MinDamage, attack.MaxDamage);
        var roll = _rng.Range(min, max) * monster.StatMult;
        // tame raw tibia numbers into arena pacing
        return Math.Max((int)(roll * 0.35), 1);
    }

    private void DamagePlayer(int damage, string damageType, Actor source)
    {
        if (Player.Hp <= 0) return;
        var final = damage * (1 - Math.Min(CardValue("damageReduction"), 0.6));
        if (IsBuffActive("shield")) final *= 0.5;
        var value = Math.Max((int)final, 1);

        Player.Hp -= value;
        _gauge = Math.Min(_gauge + value * GameConfig.GaugeFillPerDamageTaken * (1 + CardValue("gaugePercent")), GameConfig.UltimateGaugeMax);
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
        TryStep(monster, dx, dy, monster.Species!.Speed * GameConfig.MonsterSpeedMultiplier);
    }

    private void StepToward(Actor monster, int tx, int ty)
    {
        var dx = Math.Sign(tx - monster.X);
        var dy = Math.Sign(ty - monster.Y);
        var speed = monster.Species!.Speed * GameConfig.MonsterSpeedMultiplier;
        if (TryStep(monster, dx, dy, speed)) return;
        if (dx != 0 && TryStep(monster, dx, 0, speed)) return;
        if (dy != 0) TryStep(monster, 0, dy, speed);
    }

    private void StepAway(Actor monster, int tx, int ty)
    {
        var dx = -Math.Sign(tx - monster.X);
        var dy = -Math.Sign(ty - monster.Y);
        TryStep(monster, dx, dy, monster.Species!.Speed * GameConfig.MonsterSpeedMultiplier);
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
            var existing = ItemsLooted.FirstOrDefault(r => r.ItemId == item.ItemId);
            if (existing is not null)
            {
                ItemsLooted.Remove(existing);
                ItemsLooted.Add(existing with { Count = existing.Count + item.Count });
            }
            else
            {
                ItemsLooted.Add(new RewardItemDto(item.ItemId, item.Name, item.Count));
            }
            Emit("pickup", Player.X, Player.Y, 0, 0, item.ItemId, item.Name);
        }
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
                    mob.TargetId = Player.Id;
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
            .SelectMany(name => _data.Get(name).Loot)
            .Where(l => !l.Name.Contains("gold coin", StringComparison.OrdinalIgnoreCase))
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
        // frontal fan widening by distance
        for (var i = 1; i <= reach; i++)
        {
            var cx = ox + dx * i;
            var cy = oy + dy * i;
            var spread = i / 2;
            for (var s = -spread; s <= spread; s++)
            {
                var x = dx == 0 ? cx + s : cx;
                var y = dy == 0 ? cy + s : cy;
                if (dx != 0 && dy != 0) { x = cx + (s != 0 ? s : 0); y = cy - (s != 0 ? s : 0); }
                if (!Floor.IsBlocked(x, y)) yield return (x, y);
            }
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

    private SnapshotDto BuildSnapshot()
    {
        var skills = new List<SkillStateDto>(4);
        for (var i = 0; i < 4; i++)
        {
            var skill = _skills[i];
            var isUlt = i == 3;
            var remaining = isUlt ? 0 : Math.Max(_skillReadyAtMs[i] - NowMs, 0);
            var ready = isUlt ? _gauge >= GameConfig.UltimateGaugeMax : remaining == 0;
            skills.Add(new SkillStateDto(skill.Id, skill.Name, remaining, skill.CooldownMs, ready));
        }

        var boss = _monsters.FirstOrDefault(m => m.IsBossActor && m.Floor == _currentFloor);

        var player = new PlayerDto(
            Player.Id, Player.X, Player.Y, (int)Player.Facing, Player.Hp, Player.MaxHp,
            Player.FromX, Player.FromY, Player.StepDurMs, Player.StepStartMs,
            new OutfitDto(Waifu.LookType, Waifu.Head, Waifu.Body, Waifu.Legs, Waifu.Feet,
                Ascension >= GameConfig.AddonTwoAscension ? 3 : Ascension >= GameConfig.AddonOneAscension ? 1 : 0),
            Player.TargetId, _gauge, skills,
            Math.Max(_autoAttackReadyAtMs - NowMs, 0),
            _buffsUntilMs.Where(b => NowMs < b.Value).Select(b => b.Key).ToList());

        var monsters = _monsters
            .Where(m => m.Hp > 0 && m.Floor == _currentFloor)
            .Select(m => new MonsterDto(
                m.Id, m.Species!.Name, m.X, m.Y, (int)m.Facing, m.Hp, m.MaxHp,
                m.FromX, m.FromY, m.StepDurMs, m.StepStartMs,
                new OutfitDto(m.Species.Outfit.LookType, m.Species.Outfit.Head, m.Species.Outfit.Body,
                    m.Species.Outfit.Legs, m.Species.Outfit.Feet, m.Species.Outfit.Addons),
                m.IsBossActor, m.IsStunned(NowMs)))
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
            NowMs, Ended);

        return new SnapshotDto(TickCount, _currentFloor, player, monsters, items, _events.ToList(), run);
    }
}
