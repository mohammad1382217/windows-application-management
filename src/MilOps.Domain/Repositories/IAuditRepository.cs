using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Security;

namespace MilOps.Domain.Repositories;

/// <summary>
/// Append-only audit store. The write path always goes through
/// <see cref="IAuditHasher"/> so rows are chained before persistence.
/// </summary>
public interface IAuditRepository
{
    Task AppendAsync(AuditAction action, int? userId, string? username,
        string entityType, string? entityId, string? details, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> QueryAsync(DateTime? fromUtc, DateTime? toUtc,
        AuditAction? action, string? entityType, CancellationToken ct = default);
}
