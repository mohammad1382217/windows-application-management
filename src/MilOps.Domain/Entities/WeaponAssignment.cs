using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// A row in a weapon's assignment history. Open assignment (ReturnedAtUtc null)
/// means the weapon is currently with SoldierId. Child of <see cref="Weapon"/>.
/// </summary>
public class WeaponAssignment : Entity
{
    public int WeaponId { get; private set; }
    public int SoldierId { get; private set; }
    public int IssuedByUserId { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime? ReturnedAtUtc { get; private set; }
    public int? ReceivedByUserId { get; private set; }
    public int? ReturnedAmmunition { get; private set; }
    public string? Note { get; private set; }

    private WeaponAssignment() { } // EF Core

    internal WeaponAssignment(int weaponId, int soldierId, int issuedByUserId, DateTime issuedAtUtc, string? note)
    {
        WeaponId = weaponId;
        SoldierId = soldierId;
        IssuedByUserId = issuedByUserId;
        IssuedAtUtc = issuedAtUtc;
        Note = note;
    }

    internal void Return(int receivedByUserId, DateTime nowUtc, int? returnedAmmo, string? note)
    {
        if (ReturnedAtUtc is not null)
            throw new DomainException("WEAPON_ALREADY_RETURNED", "Weapon already returned.");
        if (returnedAmmo < 0)
            throw new DomainException("WEAPON_AMMO_NEGATIVE", "Returned ammunition cannot be negative.");
        ReturnedAtUtc = nowUtc;
        ReceivedByUserId = receivedByUserId;
        ReturnedAmmunition = returnedAmmo;
        if (!string.IsNullOrWhiteSpace(note)) Note = note;
    }
}
