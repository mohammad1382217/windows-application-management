using FluentValidation;
using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Weapons;

public record WeaponDto(int Id, string WeaponNumber, WeaponType Type, WeaponStatus Status,
    string? Model, int? CurrentlyAssignedSoldierId, string? AssignedSoldierName = null);

public record WeaponAssignmentHistoryDto(int Id, int SoldierId, int IssuedByUserId,
    DateTime IssuedAtUtc, DateTime? ReturnedAtUtc, int? ReturnedAmmunition, string? Note,
    string? SoldierName = null);

public record ListWeaponsQuery : IRequest<IReadOnlyList<WeaponDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WeaponRead;
}
public record GetWeaponHistoryQuery(int WeaponId)
    : IRequest<IReadOnlyList<WeaponAssignmentHistoryDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WeaponRead;
}

public record CreateWeaponCommand(string WeaponNumber, WeaponType Type, string? Model)
    : IRequest<Result<int>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WeaponWrite;
}
public record IssueWeaponCommand(int WeaponId, int SoldierId, string? Note)
    : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WeaponWrite;
}
public record ReturnWeaponCommand(int WeaponId, int? ReturnedAmmunition, string? Note)
    : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.WeaponWrite;
}

public class CreateWeaponValidator : AbstractValidator<CreateWeaponCommand>
{
    public CreateWeaponValidator()
    {
        RuleFor(x => x.WeaponNumber).NotEmpty().MaximumLength(30);
    }
}

