using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.ValueObjects;

namespace MilOps.Domain.Entities;

/// <summary>
/// A soldier/personnel record. Aggregate root.
/// Value objects (<see cref="PersonName"/>, <see cref="NationalCode"/>,
/// <see cref="PersonnelCode"/>) enforce their own invariants on construction.
/// </summary>
public class Soldier : AuditableEntity
{
    public PersonName FirstName { get; private set; } = null!;
    public PersonName LastName { get; private set; } = null!;
    public PersonName? FatherName { get; private set; }
    public string Rank { get; private set; } = string.Empty;
    public NationalCode NationalCode { get; private set; } = null!;
    public PersonnelCode PersonnelCode { get; private set; } = null!;
    public HealthType HealthType { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public DateOnly ServiceStartDate { get; private set; }
    public DateOnly ServiceEndDate { get; private set; }
    public string DepartmentName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Soldier() { } // EF Core

    public static Soldier Create(
        PersonName firstName, PersonName lastName, PersonName? fatherName,
        string rank, NationalCode nationalCode, PersonnelCode personnelCode,
        HealthType healthType, DateOnly entryDate, DateOnly serviceStartDate,
        DateOnly serviceEndDate, string departmentName, bool isActive = true)
    {
        if (serviceEndDate <= serviceStartDate)
            throw new DomainException("SOLDIER_DATE_RANGE",
                "Service end date must be after service start date.");

        var soldier = new Soldier
        {
            FirstName = firstName,
            LastName = lastName,
            FatherName = fatherName,
            Rank = rank?.Trim() ?? string.Empty,
            NationalCode = nationalCode,
            PersonnelCode = personnelCode,
            HealthType = healthType,
            EntryDate = entryDate,
            ServiceStartDate = serviceStartDate,
            ServiceEndDate = serviceEndDate,
            DepartmentName = departmentName?.Trim() ?? string.Empty,
            IsActive = isActive
        };
        return soldier;
    }

    public void Update(
        PersonName firstName, PersonName lastName, PersonName? fatherName,
        string rank, HealthType healthType, DateOnly entryDate,
        DateOnly serviceStartDate, DateOnly serviceEndDate, string departmentName,
        bool isActive)
    {
        if (serviceEndDate <= serviceStartDate)
            throw new DomainException("SOLDIER_DATE_RANGE",
                "Service end date must be after service start date.");

        FirstName = firstName;
        LastName = lastName;
        FatherName = fatherName;
        Rank = rank?.Trim() ?? string.Empty;
        HealthType = healthType;
        EntryDate = entryDate;
        ServiceStartDate = serviceStartDate;
        ServiceEndDate = serviceEndDate;
        DepartmentName = departmentName?.Trim() ?? string.Empty;
        IsActive = isActive;
    }

    /// <summary>Whether this soldier may be assigned to guard duty right now.</summary>
    public bool CanGuard() => IsActive && HealthType != HealthType.Restricted;

    public string FullName() => $"{FirstName} {LastName}".Trim();
}
