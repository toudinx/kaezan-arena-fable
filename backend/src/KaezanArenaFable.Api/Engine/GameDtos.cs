using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

public sealed record MapDto(
    int Floor, int W, int H,
    ushort[] Ground, ushort[] Wall, ushort[] Decor, bool[] Blocked,
    int EntryX, int EntryY, int? LadderX, int? LadderY,
    List<PoiDto> Pois,
    // G-07: room graph + biome. Rooms exposes each room type (combat/elite/treasure/echo/
    // event/miniboss/boss) so the minimap can draw icons; Biome is the stratum color-grade palette.
    List<RoomDto> Rooms, BiomeDto Biome)
{
    /// <summary>
    /// LM-08: builds the wire <see cref="MapDto"/> from a generated floor plus the stratum atmosphere.
    /// Shared by the live snapshot (<see cref="GameWorld.BuildMap"/>) and the admin biome preview
    /// (LM-09), so both read the map identically. POIs are already converted by the caller
    /// (each mode decides what to telegraph); the helper only stitches ground/wall/decor + rooms + biome.
    /// </summary>
    public static MapDto FromFloor(
        DungeonFloor floor, BiomeAtmosphere atmo, int floorIndex, IReadOnlyList<PoiDto> pois) =>
        new(
            floorIndex, floor.W, floor.H,
            floor.Ground, floor.Wall, floor.Decor, floor.Blocked,
            floor.Entry.X, floor.Entry.Y,
            floor.LadderDown?.X, floor.LadderDown?.Y,
            pois.ToList(),
            floor.Rooms.Select(r => new RoomDto(r.X, r.Y, r.W, r.H, r.Role)).ToList(),
            new BiomeDto(
                atmo.Name,
                atmo.TintR, atmo.TintG, atmo.TintB, atmo.TintStrength,
                atmo.FogR, atmo.FogG, atmo.FogB, atmo.FogStrength,
                atmo.Vignette,
                atmo.ParticleR, atmo.ParticleG, atmo.ParticleB, atmo.ParticleDensity, atmo.ParticleDrift));
}

// G-09: Variant exposes only "cursed" (telegraphed) or ""; mimics arrive as "" (surprise).
public sealed record PoiDto(int Id, string Kind, int X, int Y, int ItemId, string Variant, bool Used);

/// <summary>G-07: rectangle + room type, so the minimap can paint the anticipated route.</summary>
public sealed record RoomDto(int X, int Y, int W, int H, string Role);

/// <summary>G-07: cosmetic stratum palette (color-grade/light/fog/particles). Only the frontend reads it.</summary>
public sealed record BiomeDto(
    string Name,
    int TintR, int TintG, int TintB, double TintStrength,
    int FogR, int FogG, int FogB, double FogStrength,
    double Vignette,
    int ParticleR, int ParticleG, int ParticleB, double ParticleDensity, int ParticleDrift);

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
    string TargetPreference, string MovementMode, string DefaultMovementMode,
    bool AutoHeal, int AutoHealPct, string NavMode, bool AutoCards);

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
    long DashCooldownRemainingMs, long DashCooldownTotalMs, bool DashReady,
    TraitStateDto Trait);

/// <summary>
/// K-04: live signature-passive state for the HUD. Kind identifies the mechanic; Value/Max
/// feed a bar (Rynna charge, Seren combo) when Max &gt; 0; Text is the short label
/// (for example "x3 (+24%)", "60/100", "2 burning", "HASTE"). Per-target marks go in MonsterDto.
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
    // K-04: live per-target passive state. TraitStacks = stacks (Sin/Decay/frost);
    // TraitTag = special mark ("judged" | "prey" | "frozen") so the renderer can draw the right icon.
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
    int CardRerollsRemaining, int BannedCardsCount, int CardRerollGoldCost,
    int? BossHp, int? BossMaxHp, string? BossName,
    double? BossPosture, double? BossPostureMax, bool BossStaggered, int BossPostureCycle,
    long ElapsedMs,
    RunEndDto? Ended,
    List<RewardItemDto> Items,
    NavTargetDto? NavTarget);

/// <summary>G-10: where auto-loot is walking (tile + type), only for client readability.</summary>
public sealed record NavTargetDto(int X, int Y, string Kind);

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
