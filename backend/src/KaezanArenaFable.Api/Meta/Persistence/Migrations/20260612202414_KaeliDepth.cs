using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaezanArenaFable.Api.Meta.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KaeliDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gifts_date",
                table: "accounts",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "affinity_xp",
                table: "account_waifus",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "gifts_today",
                table: "account_waifus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "selected_skin_id",
                table: "account_waifus",
                type: "varchar(96)",
                maxLength: 96,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_skins",
                columns: table => new
                {
                    account_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    skin_id = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_skins", x => new { x.account_id, x.skin_id });
                    table.ForeignKey(
                        name: "FK_account_skins_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_skins");

            migrationBuilder.DropColumn(
                name: "gifts_date",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "affinity_xp",
                table: "account_waifus");

            migrationBuilder.DropColumn(
                name: "gifts_today",
                table: "account_waifus");

            migrationBuilder.DropColumn(
                name: "selected_skin_id",
                table: "account_waifus");
        }
    }
}
