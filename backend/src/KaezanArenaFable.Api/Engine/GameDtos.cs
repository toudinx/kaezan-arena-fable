using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

public sealed record MapDto(
    int Floor, int W, int H,
    ushort[] Ground, ushort[] Wall, ushort[] Decor, bool[] Blocked,
    int EntryX, int EntryY, int? LadderX, int? LadderY,
    List<PoiDto> Pois,
    // G-07: grafo de salas + bioma. Rooms expõe o tipo de cada sala (combate/elite/tesouro/eco/
    // evento/miniboss/boss) pro minimapa desenhar ícones; Biome é a paleta de color-grade do estrato.
    List<RoomDto> Rooms, BiomeDto Biome)
{
    /// <summary>
    /// LM-08: monta o <see cref="MapDto"/> de wire a partir de um andar gerado + a atmosfera do estrato.
    /// Compartilhado pelo snapshot ao vivo (<see cref="GameWorld.BuildMap"/>) e pelo preview de bioma do
    /// admin (LM-09) — assim os dois leem o mapa de forma idêntica. Os POIs já vêm convertidos pelo
    /// chamador (cada modo decide o que telegrafar); o helper só costura ground/wall/decor + salas + bioma.
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

// G-09: Variant expõe só "cursed" (amaldiçoado, telegrafado) ou "" — mímicos chegam como "" (surpresa).
public sealed record PoiDto(int Id, string Kind, int X, int Y, int ItemId, string Variant, bool Used);

/// <summary>G-07: retângulo + tipo de uma sala, para o minimapa pintar a rota antecipável.</summary>
public sealed record RoomDto(int X, int Y, int W, int H, string Role);

/// <summary>G-07: paleta cosmética do estrato (color-grade/luz/névoa/partículas). Só o front lê.</summary>
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
    int CardRerollsRemaining, int BannedCardsCount, int CardRerollGoldCost,
    int? BossHp, int? BossMaxHp, string? BossName,
    double? BossPosture, double? BossPostureMax, bool BossStaggered, int BossPostureCycle,
    long ElapsedMs,
    RunEndDto? Ended,
    List<RewardItemDto> Items,
    NavTargetDto? NavTarget);

/// <summary>G-10: para onde o auto-loot está andando (tile + tipo), só pra legibilidade no cliente.</summary>
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
