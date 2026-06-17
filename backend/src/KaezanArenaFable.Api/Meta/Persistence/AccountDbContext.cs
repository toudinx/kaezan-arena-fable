using Microsoft.EntityFrameworkCore;

namespace KaezanArenaFable.Api.Meta.Persistence;

public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<AccountWaifuRow> AccountWaifus => Set<AccountWaifuRow>();
    public DbSet<AccountSkinRow> AccountSkins => Set<AccountSkinRow>();
    public DbSet<AccountEquipmentRow> AccountEquipment => Set<AccountEquipmentRow>();
    public DbSet<AccountInventoryRow> AccountInventory => Set<AccountInventoryRow>();
    public DbSet<AccountMasteryRow> AccountMastery => Set<AccountMasteryRow>();
    public DbSet<AccountMasteryPointsRow> AccountMasteryPoints => Set<AccountMasteryPointsRow>();
    public DbSet<GachaPityRow> GachaPity => Set<GachaPityRow>();
    public DbSet<GachaHistoryRow> GachaHistory => Set<GachaHistoryRow>();
    public DbSet<BestiaryRow> Bestiary => Set<BestiaryRow>();
    public DbSet<DailyContractRow> DailyContracts => Set<DailyContractRow>();
    public DbSet<TierClearRow> TierClears => Set<TierClearRow>();
    public DbSet<RunResultRow> RunResults => Set<RunResultRow>();
    public DbSet<ReplayRow> Replays => Set<ReplayRow>();
    public DbSet<DailyChallengeScoreRow> DailyChallengeScores => Set<DailyChallengeScoreRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureAccount(modelBuilder);
        ConfigureMutableState(modelBuilder);
        ConfigureFutureState(modelBuilder);
    }

    private static void ConfigureAccount(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRow>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(row => row.Level).HasColumnName("level");
            entity.Property(row => row.Xp).HasColumnName("xp");
            entity.Property(row => row.Gold).HasColumnName("gold");
            entity.Property(row => row.Kaeros).HasColumnName("kaeros");
            entity.Property(row => row.ActiveWaifuId).HasColumnName("active_waifu_id").HasMaxLength(64);
            entity.Property(row => row.DailyDate).HasColumnName("daily_date").HasMaxLength(10);
            entity.Property(row => row.GiftsDate).HasColumnName("gifts_date").HasMaxLength(10)
                .HasDefaultValue("");
            entity.Property(row => row.RunsPlayed).HasColumnName("runs_played");
            entity.Property(row => row.RunsWon).HasColumnName("runs_won");
        });
    }

    private static void ConfigureMutableState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountWaifuRow>(entity =>
        {
            entity.ToTable("account_waifus");
            entity.HasKey(row => new { row.AccountId, row.WaifuId });
            AccountForeignKey(entity);
            entity.Property(row => row.WaifuId).HasColumnName("waifu_id").HasMaxLength(64);
            entity.Property(row => row.Ascension).HasColumnName("ascension");
            entity.Property(row => row.Shards).HasColumnName("shards");
            entity.Property(row => row.AffinityXp).HasColumnName("affinity_xp").HasDefaultValue(0L);
            entity.Property(row => row.GiftsToday).HasColumnName("gifts_today").HasDefaultValue(0);
            entity.Property(row => row.SelectedSkinId).HasColumnName("selected_skin_id")
                .HasMaxLength(96).HasDefaultValue("");
        });

        modelBuilder.Entity<AccountSkinRow>(entity =>
        {
            entity.ToTable("account_skins");
            entity.HasKey(row => new { row.AccountId, row.SkinId });
            AccountForeignKey(entity);
            entity.Property(row => row.SkinId).HasColumnName("skin_id").HasMaxLength(96);
        });

        modelBuilder.Entity<AccountInventoryRow>(entity =>
        {
            entity.ToTable("account_inventory");
            entity.HasKey(row => new { row.AccountId, row.ItemId });
            AccountForeignKey(entity);
            entity.Property(row => row.ItemId).HasColumnName("item_id");
            entity.Property(row => row.Name).HasColumnName("name").HasMaxLength(160);
            entity.Property(row => row.Count).HasColumnName("count");
        });

        modelBuilder.Entity<GachaPityRow>(entity =>
        {
            entity.ToTable("gacha_pity");
            entity.HasKey(row => new { row.AccountId, row.BannerId });
            AccountForeignKey(entity);
            entity.Property(row => row.BannerId).HasColumnName("banner_id").HasMaxLength(64);
            entity.Property(row => row.SinceFive).HasColumnName("since_5");
            entity.Property(row => row.SinceFour).HasColumnName("since_4");
            entity.Property(row => row.Guaranteed).HasColumnName("guaranteed");
            entity.Property(row => row.Total).HasColumnName("total");
        });

        modelBuilder.Entity<BestiaryRow>(entity =>
        {
            entity.ToTable("bestiary");
            entity.HasKey(row => new { row.AccountId, row.Species });
            AccountForeignKey(entity);
            entity.Property(row => row.Species).HasColumnName("species").HasMaxLength(160);
            entity.Property(row => row.Kills).HasColumnName("kills");
        });

        modelBuilder.Entity<DailyContractRow>(entity =>
        {
            entity.ToTable("daily_contracts");
            entity.HasKey(row => new { row.AccountId, row.Date, row.ContractId });
            AccountForeignKey(entity);
            entity.Property(row => row.Date).HasColumnName("date").HasMaxLength(10);
            entity.Property(row => row.ContractId).HasColumnName("contract_id").HasMaxLength(96);
            entity.Property(row => row.Kind).HasColumnName("kind").HasMaxLength(32);
            entity.Property(row => row.Param).HasColumnName("param").HasMaxLength(160);
            entity.Property(row => row.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(row => row.Target).HasColumnName("target");
            entity.Property(row => row.Progress).HasColumnName("progress");
            entity.Property(row => row.Claimed).HasColumnName("claimed");
        });

        modelBuilder.Entity<TierClearRow>(entity =>
        {
            entity.ToTable("tier_clears");
            entity.HasKey(row => new { row.AccountId, row.Tier });
            AccountForeignKey(entity);
            entity.Property(row => row.Tier).HasColumnName("tier");
            entity.Property(row => row.Clears).HasColumnName("clears");
        });
    }

    private static void ConfigureFutureState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEquipmentRow>(entity =>
        {
            entity.ToTable("account_equipment");
            entity.HasKey(row => new { row.AccountId, row.WaifuId, row.Tier, row.Slot });
            AccountForeignKey(entity);
            entity.Property(row => row.WaifuId).HasColumnName("waifu_id").HasMaxLength(64);
            entity.Property(row => row.Tier).HasColumnName("tier").HasDefaultValue(1);
            entity.Property(row => row.Slot).HasColumnName("slot").HasMaxLength(24);
            entity.Property(row => row.ItemId).HasColumnName("item_id");
        });

        modelBuilder.Entity<AccountMasteryRow>(entity =>
        {
            entity.ToTable("account_mastery");
            entity.HasKey(row => new { row.AccountId, row.WaifuId, row.NodeId });
            AccountForeignKey(entity);
            entity.Property(row => row.WaifuId).HasColumnName("waifu_id").HasMaxLength(64);
            entity.Property(row => row.NodeId).HasColumnName("node_id").HasMaxLength(96);
        });

        modelBuilder.Entity<AccountMasteryPointsRow>(entity =>
        {
            entity.ToTable("account_mastery_points");
            entity.HasKey(row => new { row.AccountId, row.WaifuId });
            AccountForeignKey(entity);
            entity.Property(row => row.WaifuId).HasColumnName("waifu_id").HasMaxLength(64);
            entity.Property(row => row.Points).HasColumnName("points");
            entity.Property(row => row.Spent).HasColumnName("spent");
        });

        modelBuilder.Entity<GachaHistoryRow>(entity =>
        {
            entity.ToTable("gacha_history");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            AccountForeignKey(entity);
            entity.Property(row => row.BannerId).HasColumnName("banner_id").HasMaxLength(64);
            entity.Property(row => row.WaifuId).HasColumnName("waifu_id").HasMaxLength(64);
            entity.Property(row => row.Rarity).HasColumnName("rarity");
            entity.Property(row => row.Timestamp).HasColumnName("ts");
            entity.HasIndex(row => new { row.AccountId, row.Timestamp });
        });

        modelBuilder.Entity<RunResultRow>(entity =>
        {
            entity.ToTable("run_results");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            AccountForeignKey(entity);
            entity.Property(row => row.Seed).HasColumnName("seed");
            entity.Property(row => row.Tier).HasColumnName("tier");
            entity.Property(row => row.WaifuId).HasColumnName("waifu_id").HasMaxLength(64);
            entity.Property(row => row.Outcome).HasColumnName("outcome").HasMaxLength(24);
            entity.Property(row => row.Kills).HasColumnName("kills");
            entity.Property(row => row.RunLevel).HasColumnName("run_level");
            entity.Property(row => row.DurationMs).HasColumnName("duration_ms");
            entity.Property(row => row.Timestamp).HasColumnName("ts");
            entity.HasIndex(row => new { row.AccountId, row.Timestamp });
        });

        modelBuilder.Entity<ReplayRow>(entity =>
        {
            entity.ToTable("replays");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            AccountForeignKey(entity);
            entity.Property(row => row.Seed).HasColumnName("seed");
            entity.Property(row => row.Tier).HasColumnName("tier");
            entity.Property(row => row.CommandsJson).HasColumnName("commands_json").HasColumnType("longtext");
            entity.Property(row => row.FinalHash).HasColumnName("final_hash").HasMaxLength(128);
            entity.Property(row => row.Timestamp).HasColumnName("ts");
            entity.HasIndex(row => new { row.AccountId, row.Timestamp });
        });

        modelBuilder.Entity<DailyChallengeScoreRow>(entity =>
        {
            entity.ToTable("daily_challenge_scores");
            entity.HasKey(row => new { row.Date, row.AccountId });
            entity.Property(row => row.Date).HasColumnName("date").HasMaxLength(10);
            AccountForeignKey(entity);
            entity.Property(row => row.Score).HasColumnName("score");
            entity.Property(row => row.TimeMs).HasColumnName("time_ms");
            entity.HasIndex(row => new { row.Date, row.Score });
        });
    }

    private static void AccountForeignKey<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : class
    {
        entity.Property<string>("AccountId").HasColumnName("account_id").HasMaxLength(64);
        entity.HasOne<AccountRow>()
            .WithMany()
            .HasForeignKey("AccountId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
