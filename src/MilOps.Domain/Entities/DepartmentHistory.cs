using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// One period a soldier spent in a given department. Exactly one row per
/// soldier is "open" (EffectiveTo == null) at any time; changing department
/// closes the open row and opens a new one (see ChangeSoldierDepartmentCommand).
/// </summary>
public class DepartmentHistory : AuditableEntity
{
    public int SoldierId { get; private set; }
    public string DepartmentName { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }

    private DepartmentHistory() { } // EF Core

    public static DepartmentHistory Open(int soldierId, string departmentName, DateOnly effectiveFrom)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            throw new DomainException("DEPARTMENT_NAME_REQUIRED", "Department name is required.");
        return new DepartmentHistory
        {
            SoldierId = soldierId,
            DepartmentName = departmentName.Trim(),
            EffectiveFrom = effectiveFrom
        };
    }

    public void Close(DateOnly effectiveTo)
    {
        if (EffectiveTo is not null)
            throw new DomainException("DEPARTMENT_HISTORY_ALREADY_CLOSED",
                "This department period is already closed.");
        if (effectiveTo < EffectiveFrom)
            throw new DomainException("DEPARTMENT_HISTORY_DATE_RANGE",
                "End date cannot be before the start date.");
        EffectiveTo = effectiveTo;
    }
}
