using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// Manual daily roll-call entry for one soldier on one date (one row per
/// soldier+date — re-recording the same date updates the existing row rather
/// than creating a duplicate). "On leave" is intentionally NOT a stored status
/// here; it is derived at report time from <see cref="LeaveRecord.IsActiveOn"/>
/// so the two facts cannot drift apart.
/// </summary>
public class AttendanceRecord : AuditableEntity
{
    public int SoldierId { get; private set; }
    public DateOnly Date { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public int RecordedByUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    private AttendanceRecord() { } // EF Core

    public static AttendanceRecord Create(int soldierId, DateOnly date, AttendanceStatus status,
        string? reason, int recordedByUserId, DateTime recordedAtUtc)
    {
        EnsureReason(status, reason);
        return new AttendanceRecord
        {
            SoldierId = soldierId,
            Date = date,
            Status = status,
            Reason = reason?.Trim(),
            RecordedByUserId = recordedByUserId,
            RecordedAtUtc = recordedAtUtc
        };
    }

    /// <summary>Re-records this soldier's attendance for the same date (upsert target).</summary>
    public void Update(AttendanceStatus status, string? reason, int recordedByUserId, DateTime recordedAtUtc)
    {
        EnsureReason(status, reason);
        Status = status;
        Reason = reason?.Trim();
        RecordedByUserId = recordedByUserId;
        RecordedAtUtc = recordedAtUtc;
    }

    private static void EnsureReason(AttendanceStatus status, string? reason)
    {
        if (status is AttendanceStatus.Absent or AttendanceStatus.Late && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("ATTENDANCE_REASON_REQUIRED",
                "Reason is required for absence or lateness.");
    }
}
