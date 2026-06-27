namespace MilOps.Domain.Enums;

/// <summary>
/// Application roles. RBAC permissions are derived from roles via
/// <see cref="RolePermissions"/> in the Application layer.
/// </summary>
public enum Role
{
    /// <summary>Full control: user management, token management, all operations.</summary>
    Commander = 1,

    /// <summary>Read/write on operational data; cannot manage users or tokens.</summary>
    Operator = 2,

    /// <summary>Read-only access to permitted modules.</summary>
    ReadOnly = 3
}

/// <summary>Token lifecycle states.</summary>
public enum TokenStatus
{
    /// <summary>Created, not yet used by the holder.</summary>
    Active = 1,
    /// <summary>Consumed (one-time activation completed).</summary>
    Used = 2,
    /// <summary>Explicitly voided by a commander.</summary>
    Revoked = 3,
    /// <summary>Past its expiration date without use.</summary>
    Expired = 4
}

/// <summary>Token purpose scopes what a single-use token may be used for.</summary>
public enum TokenPurpose
{
    /// <summary>Activate a new user account.</summary>
    AccountActivation = 1,
    /// <summary>Register a new soldier/personnel record.</summary>
    Registration = 2,
    /// <summary>Assign or elevate permissions to an existing user.</summary>
    PermissionAssignment = 3
}

public enum HealthType
{
    /// <summary>Fit for all duties including armed guard.</summary>
    Fit = 1,
    /// <summary>Fit for light/support duties.</summary>
    Limited = 2,
    /// <summary>Medical restriction; cannot be assigned to guard duty.</summary>
    Restricted = 3
}

public enum WeaponType
{
    Rifle = 1,
    Pistol = 2,
    MachineGun = 3,
    SubmachineGun = 4,
    Other = 99
}

public enum WeaponStatus
{
    /// <summary>In armory, available for issue.</summary>
    Available = 1,
    /// <summary>Currently issued to a soldier.</summary>
    Assigned = 2,
    /// <summary>Under repair.</summary>
    InRepair = 3,
    /// <summary>Decommissioned / out of service.</summary>
    Decommissioned = 4
}

public enum LeaveStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    Completed = 4,
    Cancelled = 5
}

public enum ShiftNumber
{
    First = 1,
    Second = 2,
    Third = 3
}

public enum ScheduleStatus
{
    Draft = 1,
    Approved = 2,
    Printed = 3
}

/// <summary>Well-known fixed guard posts (mirrors the original LohePosti paper form).</summary>
public enum GuardPost
{
    PostHoursPass = 1,
    Spring = 2,
    Castle = 3,
    Workshop = 4,
    MechanizedGuard = 5,
    AmadGuard = 6,
    ArmedGuard = 7,
    Guard = 8,
    MedicalGuard = 9,
    Kitchen = 10,
    Watchman = 11,
    Armament = 12,
    Refuge = 13,
    ShelterManager = 14,
    ArmedForceMorning = 15
}
