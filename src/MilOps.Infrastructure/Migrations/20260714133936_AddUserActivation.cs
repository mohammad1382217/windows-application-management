using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing accounts predate the token-activation flow and must stay
            // usable, so they are backfilled as already-activated. Only accounts
            // created after this migration start locked behind a token.
            migrationBuilder.AddColumn<bool>(
                name: "IsActivated",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActivated",
                table: "users");
        }
    }
}
