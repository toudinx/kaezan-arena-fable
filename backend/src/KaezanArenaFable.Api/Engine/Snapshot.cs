namespace KaezanArenaFable.Api.Engine;

// ---- map payload (sent on join / floor change) ----

public sealed record MapDto(
    int Floor, int W, int H,
    ushort[] Ground, ushort[] Wall, ushort[] Decor, bool[] Blocked,
    int EntryX, int EntryY, int? LadderX, int? LadderY,
    List<PoiDto> Pois);

public sealed record PoiDto(int Id, string Kind, int X, int Y, int ItemId, bool Used);

// ---- per-tick snapshot ----

public sealed record SnapshotDto(
    long Tick, long SimulationMs, int Floor,
    PlayerDto Player,
    List<MonsterDto> Monsters,
    List<GroundItemDto> Items,
    List<EventDto> Events,
    RunStateDto Run);

public sealed record OutfitDto(int LookType, int Head, int Body, int Legs, int Feet, int Addons);

public sealed record PlayerDto(
    int Id, int X, int Y, int Dir, int Hp, int MaxHp,
    int FromX, int FromY, int StepDurMs, long StepStartTick,
    OutfitDto Outfit, int TargetId,
    double Gauge, List<SkillStateDto> Skills,
    string ClassId, string ClassName,
    string StanceId, string StanceName, string StanceElement, bool CanToggleStance,
    long AutoAttackReadyInMs, List<string> ActiveBuffs);

public sealed record SkillStateDto(
    string Id, string Name, string Element, string Description,
    long CooldownRemainingMs, long CooldownTotalMs, bool Ready);

public sealed record MonsterDto(
    int Id, string Species, int X, int Y, int Dir, int Hp, int MaxHp,
    int FromX, int FromY, int StepDurMs, long StepStartTick,
    OutfitDto Outfit, bool IsBoss, bool Stunned);

public sealed record GroundItemDto(int Id, int X, int Y, int ItemId, int Count);

/// <summary>Generic event bag; renderer switches on Kind.</summary>
public sealed record EventDto(
    string Kind, int X, int Y, int ToX, int ToY, int Value,
    string Text, int ActorId, bool Crit);

public sealed record CardOfferDto(string Id, string Name, string Description, int CurrentStacks);

public sealed record RunStateDto(
    int Tier, string TierName, long Seed,
    int Level, long Xp, long XpNext,
    long Gold, int Kills,
    List<CardStackDto> Cards,
    List<CardOfferDto>? Offer,
    int? BossHp, int? BossMaxHp, string? BossName,
    long ElapsedMs,
    RunEndDto? Ended);

public sealed record CardStackDto(string Id, string Name, int Stacks);

public sealed record RunEndDto(
    bool Victory, string Reason,
    long GoldEarned, long AccountXpEarned, int KaerosEarned,
    int Kills, int RunLevel, long DurationMs,
    List<RewardItemDto> Items,
    List<string> DailyProgressNotes);

public sealed record RewardItemDto(int ItemId, string Name, int Count);
