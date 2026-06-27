using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    PreviousHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RowHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guard_post_register",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    Time = table.Column<string>(type: "TEXT", nullable: false),
                    SoldierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Post = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    WeaponNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AmmunitionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Signature = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_post_register", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guard_schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ArmedForceMorning1 = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ArmedForceMorning2 = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ArmedForceMorning3 = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Watchman = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Armament = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Refuge = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ShelterManager = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leaves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SoldierId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<string>(type: "TEXT", nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "soldiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    FatherName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Rank = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NationalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PersonnelCode = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    HealthType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EntryDate = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceStartDate = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceEndDate = table.Column<string>(type: "TEXT", nullable: false),
                    DepartmentName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_soldiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TokenPreview = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NationalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PersonnelCode = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Rank = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ServiceStartDate = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceEndDate = table.Column<string>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IssuedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevocationReason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsLockedOut = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "weapons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeaponNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weapons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guard_assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuardScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    SoldierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Post = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Shift = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ShiftStart = table.Column<string>(type: "TEXT", nullable: true),
                    ShiftEnd = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GuardScheduleId1 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guard_assignments_guard_schedules_GuardScheduleId",
                        column: x => x.GuardScheduleId,
                        principalTable: "guard_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guard_assignments_guard_schedules_GuardScheduleId1",
                        column: x => x.GuardScheduleId1,
                        principalTable: "guard_schedules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "weapon_assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeaponId = table.Column<int>(type: "INTEGER", nullable: false),
                    SoldierId = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReturnedAmmunition = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    WeaponId1 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weapon_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weapon_assignments_weapons_WeaponId",
                        column: x => x.WeaponId,
                        principalTable: "weapons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_weapon_assignments_weapons_WeaponId1",
                        column: x => x.WeaponId1,
                        principalTable: "weapons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_Sequence",
                table: "audit_logs",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guard_assignments_GuardScheduleId_Post_Shift",
                table: "guard_assignments",
                columns: new[] { "GuardScheduleId", "Post", "Shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guard_assignments_GuardScheduleId1",
                table: "guard_assignments",
                column: "GuardScheduleId1");

            migrationBuilder.CreateIndex(
                name: "IX_tokens_Status",
                table: "tokens",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tokens_TokenHash",
                table: "tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weapon_assignments_WeaponId",
                table: "weapon_assignments",
                column: "WeaponId");

            migrationBuilder.CreateIndex(
                name: "IX_weapon_assignments_WeaponId1",
                table: "weapon_assignments",
                column: "WeaponId1");

            migrationBuilder.CreateIndex(
                name: "IX_weapons_WeaponNumber",
                table: "weapons",
                column: "WeaponNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "guard_assignments");

            migrationBuilder.DropTable(
                name: "guard_post_register");

            migrationBuilder.DropTable(
                name: "leaves");

            migrationBuilder.DropTable(
                name: "soldiers");

            migrationBuilder.DropTable(
                name: "tokens");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "weapon_assignments");

            migrationBuilder.DropTable(
                name: "guard_schedules");

            migrationBuilder.DropTable(
                name: "weapons");
        }
    }
}
