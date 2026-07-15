using MilOps.Domain.Enums;

namespace MilOps.Application.Soldiers;

/// <summary>Read model for a soldier (never exposes internal entity state).</summary>
public record SoldierDto(
    int Id,
    string FirstName,
    string LastName,
    string? FatherName,
    string Rank,
    string NationalCode,
    string PersonnelCode,
    HealthType HealthType,
    DateOnly EntryDate,
    DateOnly ServiceStartDate,
    DateOnly ServiceEndDate,
    string DepartmentName,
    bool IsActive,
    bool CanGuard,
    DateTime CreatedAtUtc)
{
    /// <summary>Human-readable label for pickers (guard schedule builder, etc.).</summary>
    public string DisplayName => $"{LastName} {FirstName} — {Rank} ({PersonnelCode})";
}

/// <summary>Filter/sort parameters for soldier queries.</summary>
public record SoldierSearchRequest(
    string? Search,
    HealthType? HealthType,
    bool? IsActive,
    string? Department,
    int Page = 1,
    int PageSize = 50);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
