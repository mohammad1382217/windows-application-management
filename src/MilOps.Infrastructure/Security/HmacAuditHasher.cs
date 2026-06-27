using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MilOps.Domain.Enums;
using MilOps.Domain.Security;
using MilOps.Infrastructure.Db;

namespace MilOps.Infrastructure.Security;

/// <summary>
/// Computes chained HMAC-SHA256 hashes over audit rows and verifies integrity.
///
/// Chain construction:
///   rowHash[i] = HMAC(key, canonical(i) || previousHash)
/// where canonical(i) is a fixed-order concatenation of all row fields. Each
/// row stores its own hash AND the previous row's hash, so altering or deleting
/// any row breaks the chain at that point (tamper-evident). The HMAC key lives
/// DPAPI/TPM-protected on disk, never in the database.
/// </summary>
public sealed class HmacAuditHasher : IAuditHasher
{
    private readonly SecretProtector _secrets;
    private readonly SecurityOptions _options;
    private readonly ILogger<HmacAuditHasher> _logger;
    private readonly DbContextAccessor _dbAccessor;

    public HmacAuditHasher(SecretProtector secrets, IOptions<SecurityOptions> options,
        ILogger<HmacAuditHasher> logger, DbContextAccessor dbAccessor)
    {
        _secrets = secrets;
        _options = options.Value;
        _logger = logger;
        _dbAccessor = dbAccessor;
    }

    public string ComputeRowHash(long sequence, DateTime occurredAtUtc, AuditAction action,
        int? userId, string? username, string entityType, string? entityId,
        string? details, string? machineName, string previousHash)
    {
        var key = _secrets.UnprotectOrCreate(SecretPurposes.AuditHmacKey, _options.AuditHmacKeyBytes);
        try
        {
            var canonical = Canonicalize(sequence, occurredAtUtc, action, userId,
                username, entityType, entityId, details, machineName, previousHash);
            return Hex(HMACSHA256.HashData(key, canonical));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task<long?> VerifyChainAsync(CancellationToken ct = default)
    {
        var rows = await _dbAccessor.AuditReader.GetOrderedAsync(ct);
        var prev = string.Empty;
        foreach (var r in rows)
        {
            var recomputed = ComputeRowHash(r.Sequence, r.OccurredAtUtc, r.Action,
                r.UserId, r.Username, r.EntityType, r.EntityId, r.Details, r.MachineName, prev);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(recomputed), Encoding.ASCII.GetBytes(r.RowHash)))
            {
                _logger.LogError("Audit chain broken at sequence {Seq}.", r.Sequence);
                return r.Sequence;
            }
            prev = r.RowHash;
        }
        return null;
    }

    /// <summary>
    /// Fixed-order canonical serialization. Field order and separators are part
    /// of the contract; changing them invalidates the whole chain.
    /// </summary>
    private static byte[] Canonicalize(long sequence, DateTime occurredAtUtc, AuditAction action,
        int? userId, string? username, string entityType, string? entityId,
        string? details, string? machineName, string previousHash)
    {
        var sb = new StringBuilder(256);
        sb.Append(sequence).Append('|');
        sb.Append(occurredAtUtc.ToString("O")).Append('|');
        sb.Append((int)action).Append('|');
        sb.Append(userId?.ToString() ?? string.Empty).Append('|');
        sb.Append(username ?? string.Empty).Append('|');
        sb.Append(entityType ?? string.Empty).Append('|');
        sb.Append(entityId ?? string.Empty).Append('|');
        sb.Append(details ?? string.Empty).Append('|');
        sb.Append(machineName ?? string.Empty).Append('|');
        sb.Append(previousHash ?? string.Empty);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
}
