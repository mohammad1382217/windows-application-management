using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// A line in the Guard Post Register (دفتر پستی): who took which weapon, when,
/// how much ammunition, signature, remarks. Printable as a register report.
/// </summary>
public class GuardPostRegisterEntry : AuditableEntity
{
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public int SoldierId { get; private set; }
    public GuardPost Post { get; private set; }
    public string WeaponNumber { get; private set; } = string.Empty;
    public int AmmunitionCount { get; private set; }
    public string? Signature { get; private set; }
    public string? Remarks { get; private set; }

    private GuardPostRegisterEntry() { } // EF Core

    public static GuardPostRegisterEntry Create(
        DateOnly date, TimeOnly time, int soldierId, GuardPost post,
        string weaponNumber, int ammunitionCount, string? signature, string? remarks)
    {
        if (ammunitionCount < 0)
            throw new DomainException("REGISTER_AMMO_NEGATIVE",
                "Ammunition count cannot be negative.");
        if (string.IsNullOrWhiteSpace(weaponNumber))
            throw new DomainException("REGISTER_WEAPON_REQUIRED",
                "Weapon number is required.");

        return new GuardPostRegisterEntry
        {
            Date = date,
            Time = time,
            SoldierId = soldierId,
            Post = post,
            WeaponNumber = weaponNumber.Trim(),
            AmmunitionCount = ammunitionCount,
            Signature = signature,
            Remarks = remarks
        };
    }
}
