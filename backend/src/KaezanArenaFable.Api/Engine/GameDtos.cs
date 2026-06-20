namespace KaezanArenaFable.Api.Engine;

public sealed record MapDto(
    int Floor, int W, int H,
    ushort[] Ground, ushort[] Wall, ushort[] Decor, bool[] Blocked,
    int EntryX, int EntryY, int? LadderX, int? LadderY,
    List<PoiDto> Pois);

public sealed record PoiDto(int Id, string Kind, int X, int Y, int ItemId, bool Used);

public sealed record SnapshotDto(
    long Tick, long SimulationMs, int Floor,
    PlayerDto Player,
    List<MonsterDto> Monsters,
    List<GroundItemDto> Items,
    List<EventDto> Events,
    RunStateDto Run);

public sealed record OutfitDto(
    int LookType, int Head, int Body, int Legs, int Feet, int Addons,
    int MountLookType = 0);

public sealed record EquipmentStatsDto(
    double AttackBonus, int MaxHpBonus, double DamageReduction, double MoveSpeedPercent,
    double SkillPowerMultiplier, double CritChance, double CritDamage, double CooldownReduction);

public sealed record AutoHelperSettingsDto(
    bool Targeting, bool Skills, bool Ultimate,
    string TargetPreference, string MovementMode, string DefaultMovementMode);

public sealed record PlayerDto(
    int Id, int X, int Y, int Dir, int Hp, int MaxHp,
    int FromX, int FromY, int StepDurMs, long StepStartTick,
    OutfitDto Outfit, int TargetId,
    double Gauge, List<SkillStateDto> Skills,
    string ClassId, string ClassName,
    string StanceId, string StanceName, string StanceElement, bool CanToggleStance,
    long AutoAttackReadyInMs, AutoHelperSettingsDto AutoHelper,
    List<string> ActiveBuffs, List<string> ActiveConditions,
    EquipmentStatsDto EquipmentStats,
    int PotionCharges, int PotionMaxCharges, int PotionItemId,
    long PotionCooldownRemainingMs, long PotionCooldownTotalMs, double PotionHealPct,
    TraitStateDto Trait);

/// <summary>
/// K-04: estado vivo da passiva assinatura para o HUD. Kind identifica a mecânica; Value/Max
/// alimentam uma barra (carga da Rynna, combo da Seren) quando Max &gt; 0; Text é o rótulo curto
/// (ex. "x3 (+24%)", "60/100", "2 em chamas", "HASTE"). Marcas por-alvo vão no MonsterDto.
/// </summary>
public sealed record TraitStateDto(
    string Kind, string Name, double Value, double Max, string Text);

public sealed record SkillStateDto(
    string Id, string Name, string Element, string Description,
    long CooldownRemainingMs, long CooldownTotalMs, bool Ready);

public sealed record MonsterDto(
    int Id, string Species, int X, int Y, int Dir, int Hp, int MaxHp,
    int FromX, int FromY, int StepDurMs, long StepStartTick,
    OutfitDto Outfit, bool IsBoss, bool Stunned, string ElementMark,
    // K-04: estado vivo da passiva por-alvo. TraitStacks = stacks (Pecado/Decadência/gelo);
    // TraitTag = marca especial ("judged" | "prey" | "frozen") pro render desenhar o ícone certo.
    int TraitStacks, string TraitTag);

public sealed record GroundItemDto(int Id, int X, int Y, int ItemId, int Count);

public sealed record EventDto(
    string Kind, int X, int Y, int ToX, int ToY, int Value,
    string Text, int ActorId, bool Crit);

public sealed record CardOfferDto(
    string Id, string Name, string Description, int CurrentStacks,
    string Rarity, IReadOnlyList<string> Tags, int MaxStacks);

public sealed record RunStateDto(
    int Tier, string TierName, long Seed,
    int Level, long Xp, long XpNext,
    long Gold, int Kills,
    List<CardStackDto> Cards,
    List<CardOfferDto>? Offer,
    int? BossHp, int? BossMaxHp, string? BossName,
    double? BossPosture, double? BossPostureMax, bool BossStaggered, int BossPostureCycle,
    long ElapsedMs,
    RunEndDto? Ended,
    List<RewardItemDto> Items);

public sealed record CardStackDto(
    string Id, string Name, int Stacks,
    string Rarity, IReadOnlyList<string> Tags);

public sealed record RunEndDto(
    bool Victory, string Reason,
    long GoldEarned, long AccountXpEarned, int KaerosEarned,
    int Kills, int RunLevel, long DurationMs,
    List<RewardItemDto> Items,
    List<string> DailyProgressNotes);

public sealed record RewardItemDto(int ItemId, string Name, int Count);
