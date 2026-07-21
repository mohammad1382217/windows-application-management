using MilOps.Domain.Enums;

namespace MilOps.Application.Security;

/// <summary>
/// Fine-grained permissions. A role maps to a set of permissions via
/// <see cref="RolePermissions"/>.
/// </summary>
[Flags]
public enum Permission
{
    None = 0,

    // Soldiers
    SoldierRead = 1 << 0,
    SoldierWrite = 1 << 1,

    // Guard schedules
    ScheduleRead = 1 << 2,
    ScheduleWrite = 1 << 3,
    ScheduleApprove = 1 << 4,

    // Guard post register
    RegisterRead = 1 << 5,
    RegisterWrite = 1 << 6,

    // Weapons
    WeaponRead = 1 << 7,
    WeaponWrite = 1 << 8,

    // Leaves
    LeaveRead = 1 << 9,
    LeaveWrite = 1 << 10,
    LeaveApprove = 1 << 11,

    // Users & tokens (commander only)
    UserManage = 1 << 12,
    TokenManage = 1 << 13,

    // Reporting & audit
    ReportPrint = 1 << 14,
    AuditRead = 1 << 15,
    DataExport = 1 << 16,

    // Schedule reopen (commander only)
    ScheduleReopen = 1 << 17,

    // Attendance
    AttendanceRead = 1 << 18,
    AttendanceWrite = 1 << 19,
}

/// <summary>Static role-to-permission mapping (RBAC).</summary>
public static class RolePermissions
{
    public static readonly IReadOnlyDictionary<Role, Permission> Map = new Dictionary<Role, Permission>
    {
        // فرمانده: full control, including user/token management.
        [Role.Commander] = Enum.GetValues<Permission>().Aggregate((a, b) => a | b),

        // جانشین: runs day-to-day operations (including approvals and the audit
        // trail) but cannot manage accounts or issue tokens — those stay with
        // the commander.
        [Role.Deputy] =
            Permission.SoldierRead | Permission.SoldierWrite |
            Permission.ScheduleRead | Permission.ScheduleWrite | Permission.ScheduleApprove |
            Permission.RegisterRead | Permission.RegisterWrite |
            Permission.WeaponRead | Permission.WeaponWrite |
            Permission.LeaveRead | Permission.LeaveWrite | Permission.LeaveApprove |
            Permission.ReportPrint | Permission.AuditRead |
            Permission.AttendanceRead | Permission.AttendanceWrite,

        // سرباز: sees the guard schedule and can view/request leaves.
        [Role.Soldier] =
            Permission.ScheduleRead | Permission.LeaveRead | Permission.LeaveWrite
    };

    public static Permission For(Role role) => Map.TryGetValue(role, out var p) ? p : Permission.None;
    public static bool Has(Role role, Permission permission) => (For(role) & permission) == permission;
}
