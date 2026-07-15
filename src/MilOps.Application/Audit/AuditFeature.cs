using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Enums;
using MilOps.Domain.Repositories;

namespace MilOps.Application.Audit;

/// <summary>Read model for an audit entry.</summary>
public record AuditEntryDto(
    long Sequence, DateTime OccurredAtUtc, AuditAction Action,
    string? Username, string EntityType, string? EntityId,
    string? Details, string? MachineName, string RowHashPreview);

public record QueryAuditQuery(DateTime? FromUtc, DateTime? ToUtc, AuditAction? Action)
    : IRequest<IReadOnlyList<AuditEntryDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.AuditRead;
}

public record VerifyAuditChainQuery : IRequest<Result<string>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.AuditRead;
}

public class AuditQueryHandlers :
    IRequestHandler<QueryAuditQuery, IReadOnlyList<AuditEntryDto>>,
    IRequestHandler<VerifyAuditChainQuery, Result<string>>
{
    private readonly IAuditRepository _audit;
    private readonly MilOps.Domain.Security.IAuditHasher _hasher;
    public AuditQueryHandlers(IAuditRepository audit, MilOps.Domain.Security.IAuditHasher hasher)
    { _audit = audit; _hasher = hasher; }

    public async Task<IReadOnlyList<AuditEntryDto>> Handle(QueryAuditQuery q, CancellationToken ct)
    {
        var rows = await _audit.QueryAsync(q.FromUtc, q.ToUtc, q.Action, null, ct);
        return rows.Select(r => new AuditEntryDto(
            r.Sequence, r.OccurredAtUtc, r.Action, r.Username, r.EntityType,
            r.EntityId, r.Details, r.MachineName,
            r.RowHash.Length > 12 ? r.RowHash[..12] + "…" : r.RowHash)).ToList();
    }

    public async Task<Result<string>> Handle(VerifyAuditChainQuery request, CancellationToken ct)
    {
        var brokenAt = await _hasher.VerifyChainAsync(ct);
        return brokenAt is { } seq
            ? Result.Failure<string>("CHAIN_BROKEN",
                $"هشدار: زنجیره سلامت گزارش در ردیف {seq} شکسته است.\n" +
                "این یعنی سوابق از این ردیف به بعد دستکاری یا حذف شده‌اند و دیگر قابل استناد نیستند.")
            : Result.Success(
                "سلامت گزارش تأیید شد ✔\n" +
                "تمام سوابق حسابرسی دست‌نخورده‌اند؛ هیچ ردیفی ویرایش یا حذف نشده است.");
    }
}
