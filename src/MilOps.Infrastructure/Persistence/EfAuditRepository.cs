using Microsoft.EntityFrameworkCore;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Repositories;
using MilOps.Domain.Security;
using MilOps.Infrastructure.Db;

namespace MilOps.Infrastructure.Persistence;

/// <summary>
/// Append-only audit repository. Each append:
///   1. Reads the previous row's hash (chain link).
///   2. Computes this row's hash via <see cref="IAuditHasher"/>.
///   3. Inserts (no update/delete path is exposed).
/// Sequencing is monotonic; we take a process-local lock plus a UNIQUE index
/// on Sequence as a DB-level guarantee for single-writer scenarios.
/// </summary>
public sealed class EfAuditRepository : IAuditRepository
{
    private static readonly object s_appendLock = new();
    private readonly MilOpsDbContext _db;
    private readonly IAuditHasher _hasher;

    public EfAuditRepository(MilOpsDbContext db, IAuditHasher hasher)
    { _db = db; _hasher = hasher; }

    public async Task AppendAsync(AuditAction action, int? userId, string? username,
        string entityType, string? entityId, string? details, CancellationToken ct = default)
    {
        // Chain: previous hash + monotonic sequence. Lock guards single-writer
        // within a process; the UNIQUE index on Sequence guards at the DB level.
        AuditLog log;
        lock (s_appendLock)
        {
            var lastSeq = _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Sequence)
                .Select(a => new { a.Sequence, a.RowHash }).FirstOrDefault();
            var sequence = (lastSeq?.Sequence ?? 0) + 1;
            var previousHash = lastSeq?.RowHash ?? string.Empty;
            var occurredAt = DateTime.UtcNow;
            var machine = Environment.MachineName;

            var rowHash = _hasher.ComputeRowHash(sequence, occurredAt, action, userId,
                username, entityType, entityId, details, machine, previousHash);

            log = AuditLog.Create(sequence, occurredAt, action, userId, username,
                entityType, entityId, details, machine, previousHash, rowHash);
        }
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(DateTime? fromUtc, DateTime? toUtc,
        AuditAction? action, string? entityType, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking();
        if (fromUtc.HasValue) q = q.Where(a => a.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(a => a.OccurredAtUtc <= toUtc.Value);
        if (action.HasValue) q = q.Where(a => a.Action == action.Value);
        if (!string.IsNullOrEmpty(entityType)) q = q.Where(a => a.EntityType == entityType);
        return await q.OrderByDescending(a => a.Sequence).Take(1000).ToListAsync(ct);
    }
}
