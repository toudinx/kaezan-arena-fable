using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaezanArenaFable.Api.Meta.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlayerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    level = table.Column<int>(type: "int", nullable: false),
                    xp = table.Column<long>(type: "bigint", nullable: false),
                    gold = table.Column<long>(type: "bigint", nullable: false),
                    kaeros = table.Column<long>(type: "bigint", nullable: false),
                    active_waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    daily_date = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    runs_played = table.Column<int>(type: "int", nullable: false),
                    runs_won = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_equipment",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    slot = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_equipment", x => new { x.account_id, x.waifu_id, x.slot });
                    table.ForeignKey(
                        name: "FK_account_equipment_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_inventory",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_inventory", x => new { x.account_id, x.item_id });
                    table.ForeignKey(
                        name: "FK_account_inventory_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_mastery",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    node_id = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_mastery", x => new { x.account_id, x.waifu_id, x.node_id });
                    table.ForeignKey(
                        name: "FK_account_mastery_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_mastery_points",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    points = table.Column<int>(type: "int", nullable: false),
                    spent = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_mastery_points", x => new { x.account_id, x.waifu_id });
                    table.ForeignKey(
                        name: "FK_account_mastery_points_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_waifus",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ascension = table.Column<int>(type: "int", nullable: false),
                    shards = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_waifus", x => new { x.account_id, x.waifu_id });
                    table.ForeignKey(
                        name: "FK_account_waifus_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bestiary",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    species = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kills = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bestiary", x => new { x.account_id, x.species });
                    table.ForeignKey(
                        name: "FK_bestiary_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "daily_challenge_scores",
                columns: table => new
                {
                    date = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    score = table.Column<long>(type: "bigint", nullable: false),
                    time_ms = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_challenge_scores", x => new { x.date, x.account_id });
                    table.ForeignKey(
                        name: "FK_daily_challenge_scores_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "daily_contracts",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    contract_id = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    param = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    target = table.Column<long>(type: "bigint", nullable: false),
                    progress = table.Column<long>(type: "bigint", nullable: false),
                    claimed = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_contracts", x => new { x.account_id, x.date, x.contract_id });
                    table.ForeignKey(
                        name: "FK_daily_contracts_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gacha_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    banner_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rarity = table.Column<int>(type: "int", nullable: false),
                    ts = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gacha_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_gacha_history_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gacha_pity",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    banner_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    since_5 = table.Column<int>(type: "int", nullable: false),
                    since_4 = table.Column<int>(type: "int", nullable: false),
                    guaranteed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    total = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gacha_pity", x => new { x.account_id, x.banner_id });
                    table.ForeignKey(
                        name: "FK_gacha_pity_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "replays",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    seed = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    tier = table.Column<int>(type: "int", nullable: false),
                    commands_json = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    final_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ts = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replays", x => x.id);
                    table.ForeignKey(
                        name: "FK_replays_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "run_results",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    seed = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    tier = table.Column<int>(type: "int", nullable: false),
                    waifu_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    outcome = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kills = table.Column<int>(type: "int", nullable: false),
                    run_level = table.Column<int>(type: "int", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    ts = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_run_results_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tier_clears",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tier = table.Column<int>(type: "int", nullable: false),
                    clears = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tier_clears", x => new { x.account_id, x.tier });
                    table.ForeignKey(
                        name: "FK_tier_clears_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_daily_challenge_scores_account_id",
                table: "daily_challenge_scores",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_challenge_scores_date_score",
                table: "daily_challenge_scores",
                columns: new[] { "date", "score" });

            migrationBuilder.CreateIndex(
                name: "IX_gacha_history_account_id_ts",
                table: "gacha_history",
                columns: new[] { "account_id", "ts" });

            migrationBuilder.CreateIndex(
                name: "IX_replays_account_id_ts",
                table: "replays",
                columns: new[] { "account_id", "ts" });

            migrationBuilder.CreateIndex(
                name: "IX_run_results_account_id_ts",
                table: "run_results",
                columns: new[] { "account_id", "ts" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_equipment");

            migrationBuilder.DropTable(
                name: "account_inventory");

            migrationBuilder.DropTable(
                name: "account_mastery");

            migrationBuilder.DropTable(
                name: "account_mastery_points");

            migrationBuilder.DropTable(
                name: "account_waifus");

            migrationBuilder.DropTable(
                name: "bestiary");

            migrationBuilder.DropTable(
                name: "daily_challenge_scores");

            migrationBuilder.DropTable(
                name: "daily_contracts");

            migrationBuilder.DropTable(
                name: "gacha_history");

            migrationBuilder.DropTable(
                name: "gacha_pity");

            migrationBuilder.DropTable(
                name: "replays");

            migrationBuilder.DropTable(
                name: "run_results");

            migrationBuilder.DropTable(
                name: "tier_clears");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
