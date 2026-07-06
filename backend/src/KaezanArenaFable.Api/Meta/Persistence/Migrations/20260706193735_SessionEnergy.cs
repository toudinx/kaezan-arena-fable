using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaezanArenaFable.Api.Meta.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionEnergy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "energy",
                table: "accounts",
                type: "int",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<string>(
                name: "energy_updated_utc",
                table: "accounts",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "energy",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "energy_updated_utc",
                table: "accounts");
        }
    }
}
