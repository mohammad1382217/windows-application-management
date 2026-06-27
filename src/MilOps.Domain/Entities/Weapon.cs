using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// A weapon in the unit armory. Aggregate root with an assignment-history
/// collection. Current assignment is derived from the latest open history row.
/// </summary>
public class Weapon : AuditableEntity
{
    private readonly List<WeaponAssignment> _history = new();
    public IReadOnlyCollection<WeaponAssignment> History => _history;

    public string WeaponNumber { get; private set; } = string.Empty;
    public WeaponType Type { get; private set; }
    public WeaponStatus Status { get; private set; }
    public string? Model { get; private set; }
    public int? CurrentlyAssignedSoldierId => _history.FirstOrDefault(h => h.ReturnedAtUtc == null)?.SoldierId;

    private Weapon() { } // EF Core

    public static Weapon Create(string weaponNumber, WeaponType type,
        WeaponStatus status = WeaponStatus.Available, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(weaponNumber))
            throw new DomainException("WEAPON_NUMBER_REQUIRED", "Weapon number is required.");
        return new Weapon
        {
            WeaponNumber = weaponNumber.Trim().ToUpperInvariant(),
            Type = type,
            Status = status,
            Model = model?.Trim()
        };
    }

    /// <summary>Issue the weapon to a soldier, opening a history row.</summary>
    public WeaponAssignment IssueTo(int soldierId, int issuedByUserId, DateTime nowUtc, string? note = null)
    {
        if (CurrentlyAssignedSoldierId is not null)
            throw new DomainException("WEAPON_ALREADY_ISSUED", "Weapon is already issued. Return it first.");
        if (Status is WeaponStatus.InRepair or WeaponStatus.Decommissioned)
            throw new DomainException("WEAPON_UNAVAILABLE", "Weapon is not available for issue.");

        var assignment = new WeaponAssignment(Id, soldierId, issuedByUserId, nowUtc, note);
        _history.Add(assignment);
        Status = WeaponStatus.Assigned;
        return assignment;
    }

    /// <summary>Return the weapon from its current holder.</summary>
    public void Return(int receivedByUserId, DateTime nowUtc, int? returnedAmmo = null, string? note = null)
    {
        var open = _history.FirstOrDefault(h => h.ReturnedAtUtc == null)
            ?? throw new DomainException("WEAPON_NOT_ISSUED", "Weapon is not currently issued.");
        open.Return(receivedByUserId, nowUtc, returnedAmmo, note);
        Status = WeaponStatus.Available;
    }

    public void SetStatus(WeaponStatus status) => Status = status;
}
