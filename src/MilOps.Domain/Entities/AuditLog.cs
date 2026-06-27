using MilOps.Domain.Common;
using MilOps.Domain.Enums;

namespace MilOps.Domain.Entities;

/// <summary>
/// A single tamper-evident audit record.
///
/// Tamper-resistance strategy (append-only + chained HMAC):
///   - Records are INSERT-only at the DB level (no UPDATE/DELETE path exposed).
///   - Each row carries <see cref="PreviousHash"/> (HMAC of the prior row) and
///     <see cref="RowHash"/> (HMAC of this row's canonical content).
///   - The HMAC key is derived from a value protected by DPAPI (see infra).
///   - On verification, recomputing the chain exposes any tampering/gap.
///
/// This is tamper-EVIDENT, not tamper-PROOF: a determined attacker with the
/// machine + key could still forge. For stronger guarantees, mirror the chain
/// to write-once media or a remote collector (out of scope for offline use).
/// </summary>
public class AuditLog : Entity
{
    public long Sequence { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public AuditAction Action { get; private set; }
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? Category { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string? Details { get; private set; }
    public string? MachineName { get; private set; }
    public string PreviousHash { get; private set; } = string.Empty;
    public string RowHash { get; private set; } = string.Empty;

    private AuditLog() { } // EF Core

    internal AuditLog(long sequence, DateTime occurredAtUtc, AuditAction action,
        int? userId, string? username, string entityType, string? entityId,
        string? details, string? machineName, string previousHash, string rowHash)
    {
        Sequence = sequence;
        OccurredAtUtc = occurredAtUtc;
        Action = action;
        UserId = userId;
        Username = username;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        MachineName = machineName;
        PreviousHash = previousHash;
        RowHash = rowHash;
    }

    /// <summary>
    /// Factory used by Infrastructure to construct an already-hashed audit row.
    /// Kept internal so only the audit pipeline can build rows with a valid hash.
    /// </summary>
    internal static AuditLog Create(long sequence, DateTime occurredAtUtc, AuditAction action,
        int? userId, string? username, string entityType, string? entityId,
        string? details, string? machineName, string previousHash, string rowHash)
        => new(sequence, occurredAtUtc, action, userId, username, entityType, entityId,
            details, machineName, previousHash, rowHash);
}
