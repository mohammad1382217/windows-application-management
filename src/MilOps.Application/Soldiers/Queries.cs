using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Soldiers;

// ============================================================
// Queries (read side)
// ============================================================

public record GetSoldierByIdQuery(int Id) : IRequest<SoldierDto?>;

public record SearchSoldiersQuery(SoldierSearchRequest Filter)
    : IRequest<PagedResult<SoldierDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.SoldierRead;
}

public class SoldierQueryHandlers :
    IRequestHandler<GetSoldierByIdQuery, SoldierDto?>,
    IRequestHandler<SearchSoldiersQuery, PagedResult<SoldierDto>>
{
    private readonly IRepository<Soldier> _soldiers;
    public SoldierQueryHandlers(IRepository<Soldier> soldiers) => _soldiers = soldiers;

    public async Task<SoldierDto?> Handle(GetSoldierByIdQuery request, CancellationToken ct)
    {
        var s = await _soldiers.GetByIdAsync(request.Id, ct);
        return s is null ? null : Map(s);
    }

    public async Task<PagedResult<SoldierDto>> Handle(SearchSoldiersQuery request, CancellationToken ct)
    {
        var spec = new SoldierSpecification(request.Filter);
        var total = await _soldiers.CountAsync(spec, ct);
        var items = await _soldiers.ListAsync(spec, ct);
        return new PagedResult<SoldierDto>(
            items.Select(Map).ToList(), total, request.Filter.Page, request.Filter.PageSize);
    }

    private static SoldierDto Map(Soldier s) => new(
        s.Id, s.FirstName, s.LastName, s.FatherName, s.Rank,
        s.NationalCode, s.PersonnelCode, s.HealthType,
        s.EntryDate, s.ServiceStartDate, s.ServiceEndDate,
        s.DepartmentName, s.IsActive, s.CanGuard(), s.CreatedAtUtc);
}
