using FluentValidation;
using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;
using MilOps.Domain.ValueObjects;

namespace MilOps.Application.Schedules;

// SoldierName is resolved server-side for display; it is never required when
// building a write request (CreateScheduleCommand only needs SoldierId).
public record GuardAssignmentDto(int SoldierId, GuardPost Post, ShiftNumber Shift,
    TimeOnly? ShiftStart, TimeOnly? ShiftEnd, string? Note, string? SoldierName = null)
{
    /// <summary>Display text for the board grid: resolved name, or the raw ID as a fallback.</summary>
    public string SoldierDisplay => SoldierName ?? $"#{SoldierId}";
}

public record GuardScheduleDto(
    int Id, DateOnly Date, ScheduleStatus Status,
    int? ApprovedByUserId, DateTime? ApprovedAtUtc, string? Remarks,
    IReadOnlyList<GuardAssignmentDto> Assignments);

/// <summary>Lightweight row for the schedule picker list (no assignment detail).</summary>
public record GuardScheduleSummaryDto(int Id, DateOnly Date, ScheduleStatus Status, int AssignmentCount);

public record GetScheduleByDateQuery(DateOnly Date) : IRequest<GuardScheduleDto?>;
public record GetScheduleByIdQuery(int Id) : IRequest<GuardScheduleDto?>;

/// <summary>Recent schedules for the picker list, newest first.</summary>
public record ListSchedulesQuery(DateOnly? From = null, DateOnly? To = null, int Take = 100)
    : IRequest<IReadOnlyList<GuardScheduleSummaryDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.ScheduleRead;
}

public record CreateScheduleCommand(
    DateOnly Date, string? Remarks, IReadOnlyList<GuardAssignmentDto> Assignments)
    : IRequest<Result<int>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.ScheduleWrite;
}

public record ApproveScheduleCommand(int Id) : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.ScheduleApprove;
}

public class CreateScheduleValidator : AbstractValidator<CreateScheduleCommand>
{
    public CreateScheduleValidator()
    {
        RuleFor(x => x.Assignments).NotNull();
        RuleForEach(x => x.Assignments).ChildRules(a =>
        {
            a.RuleFor(x => x.SoldierId).GreaterThan(0);
        });
    }
}

