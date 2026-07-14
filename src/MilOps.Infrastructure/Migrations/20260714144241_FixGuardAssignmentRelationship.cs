using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixGuardAssignmentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_guard_assignments_guard_schedules_GuardScheduleId1",
                table: "guard_assignments");

            migrationBuilder.DropIndex(
                name: "IX_guard_assignments_GuardScheduleId1",
                table: "guard_assignments");

            migrationBuilder.DropColumn(
                name: "GuardScheduleId1",
                table: "guard_assignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuardScheduleId1",
                table: "guard_assignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_guard_assignments_GuardScheduleId1",
                table: "guard_assignments",
                column: "GuardScheduleId1");

            migrationBuilder.AddForeignKey(
                name: "FK_guard_assignments_guard_schedules_GuardScheduleId1",
                table: "guard_assignments",
                column: "GuardScheduleId1",
                principalTable: "guard_schedules",
                principalColumn: "Id");
        }
    }
}
