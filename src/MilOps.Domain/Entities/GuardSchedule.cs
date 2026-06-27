using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.ValueObjects;

namespace MilOps.Domain.Entities;

/// <summary>
/// A single day's guard board ("Lohe Posti"). An aggregate root composed of
/// <see cref="GuardAssignment"/> entries (one per post per shift). This models
/// the original paper form (3 shifts × multiple named posts) in a normalized,
/// queryable way instead of 40+ flat string columns.
/// </summary>
public class GuardSchedule : AuditableEntity
{
    private readonly List<GuardAssignment> _assignments = new();
    public IReadOnlyCollection<GuardAssignment> Assignments => _assignments;

    public DateOnly Date { get; private set; }
    public ScheduleStatus Status { get; private set; }
    public int? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? Remarks { get; private set; }

    // Fixed extra duty cells carried over from the paper form.
    public string? ArmedForceMorning1 { get; private set; }
    public string? ArmedForceMorning2 { get; private set; }
    public string? ArmedForceMorning3 { get; private set; }
    public string? Watchman { get; private set; }
    public string? Armament { get; private set; }
    public string? Refuge { get; private set; }
    public string? ShelterManager { get; private set; }

    private GuardSchedule() { } // EF Core

    public static GuardSchedule Create(DateOnly date, string? remarks = null)
    {
        return new GuardSchedule
        {
            Date = date,
            Status = ScheduleStatus.Draft,
            Remarks = remarks
        };
    }

    /// <summary>
    /// Assign a soldier to a post for a shift. One soldier may not occupy the
    /// same post+shift twice; a soldier may not be on two posts in the same shift.
    /// </summary>
    public GuardAssignment Assign(
        int soldierId, GuardPost post, ShiftNumber shift,
        TimeRange? shiftHours = null, string? note = null)
    {
        if (_assignments.Any(a => a.SoldierId == soldierId && a.Shift == shift))
            throw new DomainException("SCHEDULE_DOUBLE_SHIFT",
                "A soldier cannot be assigned to two posts in the same shift.");
        if (_assignments.Any(a => a.Post == post && a.Shift == shift))
            throw new DomainException("SCHEDULE_POST_FILLED",
                "That post is already assigned for this shift.");

        var assignment = new GuardAssignment(Id, soldierId, post, shift, shiftHours, note);
        _assignments.Add(assignment);
        return assignment;
    }

    public void RemoveAssignment(int assignmentId) =>
        _assignments.RemoveAll(a => a.Id == assignmentId);

    public void SetExtraDuty(string? armedForceMorning1, string? armedForceMorning2,
        string? armedForceMorning3, string? watchman, string? armament,
        string? refuge, string? shelterManager)
    {
        ArmedForceMorning1 = armedForceMorning1;
        ArmedForceMorning2 = armedForceMorning2;
        ArmedForceMorning3 = armedForceMorning3;
        Watchman = watchman;
        Armament = armament;
        Refuge = refuge;
        ShelterManager = shelterManager;
    }

    public void Approve(int userId, DateTime nowUtc)
    {
        if (Status == ScheduleStatus.Approved)
            throw new DomainException("SCHEDULE_ALREADY_APPROVED", "Schedule is already approved.");
        if (_assignments.Count == 0)
            throw new DomainException("SCHEDULE_EMPTY", "Cannot approve an empty schedule.");
        Status = ScheduleStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAtUtc = nowUtc;
    }

    public void MarkPrinted() => Status = ScheduleStatus.Printed;
}
