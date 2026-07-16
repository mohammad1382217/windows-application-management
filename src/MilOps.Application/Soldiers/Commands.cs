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

namespace MilOps.Application.Soldiers;

// ============================================================
// Commands (write side)
// ============================================================

public record CreateSoldierCommand(
    string FirstName, string LastName, string? FatherName, string Rank,
    string NationalCode, string PersonnelCode, HealthType HealthType,
    DateOnly EntryDate, DateOnly ServiceStartDate, DateOnly ServiceEndDate,
    string DepartmentName, bool IsActive = true)
    : IRequest<Result<int>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.SoldierWrite;
}

public record UpdateSoldierCommand(
    int Id, string FirstName, string LastName, string? FatherName, string Rank,
    HealthType HealthType, DateOnly EntryDate, DateOnly ServiceStartDate,
    DateOnly ServiceEndDate, string DepartmentName, bool IsActive)
    : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.SoldierWrite;
}

public record DeleteSoldierCommand(int Id) : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.SoldierWrite;
}

// ------------------------------------------------------------
// Validators
// ------------------------------------------------------------

public class CreateSoldierValidator : AbstractValidator<CreateSoldierCommand>
{
    public CreateSoldierValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.FatherName).MaximumLength(60).When(x => x.FatherName is not null);
        RuleFor(x => x.Rank).NotEmpty().MaximumLength(40);
        RuleFor(x => x.NationalCode).NotEmpty().Matches("^[0-9]{10}$")
            .WithMessage("کد ملی باید دقیقاً ۱۰ رقم باشد.");
        RuleFor(x => x.PersonnelCode).NotEmpty().MaximumLength(12);
        RuleFor(x => x.DepartmentName).MaximumLength(80);
        RuleFor(x => x.ServiceEndDate).GreaterThan(x => x.ServiceStartDate)
            .WithMessage("تاریخ پایان خدمت باید بعد از تاریخ شروع باشد.");
    }
}

public class UpdateSoldierValidator : AbstractValidator<UpdateSoldierCommand>
{
    public UpdateSoldierValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Rank).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ServiceEndDate).GreaterThan(x => x.ServiceStartDate);
    }
}

// ------------------------------------------------------------
// Handlers
// ------------------------------------------------------------

public class SoldierCommandHandlers :
    IRequestHandler<CreateSoldierCommand, Result<int>>,
    IRequestHandler<UpdateSoldierCommand, Result>,
    IRequestHandler<DeleteSoldierCommand, Result>
{
    private readonly IRepository<Soldier> _soldiers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public SoldierCommandHandlers(IRepository<Soldier> soldiers, IUnitOfWork uow,
        ICurrentUser user, IDateTime time, IAuditRepository audit)
    { _soldiers = soldiers; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<Result<int>> Handle(CreateSoldierCommand c, CancellationToken ct)
    {
        try
        {
            var soldier = Soldier.Create(
                PersonName.Create(c.FirstName, "First name"),
                PersonName.Create(c.LastName, "Last name"),
                c.FatherName is null ? null : PersonName.Create(c.FatherName, "Father name"),
                c.Rank,
                NationalCode.Create(c.NationalCode),
                PersonnelCode.Create(c.PersonnelCode),
                c.HealthType, c.EntryDate, c.ServiceStartDate, c.ServiceEndDate,
                c.DepartmentName, c.IsActive);

            soldier.CreatedBy = _user.Username;
            _soldiers.Add(soldier);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.SoldierCreated, _user.UserId, _user.Username,
                nameof(Soldier), soldier.Id.ToString(),
                $"ایجاد سرباز {soldier.FullName()} (کد ملی: {soldier.NationalCode})", ct);

            return Result.Success(soldier.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(UpdateSoldierCommand c, CancellationToken ct)
    {
        var soldier = await _soldiers.GetByIdAsync(c.Id, ct);
        if (soldier is null) return Result.Failure("NOT_FOUND", "سرباز یافت نشد.");

        try
        {
            soldier.Update(
                PersonName.Create(c.FirstName, "First name"),
                PersonName.Create(c.LastName, "Last name"),
                c.FatherName is null ? null : PersonName.Create(c.FatherName, "Father name"),
                c.Rank, c.HealthType, c.EntryDate, c.ServiceStartDate, c.ServiceEndDate,
                c.DepartmentName, c.IsActive);
            soldier.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.SoldierUpdated, _user.UserId, _user.Username,
                nameof(Soldier), soldier.Id.ToString(), $"ویرایش سرباز {soldier.FullName()}", ct);

            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(DeleteSoldierCommand c, CancellationToken ct)
    {
        var soldier = await _soldiers.GetByIdAsync(c.Id, ct);
        if (soldier is null) return Result.Failure("NOT_FOUND", "سرباز یافت نشد.");

        try
        {
            _soldiers.Remove(soldier);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.SoldierDeleted, _user.UserId, _user.Username,
                nameof(Soldier), soldier.Id.ToString(), $"حذف سرباز {soldier.FullName()}", ct);

            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }
}
