using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.ValueObjects;

namespace MilOps.Domain.Entities;

/// <summary>
/// One cell of the daily guard board: a soldier on a specific post for a shift.
/// A child entity of <see cref="GuardSchedule"/>.
/// </summary>
public class GuardAssignment : Entity
{
    public int GuardScheduleId { get; private set; }
    public int SoldierId { get; private set; }
    public GuardPost Post { get; private set; }
    public ShiftNumber Shift { get; private set; }
    public TimeOnly? ShiftStart { get; private set; }
    public TimeOnly? ShiftEnd { get; private set; }
    public string? Note { get; private set; }

    private GuardAssignment() { } // EF Core

    internal GuardAssignment(int scheduleId, int soldierId, GuardPost post,
        ShiftNumber shift, TimeRange? shiftHours, string? note)
    {
        GuardScheduleId = scheduleId;
        SoldierId = soldierId;
        Post = post;
        Shift = shift;
        ShiftStart = shiftHours?.Start;
        ShiftEnd = shiftHours?.End;
        Note = note;
    }

    public string ShiftDisplay() => ShiftStart is { } s && ShiftEnd is { } e
        ? $"{s:HH:mm}-{e:HH:mm}" : Shift.ToString();
}