public class WeaponHandlers :
    IRequestHandler<ListWeaponsQuery, IReadOnlyList<WeaponDto>>,
    IRequestHandler<GetWeaponHistoryQuery, IReadOnlyList<WeaponAssignmentHistoryDto>>,
    IRequestHandler<CreateWeaponCommand, Result<int>>,
    IRequestHandler<IssueWeaponCommand, Result>,
    IRequestHandler<ReturnWeaponCommand, Result>
{
    private readonly IRepository<Weapon> _weapons;
    private readonly IRepository<Soldier> _soldiers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly IAuditRepository _audit;

    public WeaponHandlers(IRepository<Weapon> weapons, IRepository<Soldier> soldiers, IUnitOfWork uow,
        ICurrentUser user, IDateTime time, IAuditRepository audit)
    { _weapons = weapons; _soldiers = soldiers; _uow = uow; _user = user; _time = time; _audit = audit; }

    public async Task<IReadOnlyList<WeaponDto>> Handle(ListWeaponsQuery q, CancellationToken ct)
    {
        // History MUST be loaded: CurrentlyAssignedSoldierId is computed from it
        // (open history row). Without the Include it always read as null, so
        // the "تخصیص‌یافته به" column silently showed nothing for every weapon.
        var items = await _weapons.ListAsync(new AllWeaponsWithHistorySpec(), ct);
        var ids = items.Where(w => w.CurrentlyAssignedSoldierId.HasValue)
            .Select(w => w.CurrentlyAssignedSoldierId!.Value).Distinct().ToList();
        var names = await ResolveSoldierNamesAsync(ids, ct);
        return items.Select(w => Map(w, names)).ToList();
    }

    public async Task<IReadOnlyList<WeaponAssignmentHistoryDto>> Handle(GetWeaponHistoryQuery q, CancellationToken ct)
    {
        // GetByIdAsync uses FindAsync, which does NOT eager-load the History
        // navigation. Use a spec with an Include so the assignment rows are
        // actually fetched (otherwise the history always reads as empty).
        var w = await _weapons.FirstOrDefaultAsync(new WeaponWithHistorySpec(q.WeaponId), ct);
        if (w is null) return new List<WeaponAssignmentHistoryDto>();

        var names = await ResolveSoldierNamesAsync(
            w.History.Select(h => h.SoldierId).Distinct().ToList(), ct);
        return w.History.Select(h => new WeaponAssignmentHistoryDto(
            h.Id, h.SoldierId, h.IssuedByUserId, h.IssuedAtUtc,
            h.ReturnedAtUtc, h.ReturnedAmmunition, h.Note,
            names.GetValueOrDefault(h.SoldierId))).ToList();
    }

    private async Task<Dictionary<int, string>> ResolveSoldierNamesAsync(
        IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<int, string>();
        var found = await _soldiers.ListAsync(new SoldiersByIdsSpec(ids), ct);
        return found.ToDictionary(s => s.Id, s => s.FullName());
    }

    public async Task<Result<int>> Handle(CreateWeaponCommand c, CancellationToken ct)
    {
        try
        {
            var weapon = Weapon.Create(c.WeaponNumber, c.Type, WeaponStatus.Available, c.Model);
            weapon.CreatedBy = _user.Username;
            _weapons.Add(weapon);
            await _uow.SaveChangesAsync(ct);
            await _audit.AppendAsync(AuditAction.WeaponIssued, _user.UserId, _user.Username,
                nameof(Weapon), weapon.Id.ToString(), $"ثبت سلاح {weapon.WeaponNumber}", ct);
            return Result.Success(weapon.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(IssueWeaponCommand c, CancellationToken ct)
    {
        // History MUST be loaded: IssueTo's double-issue guard reads it.
        var w = await _weapons.FirstOrDefaultAsync(new WeaponWithHistorySpec(c.WeaponId), ct);
        if (w is null) return Result.Failure("NOT_FOUND", "سلاح یافت نشد.");
        try
        {
            w.IssueTo(c.SoldierId, _user.UserId ?? 0, _time.UtcNow, c.Note);
            w.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);
            var issuedToName = (await ResolveSoldierNamesAsync(new[] { c.SoldierId }, ct))
                .GetValueOrDefault(c.SoldierId) ?? $"#{c.SoldierId}";
            await _audit.AppendAsync(AuditAction.WeaponIssued, _user.UserId, _user.Username,
                nameof(Weapon), w.Id.ToString(), $"تحویل {w.WeaponNumber} به {issuedToName}", ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(ReturnWeaponCommand c, CancellationToken ct)
    {
        // History MUST be loaded: Return closes the open history row; with an
        // unloaded collection it always failed with WEAPON_NOT_ISSUED.
        var w = await _weapons.FirstOrDefaultAsync(new WeaponWithHistorySpec(c.WeaponId), ct);
        if (w is null) return Result.Failure("NOT_FOUND", "سلاح یافت نشد.");
        try
        {
            w.Return(_user.UserId ?? 0, _time.UtcNow, c.ReturnedAmmunition, c.Note);
            w.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);
            await _audit.AppendAsync(AuditAction.WeaponReturned, _user.UserId, _user.Username,
                nameof(Weapon), w.Id.ToString(), $"بازگشت سلاح {w.WeaponNumber}", ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    private static WeaponDto Map(Weapon w, IReadOnlyDictionary<int, string> names) => new(
        w.Id, w.WeaponNumber, w.Type, w.Status, w.Model, w.CurrentlyAssignedSoldierId,
        w.CurrentlyAssignedSoldierId is { } sid ? names.GetValueOrDefault(sid) : null);
}

/// <summary>Eager-loads the assignment history for a single weapon.</summary>
internal sealed class WeaponWithHistorySpec : Specification<Weapon>
{
    public WeaponWithHistorySpec(int weaponId)
    {
        Criteria = w => w.Id == weaponId;
        AddInclude(w => w.History);
    }
}

/// <summary>Eager-loads history for every weapon (needed for CurrentlyAssignedSoldierId).</summary>
internal sealed class AllWeaponsWithHistorySpec : Specification<Weapon>
{
    public AllWeaponsWithHistorySpec() => AddInclude(w => w.History);
}

internal sealed class SoldiersByIdsSpec : Specification<Soldier>
{
    public SoldiersByIdsSpec(IReadOnlyCollection<int> ids) => Criteria = s => ids.Contains(s.Id);
}
