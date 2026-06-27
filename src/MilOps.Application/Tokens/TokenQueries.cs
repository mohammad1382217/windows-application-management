using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Tokens;

// ============================================================
// Token queries
// ============================================================

public record ListTokensQuery(TokenStatus? StatusFilter)
    : IRequest<IReadOnlyList<TokenListItemDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.TokenManage;
}

public class TokenQueryHandler : IRequestHandler<ListTokensQuery, IReadOnlyList<TokenListItemDto>>
{
    private readonly IRepository<CommanderToken> _tokens;
    private readonly IDateTime _time;
    public TokenQueryHandler(IRepository<CommanderToken> tokens, IDateTime time)
    { _tokens = tokens; _time = time; }

    public async Task<IReadOnlyList<TokenListItemDto>> Handle(ListTokensQuery req, CancellationToken ct)
    {
        var spec = new TokenListSpec(req.StatusFilter, _time.UtcNow);
        var items = await _tokens.ListAsync(spec, ct);
        return items.Select(Map).ToList();
    }

    private static TokenListItemDto Map(CommanderToken t) => new(
        t.Id, t.TokenPreview, t.FirstName, t.LastName,
        t.NationalCode, t.PersonnelCode, t.Rank,
        t.Purpose, t.Status, t.IssuedAtUtc, t.ExpiresAtUtc, t.UsedAtUtc);
}

internal sealed class TokenListSpec : Specification<CommanderToken>
{
    public TokenListSpec(TokenStatus? status, DateTime nowUtc)
    {
        // "Expired" is a derived status (Active but past ExpiresAtUtc). We surface
        // active rows for the Active/Expired filter and rely on mapping to display.
        if (status == TokenStatus.Active)
            Criteria = t => t.Status == TokenStatus.Active && t.ExpiresAtUtc >= nowUtc;
        else if (status == TokenStatus.Expired)
            Criteria = t => (t.Status == TokenStatus.Active && t.ExpiresAtUtc < nowUtc)
                         || t.Status == TokenStatus.Expired;
        else if (status.HasValue)
            Criteria = t => t.Status == status.Value;

        OrderByDescending = t => t.Id;
    }
}
