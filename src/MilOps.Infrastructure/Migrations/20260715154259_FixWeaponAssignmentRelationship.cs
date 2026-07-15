using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixWeaponAssignmentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_weapon_assignments_weapons_WeaponId1",
                table: "weapon_assignments");

            migrationBuilder.DropIndex(
                name: "IX_weapon_assignments_WeaponId1",
                table: "weapon_assignments");

            migrationBuilder.DropColumn(
                name: "WeaponId1",
                table: "weapon_assignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeaponId1",
                table: "weapon_assignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_weapon_assignments_WeaponId1",
                table: "weapon_assignments",
                column: "WeaponId1");

            migrationBuilder.AddForeignKey(
                name: "FK_weapon_assignments_weapons_WeaponId1",
                table: "weapon_assignments",
                column: "WeaponId1",
                principalTable: "weapons",
                principalColumn: "Id");
        }
    }
}
