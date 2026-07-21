using MediatR;
using MilOps.Application.Attendance;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Soldiers;

public record SoldierGuardHistoryDto(DateOnly Date, GuardPost Post, ShiftNumber Shift,
    TimeOnly? ShiftStart, TimeOnly? ShiftEnd);

public record SoldierLeaveHistoryDto(int Id, DateOnly StartDate, DateOnly EndDate,
    LeaveStatus Status, string Reason);

public record SoldierAttendanceHistoryDto(DateOnly Date, AttendanceStatus Status, string? Reason);

public record SoldierDepartmentHistoryDto(string DepartmentName, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record SoldierFullReportDto(
    SoldierDto Soldier,
    IReadOnlyList<SoldierDepartmentHistoryDto> DepartmentHistory,
    IReadOnlyList<SoldierGuardHistoryDto> GuardAssignments,
    IReadOnlyList<SoldierLeaveHistoryDto> Leaves,
    IReadOnlyList<SoldierAttendanceHistoryDto> Attendance,
    DateOnly? From,
    DateOnly? To);

public record GetSoldierFullReportQuery(int SoldierId, DateOnly? From, DateOnly? To)
    : IRequest<Result<SoldierFullReportDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.ReportPrint;
}

public class SoldierReportHandlers : IRequestHandler<GetSoldierFullReportQuery, Result<SoldierFullReportDto>>
{
    private readonly IRepository<Soldier> _soldiers;
    private readonly IRepository<DepartmentHistory> _deptHistory;
    private readonly IRepository<GuardAssignment> _guardAssignments;
    private readonly IRepository<GuardSchedule> _schedules;
    private readonly IRepository<LeaveRecord> _leaves;
    private readonly IRepository<AttendanceRecord> _attendance;

    public SoldierReportHandlers(IRepository<Soldier> soldiers, IRepository<DepartmentHistory> deptHistory,
        IRepository<GuardAssignment> guardAssignments, IRepository<GuardSchedule> schedules,
        IRepository<LeaveRecord> leaves, IRepository<AttendanceRecord> attendance)
    {
        _soldiers = soldiers; _deptHistory = deptHistory; _guardAssignments = guardAssignments;
        _schedules = schedules; _leaves = leaves; _attendance = attendance;
    }

    public async Task<Result<SoldierFullReportDto>> Handle(GetSoldierFullReportQuery q, CancellationToken ct)
    {
        var soldier = await _soldiers.GetByIdAsync(q.SoldierId, ct);
        if (soldier is null) return Result.Failure<SoldierFullReportDto>("NOT_FOUND", "سرباز یافت نشد.");

        var deptHistory = (await _deptHistory.ListAsync(new DepartmentHistoryBySoldierSpec(q.SoldierId), ct))
            .OrderBy(h => h.EffectiveFrom)
            .Select(h => new SoldierDepartmentHistoryDto(h.DepartmentName, h.EffectiveFrom, h.EffectiveTo))
            .ToList();

        var assignments = await _guardAssignments.ListAsync(new GuardAssignmentsBySoldierSpec(q.SoldierId), ct);
        var scheduleIds = assignments.Select(a => a.GuardScheduleId).Distinct().ToList();
        var scheduleDates = scheduleIds.Count == 0
            ? new Dictionary<int, DateOnly>()
            : (await _schedules.ListAsync(new ScheduleDatesByIdsSpec(scheduleIds), ct))
                .ToDictionary(s => s.Id, s => s.Date);

        var guardHistory = assignments
            .Where(a => scheduleDates.ContainsKey(a.GuardScheduleId))
            .Select(a => new SoldierGuardHistoryDto(
                scheduleDates[a.GuardScheduleId], a.Post, a.Shift, a.ShiftStart, a.ShiftEnd))
            .Where(g => (!q.From.HasValue || g.Date >= q.From.Value) && (!q.To.HasValue || g.Date <= q.To.Value))
            .OrderBy(g => g.Date)
            .ToList();

        var leaves = (await _leaves.ListAsync(new LeavesBySoldierRangeSpec(q.SoldierId, q.From, q.To), ct))
            .Select(l => new SoldierLeaveHistoryDto(l.Id, l.StartDate, l.EndDate, l.Status, l.Reason))
            .ToList();

        var attendance = (await _attendance.ListAsync(
                new SoldierReportAttendanceRangeSpec(q.SoldierId, q.From, q.To), ct))
            .Select(a => new SoldierAttendanceHistoryDto(a.Date, a.Status, a.Reason))
            .ToList();

        var soldierDto = new SoldierDto(
            soldier.Id, soldier.FirstName, soldier.LastName, soldier.FatherName, soldier.Rank,
            soldier.NationalCode, soldier.PersonnelCode, soldier.HealthType,
            soldier.EntryDate, soldier.ServiceStartDate, soldier.ServiceEndDate,
            soldier.DepartmentName, soldier.IsActive, soldier.CanGuard(), soldier.CreatedAtUtc);

        return Result.Success(new SoldierFullReportDto(
            soldierDto, deptHistory, guardHistory, leaves, attendance, q.From, q.To));
    }
}

internal sealed class DepartmentHistoryBySoldierSpec : Specification<DepartmentHistory>
{
    public DepartmentHistoryBySoldierSpec(int soldierId) => Criteria = h => h.SoldierId == soldierId;
}

internal sealed class GuardAssignmentsBySoldierSpec : Specification<GuardAssignment>
{
    public GuardAssignmentsBySoldierSpec(int soldierId) => Criteria = a => a.SoldierId == soldierId;
}

internal sealed class ScheduleDatesByIdsSpec : Specification<GuardSchedule>
{
    public ScheduleDatesByIdsSpec(IReadOnlyCollection<int> ids) => Criteria = s => ids.Contains(s.Id);
}

internal sealed class LeavesBySoldierRangeSpec : Specification<LeaveRecord>
{
    public LeavesBySoldierRangeSpec(int soldierId, DateOnly? from, DateOnly? to)
    {
        Criteria = (from, to) switch
        {
            ({ } f, { } t) => l => l.SoldierId == soldierId && l.StartDate <= t && l.EndDate >= f,
            ({ } f, null) => l => l.SoldierId == soldierId && l.EndDate >= f,
            (null, { } t) => l => l.SoldierId == soldierId && l.StartDate <= t,
            _ => l => l.SoldierId == soldierId
        };
        OrderBy = l => l.StartDate;
    }
}

internal sealed class SoldierReportAttendanceRangeSpec : Specification<AttendanceRecord>
{
    public SoldierReportAttendanceRangeSpec(int soldierId, DateOnly? from, DateOnly? to)
    {
        Criteria = (from, to) switch
        {
            ({ } f, { } t) => a => a.SoldierId == soldierId && a.Date >= f && a.Date <= t,
            ({ } f, null) => a => a.SoldierId == soldierId && a.Date >= f,
            (null, { } t) => a => a.SoldierId == soldierId && a.Date <= t,
            _ => a => a.SoldierId == soldierId
        };
        OrderBy = a => a.Date;
    }
}
