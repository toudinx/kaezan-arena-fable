using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaezanArenaFable.Api.Meta.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfflineProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_seen_utc",
                table: "accounts",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_seen_utc",
                table: "accounts");
        }
    }
}