public class ScheduleHandlers :
    IRequestHandler<GetScheduleByDateQuery, GuardScheduleDto?>,
    IRequestHandler<GetScheduleByIdQuery, GuardScheduleDto?>,
    IRequestHandler<ListSchedulesQuery, IReadOnlyList<GuardScheduleSummaryDto>>,
    IRequestHandler<CreateScheduleCommand, Result<int>>,
    IRequestHandler<ApproveScheduleCommand, Result>
{
    private readonly IRepository<GuardSchedule> _schedules;
    private readonly IRepository<Soldier> _soldiers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public ScheduleHandlers(IRepository<GuardSchedule> schedules, IRepository<Soldier> soldiers,
        IUnitOfWork uow, ICurrentUser user, IDateTime time, IAuditRepository audit)
    { _schedules = schedules; _soldiers = soldiers; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<GuardScheduleDto?> Handle(GetScheduleByDateQuery q, CancellationToken ct)
    {
        var spec = new ScheduleByDateSpec(q.Date);
        var s = await _schedules.FirstOrDefaultAsync(spec, ct);
        return s is null ? null : await MapAsync(s, ct);
    }

    public async Task<GuardScheduleDto?> Handle(GetScheduleByIdQuery q, CancellationToken ct)
    {
        var s = await _schedules.FirstOrDefaultAsync(new ScheduleByIdSpec(q.Id), ct);
        return s is null ? null : await MapAsync(s, ct);
    }

    public async Task<IReadOnlyList<GuardScheduleSummaryDto>> Handle(ListSchedulesQuery q, CancellationToken ct)
    {
        var spec = new SchedulesInRangeSpec(q.From, q.To, q.Take);
        var items = await _schedules.ListAsync(spec, ct);
        return items.Select(s => new GuardScheduleSummaryDto(s.Id, s.Date, s.Status, s.Assignments.Count)).ToList();
    }

    public async Task<Result<int>> Handle(CreateScheduleCommand c, CancellationToken ct)
    {
        try
        {
            var schedule = GuardSchedule.Create(c.Date, c.Remarks);
            schedule.CreatedBy = _user.Username;
            foreach (var a in c.Assignments)
            {
                var hours = a.ShiftStart is { } st && a.ShiftEnd is { } en
                    ? TimeRange.Create(st, en) : null;
                schedule.Assign(a.SoldierId, a.Post, a.Shift, hours, a.Note);
            }
            _schedules.Add(schedule);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.ScheduleCreated, _user.UserId, _user.Username,
                nameof(GuardSchedule), schedule.Id.ToString(),
                $"Schedule for {c.Date:O} with {c.Assignments.Count} assignments", ct);

            return Result.Success(schedule.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(ApproveScheduleCommand c, CancellationToken ct)
    {
        // Approve() checks the assignments, so they must be loaded.
        var s = await _schedules.FirstOrDefaultAsync(new ScheduleByIdSpec(c.Id), ct);
        if (s is null) return Result.Failure("NOT_FOUND", "برنامه یافت نشد.");
        try
        {
            s.Approve(_user.UserId ?? 0, _time.UtcNow);
            s.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.ScheduleApproved, _user.UserId, _user.Username,
                nameof(GuardSchedule), s.Id.ToString(), $"Approved schedule for {s.Date:O}", ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    /// <summary>Maps the schedule AND resolves soldier names for display.</summary>
    private async Task<GuardScheduleDto> MapAsync(GuardSchedule s, CancellationToken ct)
    {
        var ids = s.Assignments.Select(a => a.SoldierId).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<int, string>()
            : (await _soldiers.ListAsync(new SoldiersByIdsSpec(ids), ct))
                .ToDictionary(sol => sol.Id, sol => sol.FullName());

        return new(
            s.Id, s.Date, s.Status, s.ApprovedByUserId, s.ApprovedAtUtc, s.Remarks,
            s.Assignments.Select(a => new GuardAssignmentDto(
                a.SoldierId, a.Post, a.Shift, a.ShiftStart, a.ShiftEnd, a.Note,
                names.GetValueOrDefault(a.SoldierId))).ToList());
    }
}

// Assignments MUST be eager-loaded: the DTO maps them and Approve() validates
// against them; with a lazy (never-loaded) collection the schedule always looked
// empty outside the scope that created it.
internal sealed class ScheduleByDateSpec : Specification<GuardSchedule>
{
    public ScheduleByDateSpec(DateOnly date)
    {
        Criteria = s => s.Date == date;
        AddInclude(s => s.Assignments);
    }
}

internal sealed class ScheduleByIdSpec : Specification<GuardSchedule>
{
    public ScheduleByIdSpec(int id)
    {
        Criteria = s => s.Id == id;
        AddInclude(s => s.Assignments);
    }
}

internal sealed class SchedulesInRangeSpec : Specification<GuardSchedule>
{
    public SchedulesInRangeSpec(DateOnly? from, DateOnly? to, int take)
    {
        Criteria = (from, to) switch
        {
            ({ } f, { } t) => s => s.Date >= f && s.Date <= t,
            ({ } f, null) => s => s.Date >= f,
            (null, { } t) => s => s.Date <= t,
            _ => null
        };
        AddInclude(s => s.Assignments);
        OrderByDescending = s => s.Date;
        ApplyPaging(0, take);
    }
}

internal sealed class SoldiersByIdsSpec : Specification<Soldier>
{
    public SoldiersByIdsSpec(IReadOnlyCollection<int> ids) => Criteria = s => ids.Contains(s.Id);
}
