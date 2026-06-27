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

public record GuardAssignmentDto(int SoldierId, GuardPost Post, ShiftNumber Shift,
    TimeOnly? ShiftStart, TimeOnly? ShiftEnd, string? Note);

public record GuardScheduleDto(
    int Id, DateOnly Date, ScheduleStatus Status,
    int? ApprovedByUserId, DateTime? ApprovedAtUtc, string? Remarks,
    IReadOnlyList<GuardAssignmentDto> Assignments);

public record GetScheduleByDateQuery(DateOnly Date) : IRequest<GuardScheduleDto?>;
public record GetScheduleByIdQuery(int Id) : IRequest<GuardScheduleDto?>;

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
    IRequestHandler<CreateScheduleCommand, Result<int>>,
    IRequestHandler<ApproveScheduleCommand, Result>
{
    private readonly IRepository<GuardSchedule> _schedules;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public ScheduleHandlers(IRepository<GuardSchedule> schedules, IUnitOfWork uow,
        ICurrentUser user, IDateTime time, IAuditRepository audit)
    { _schedules = schedules; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<GuardScheduleDto?> Handle(GetScheduleByDateQuery q, CancellationToken ct)
    {
        var spec = new ScheduleByDateSpec(q.Date);
        var s = await _schedules.FirstOrDefaultAsync(spec, ct);
        return s is null ? null : Map(s);
    }

    public async Task<GuardScheduleDto?> Handle(GetScheduleByIdQuery q, CancellationToken ct)
    {
        var s = await _schedules.GetByIdAsync(q.Id, ct);
        return s is null ? null : Map(s);
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
        var s = await _schedules.GetByIdAsync(c.Id, ct);
        if (s is null) return Result.Failure("NOT_FOUND", "Schedule not found.");
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

    private static GuardScheduleDto Map(GuardSchedule s) => new(
        s.Id, s.Date, s.Status, s.ApprovedByUserId, s.ApprovedAtUtc, s.Remarks,
        s.Assignments.Select(a => new GuardAssignmentDto(
            a.SoldierId, a.Post, a.Shift, a.ShiftStart, a.ShiftEnd, a.Note)).ToList());
}

internal sealed class ScheduleByDateSpec : Specification<GuardSchedule>
{
    public ScheduleByDateSpec(DateOnly date) => Criteria = s => s.Date == date;
}
