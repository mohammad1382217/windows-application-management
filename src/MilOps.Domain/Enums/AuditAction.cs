namespace MilOps.Domain.Enums;

/// <summary>Audit actions recorded by the system (see requirements list).</summary>
public enum AuditAction
{
    Login = 1,
    Logout = 2,
    LoginFailed = 3,
    UserCreated = 4,
    UserUpdated = 5,
    UserDeactivated = 6,
    PasswordChanged = 7,
    TokenGenerated = 8,
    TokenRevoked = 9,
    TokenUsed = 10,
    SoldierCreated = 11,
    SoldierUpdated = 12,
    SoldierDeleted = 13,
    ScheduleCreated = 14,
    ScheduleUpdated = 15,
    ScheduleApproved = 16,
    RegisterEntryCreated = 17,
    WeaponIssued = 18,
    WeaponReturned = 19,
    LeaveCreated = 20,
    LeaveApproved = 21,
    LeaveRejected = 22,
    ReportPrinted = 23,
    DataExported = 24,
    ConfigChanged = 25,
    DbUnlocked = 26,
    Other = 99
}
