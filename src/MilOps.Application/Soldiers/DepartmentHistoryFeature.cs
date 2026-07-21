using FluentValidation;
using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Soldiers;

/// <summary>Changes a soldier's department, closing the old history period and opening a new one.</summary>
public record ChangeSoldierDepartmentCommand(int SoldierId, string NewDepartmentName)
    : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.SoldierWrite;
}

public class ChangeSoldierDepartmentValidator : AbstractValidator<ChangeSoldierDepartmentCommand>
{
    public ChangeSoldierDepartmentValidator()
    {
        RuleFor(x => x.SoldierId).GreaterThan(0);
        RuleFor(x => x.NewDepartmentName).NotEmpty().MaximumLength(80);
    }
}

public class DepartmentHistoryHandlers : IRequestHandler<ChangeSoldierDepartmentCommand, Result>
{
    private readonly IRepository<Soldier> _soldiers;
    private readonly IRepository<DepartmentHistory> _deptHistory;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public DepartmentHistoryHandlers(IRepository<Soldier> soldiers, IRepository<DepartmentHistory> deptHistory,
        IUnitOfWork uow, ICurrentUser user, IDateTime time, IAuditRepository audit)
    { _soldiers = soldiers; _deptHistory = deptHistory; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<Result> Handle(ChangeSoldierDepartmentCommand c, CancellationToken ct)
    {
        var soldier = await _soldiers.GetByIdAsync(c.SoldierId, ct);
        if (soldier is null) return Result.Failure("NOT_FOUND", "سرباز یافت نشد.");

        var oldDepartment = soldier.DepartmentName;
        var today = DateOnly.FromDateTime(_time.UtcNow);

        try
        {
            soldier.ChangeDepartment(c.NewDepartmentName);
            soldier.Touch(_user.Username);

            var openRow = await _deptHistory.FirstOrDefaultAsync(
                new OpenDepartmentHistorySpec(c.SoldierId), ct);
            openRow?.Close(today);

            var newRow = DepartmentHistory.Open(c.SoldierId, c.NewDepartmentName, today);
            newRow.CreatedBy = _user.Username;
            _deptHistory.Add(newRow);

            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.DepartmentChanged, _user.UserId, _user.Username,
                nameof(Soldier), soldier.Id.ToString(),
                $"تغییر بخش {soldier.FullName()} از «{oldDepartment}» به «{c.NewDepartmentName}»", ct);

            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }
}

internal sealed class OpenDepartmentHistorySpec : Specification<DepartmentHistory>
{
    public OpenDepartmentHistorySpec(int soldierId)
        => Criteria = h => h.SoldierId == soldierId && h.EffectiveTo == null;
}
