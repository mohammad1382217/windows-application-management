using FluentValidation;
using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Leaves;

public record LeaveDto(int Id, int SoldierId, DateOnly StartDate, DateOnly EndDate,
    LeaveStatus Status, string Reason, int? ApprovedByUserId);

public record ListLeavesQuery(LeaveStatus? Status) : IRequest<IReadOnlyList<LeaveDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.LeaveRead;
}

public record IsSoldierAvailableQuery(int SoldierId, DateOnly Date) : IRequest<bool>;

public record CreateLeaveCommand(int SoldierId, DateOnly StartDate, DateOnly EndDate, string Reason)
    : IRequest<Result<int>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.LeaveWrite;
}

public record ApproveLeaveCommand(int Id) : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.LeaveApprove;
}
public record RejectLeaveCommand(int Id, string Reason) : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.LeaveApprove;
}

public class CreateLeaveValidator : AbstractValidator<CreateLeaveCommand>
{
    public CreateLeaveValidator()
    {
        RuleFor(x => x.SoldierId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class LeaveHandlers :
    IRequestHandler<ListLeavesQuery, IReadOnlyList<LeaveDto>>,
    IRequestHandler<IsSoldierAvailableQuery, bool>,
    IRequestHandler<CreateLeaveCommand, Result<int>>,
    IRequestHandler<ApproveLeaveCommand, Result>,
    IRequestHandler<RejectLeaveCommand, Result>
{
    private readonly IRepository<LeaveRecord> _leaves;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public LeaveHandlers(IRepository<LeaveRecord> leaves, IUnitOfWork uow, ICurrentUser user,
        IDateTime time, IAuditRepository audit)
    { _leaves = leaves; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<IReadOnlyList<LeaveDto>> Handle(ListLeavesQuery q, CancellationToken ct)
    {
        var spec = new LeaveListSpec(q.Status);
        var items = await _leaves.ListAsync(spec, ct);
        return items.Select(Map).ToList();
    }

    public async Task<bool> Handle(IsSoldierAvailableQuery q, CancellationToken ct)
    {
        // A soldier is unavailable on a date if they have an approved leave covering it.
        var spec = new SoldierLeaveOnDateSpec(q.SoldierId, q.Date);
        var blocking = await _leaves.AnyAsync(spec, ct);
        return !blocking;
    }

    public async Task<Result<int>> Handle(CreateLeaveCommand c, CancellationToken ct)
    {
        try
        {
            var leave = LeaveRecord.Create(c.SoldierId, c.StartDate, c.EndDate, c.Reason);
            leave.CreatedBy = _user.Username;
            _leaves.Add(leave);
            await _uow.SaveChangesAsync(ct);
            await _audit.AppendAsync(AuditAction.LeaveCreated, _user.UserId, _user.Username,
                nameof(LeaveRecord), leave.Id.ToString(),
                $"Leave for soldier {c.SoldierId} ({c.StartDate:O}..{c.EndDate:O})", ct);
            return Result.Success(leave.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(ApproveLeaveCommand c, CancellationToken ct)
    {
        var l = await _leaves.GetByIdAsync(c.Id, ct);
        if (l is null) return Result.Failure("NOT_FOUND", "مرخصی یافت نشد.");
        try
        {
            l.Approve(_user.UserId ?? 0, _time.UtcNow);
            l.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);
            await _audit.AppendAsync(AuditAction.LeaveApproved, _user.UserId, _user.Username,
                nameof(LeaveRecord), l.Id.ToString(), "Leave approved", ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(RejectLeaveCommand c, CancellationToken ct)
    {
        var l = await _leaves.GetByIdAsync(c.Id, ct);
        if (l is null) return Result.Failure("NOT_FOUND", "مرخصی یافت نشد.");
        try
        {
            l.Reject(_user.UserId ?? 0, _time.UtcNow, c.Reason);
            l.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);
            await _audit.AppendAsync(AuditAction.LeaveRejected, _user.UserId, _user.Username,
                nameof(LeaveRecord), l.Id.ToString(), $"Rejected: {c.Reason}", ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    private static LeaveDto Map(LeaveRecord l) => new(l.Id, l.SoldierId, l.StartDate, l.EndDate,
        l.Status, l.Reason, l.ApprovedByUserId);
}

internal sealed class LeaveListSpec : Specification<LeaveRecord>
{
    public LeaveListSpec(LeaveStatus? status)
    {
        if (status.HasValue) Criteria = l => l.Status == status.Value;
        OrderByDescending = l => l.Id;
    }
}

internal sealed class SoldierLeaveOnDateSpec : Specification<LeaveRecord>
{
    public SoldierLeaveOnDateSpec(int soldierId, DateOnly date)
        => Criteria = l => l.SoldierId == soldierId && l.Status == LeaveStatus.Approved
                        && date >= l.StartDate && date <= l.EndDate;
}
