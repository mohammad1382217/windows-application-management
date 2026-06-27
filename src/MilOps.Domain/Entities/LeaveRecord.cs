using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// A leave (مرخصی) request for a soldier. Overlapping active leaves for the
/// same soldier are forbidden by the application layer; the domain enforces
/// basic date validity here.
/// </summary>
public class LeaveRecord : AuditableEntity
{
    public int SoldierId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public LeaveStatus Status { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public int? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    private LeaveRecord() { } // EF Core

    public static LeaveRecord Create(int soldierId, DateOnly startDate, DateOnly endDate, string reason)
    {
        if (endDate < startDate)
            throw new DomainException("LEAVE_DATE_RANGE", "Leave end date cannot be before start date.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("LEAVE_REASON_REQUIRED", "Leave reason is required.");

        return new LeaveRecord
        {
            SoldierId = soldierId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = reason.Trim(),
            Status = LeaveStatus.Requested
        };
    }

    public void Approve(int userId, DateTime nowUtc)
    {
        if (Status != LeaveStatus.Requested)
            throw new DomainException("LEAVE_NOT_REQUESTED", "Only requested leaves can be approved.");
        Status = LeaveStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAtUtc = nowUtc;
    }

    public void Reject(int userId, DateTime nowUtc, string reason)
    {
        if (Status != LeaveStatus.Requested)
            throw new DomainException("LEAVE_NOT_REQUESTED", "Only requested leaves can be rejected.");
        Status = LeaveStatus.Rejected;
        ApprovedByUserId = userId;
        ApprovedAtUtc = nowUtc;
        RejectionReason = reason;
    }

    public void Cancel()
    {
        if (Status is LeaveStatus.Completed or LeaveStatus.Cancelled)
            throw new DomainException("LEAVE_NOT_CANCELLABLE", "This leave cannot be cancelled.");
        Status = LeaveStatus.Cancelled;
    }

    /// <summary>True if this leave covers the given date.</summary>
    public bool IsActiveOn(DateOnly date) =>
        Status == LeaveStatus.Approved && date >= StartDate && date <= EndDate;
}
