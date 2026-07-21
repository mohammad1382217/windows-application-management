using FluentValidation;
using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Attendance;

public record AttendanceRecordDto(int Id, int SoldierId, DateOnly Date, AttendanceStatus Status,
    string? Reason, int RecordedByUserId, DateTime RecordedAtUtc, string? SoldierName = null);

/// <summary>Upserts a soldier's attendance for a given date (one row per soldier+date).</summary>
public record RecordAttendanceCommand(int SoldierId, DateOnly Date, AttendanceStatus Status, string? Reason)
    : IRequest<Result<int>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.AttendanceWrite;
}

/// <summary>Daily roll-call view.</summary>
public record ListAttendanceByDateQuery(DateOnly Date)
    : IRequest<IReadOnlyList<AttendanceRecordDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.AttendanceRead;
}

/// <summary>Per-soldier history across an optional range (feeds the full report).</summary>
public record ListAttendanceBySoldierQuery(int SoldierId, DateOnly? From, DateOnly? To)
    : IRequest<IReadOnlyList<AttendanceRecordDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.AttendanceRead;
}

public class RecordAttendanceValidator : AbstractValidator<RecordAttendanceCommand>
{
    public RecordAttendanceValidator()
    {
        RuleFor(x => x.SoldierId).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Reason).NotEmpty()
            .When(x => x.Status is AttendanceStatus.Absent or AttendanceStatus.Late)
            .WithMessage("در صورت غیبت یا تأخیر، ذکر دلیل الزامی است.");
    }
}

public class AttendanceHandlers :
    IRequestHandler<RecordAttendanceCommand, Result<int>>,
    IRequestHandler<ListAttendanceByDateQuery, IReadOnlyList<AttendanceRecordDto>>,
    IRequestHandler<ListAttendanceBySoldierQuery, IReadOnlyList<AttendanceRecordDto>>
{
    private readonly IRepository<AttendanceRecord> _attendance;
    private readonly IRepository<Soldier> _soldiers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public AttendanceHandlers(IRepository<AttendanceRecord> attendance, IRepository<Soldier> soldiers,
        IUnitOfWork uow, ICurrentUser user, IDateTime time, IAuditRepository audit)
    { _attendance = attendance; _soldiers = soldiers; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<Result<int>> Handle(RecordAttendanceCommand c, CancellationToken ct)
    {
        var existing = await _attendance.FirstOrDefaultAsync(
            new AttendanceBySoldierAndDateSpec(c.SoldierId, c.Date), ct);
        try
        {
            var now = _time.UtcNow;
            if (existing is not null)
            {
                existing.Update(c.Status, c.Reason, _user.UserId ?? 0, now);
                existing.Touch(_user.Username);
            }
            else
            {
                existing = AttendanceRecord.Create(c.SoldierId, c.Date, c.Status, c.Reason, _user.UserId ?? 0, now);
                existing.CreatedBy = _user.Username;
                _attendance.Add(existing);
            }
            await _uow.SaveChangesAsync(ct);

            var soldierName = (await _soldiers.ListAsync(new SoldiersByIdsSpec(new[] { c.SoldierId }), ct))
                .Select(s => s.FullName()).FirstOrDefault() ?? $"#{c.SoldierId}";
            await _audit.AppendAsync(AuditAction.AttendanceRecorded, _user.UserId, _user.Username,
                nameof(AttendanceRecord), existing.Id.ToString(),
                $"ثبت حضور‌وغیاب {soldierName} در {c.Date:yyyy/MM/dd}: {EnumDescriptions.Describe(c.Status)}", ct);

            return Result.Success(existing.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Code, ex.Message); }
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(ListAttendanceByDateQuery q, CancellationToken ct)
    {
        var items = await _attendance.ListAsync(new AttendanceByDateSpec(q.Date), ct);
        return await MapAsync(items, ct);
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> Handle(ListAttendanceBySoldierQuery q, CancellationToken ct)
    {
        var items = await _attendance.ListAsync(new AttendanceBySoldierRangeSpec(q.SoldierId, q.From, q.To), ct);
        return await MapAsync(items, ct);
    }

    private async Task<IReadOnlyList<AttendanceRecordDto>> MapAsync(
        IReadOnlyList<AttendanceRecord> items, CancellationToken ct)
    {
        var ids = items.Select(a => a.SoldierId).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<int, string>()
            : (await _soldiers.ListAsync(new SoldiersByIdsSpec(ids), ct))
                .ToDictionary(s => s.Id, s => s.FullName());

        return items.Select(a => new AttendanceRecordDto(
            a.Id, a.SoldierId, a.Date, a.Status, a.Reason, a.RecordedByUserId, a.RecordedAtUtc,
            names.GetValueOrDefault(a.SoldierId))).ToList();
    }
}

internal sealed class AttendanceBySoldierAndDateSpec : Specification<AttendanceRecord>
{
    public AttendanceBySoldierAndDateSpec(int soldierId, DateOnly date)
        => Criteria = a => a.SoldierId == soldierId && a.Date == date;
}

internal sealed class AttendanceByDateSpec : Specification<AttendanceRecord>
{
    public AttendanceByDateSpec(DateOnly date)
    {
        Criteria = a => a.Date == date;
        OrderBy = a => a.SoldierId;
    }
}

internal sealed class AttendanceBySoldierRangeSpec : Specification<AttendanceRecord>
{
    public AttendanceBySoldierRangeSpec(int soldierId, DateOnly? from, DateOnly? to)
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

internal sealed class SoldiersByIdsSpec : Specification<Soldier>
{
    public SoldiersByIdsSpec(IReadOnlyCollection<int> ids) => Criteria = s => ids.Contains(s.Id);
}
