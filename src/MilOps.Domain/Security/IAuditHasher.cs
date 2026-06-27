using MilOps.Domain.Enums;

namespace MilOps.Domain.Security;

/// <summary>
/// Computes chained HMAC hashes for <see cref="Entities.AuditLog"/> rows and
/// verifies the integrity of the stored chain. The HMAC key is provisioned and
/// protected by the Infrastructure (DPAPI/TPM) layer.
/// </summary>
public interface IAuditHasher
{
    /// <summary>Compute the row hash for a new audit record, given the previous row's hash.</summary>
    string ComputeRowHash(long sequence, DateTime occurredAtUtc, AuditAction action,
        int? userId, string? username, string entityType, string? entityId,
        string? details, string? machineName, string previousHash);

    /// <summary>Walk the chain and return the first broken sequence number (or null if intact).</summary>
    Task<long?> VerifyChainAsync(CancellationToken ct = default);
}
