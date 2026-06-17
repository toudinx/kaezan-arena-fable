using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaezanArenaFable.Api.Meta.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_account_equipment",
                table: "account_equipment");

            migrationBuilder.AddColumn<int>(
                name: "tier",
                table: "account_equipment",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddPrimaryKey(
                name: "PK_account_equipment",
                table: "account_equipment",
                columns: new[] { "account_id", "waifu_id", "tier", "slot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_account_equipment",
                table: "account_equipment");

            migrationBuilder.DropColumn(
                name: "tier",
                table: "account_equipment");

            migrationBuilder.AddPrimaryKey(
                name: "PK_account_equipment",
                table: "account_equipment",
                columns: new[] { "account_id", "waifu_id", "slot" });
        }
    }
}
