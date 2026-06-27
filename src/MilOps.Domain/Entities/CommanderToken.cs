using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.ValueObjects;

namespace MilOps.Domain.Entities;

/// <summary>
/// A one-time authorization token issued by a Commander.
///
/// The plaintext token is shown to the commander exactly once at creation
/// (for them to relay securely) and is NEVER persisted. We store only:
///   - TokenHash        : SHA-256 of the plaintext token (lookup/verify)
///   - TokenPreview     : first 8 chars (harmless, for UI display only)
///
/// The cryptographic generation itself is delegated to the
/// <see cref="ITokenGenerator"/> port in the Application layer so the domain
/// stays free of crypto dependencies.
/// </summary>
public class CommanderToken : AuditableEntity
{
    public string TokenHash { get; private set; } = string.Empty;
    public string TokenPreview { get; private set; } = string.Empty;

    // Intended holder identity (for display and revocation auditability).
    public PersonName FirstName { get; private set; } = null!;
    public PersonName LastName { get; private set; } = null!;
    public NationalCode NationalCode { get; private set; } = null!;
    public PersonnelCode PersonnelCode { get; private set; } = null!;
    public string Rank { get; private set; } = string.Empty;

    public DateOnly ServiceStartDate { get; private set; }
    public DateOnly ServiceEndDate { get; private set; }

    public TokenPurpose Purpose { get; private set; }
    public TokenStatus Status { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public int? IssuedByUserId { get; private set; }
    public int? UsedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }

    private CommanderToken() { } // EF Core

    public static CommanderToken Create(
        PersonName firstName, PersonName lastName,
        NationalCode nationalCode, PersonnelCode personnelCode, string rank,
        DateOnly serviceStartDate, DateOnly serviceEndDate,
        TokenPurpose purpose, string tokenHash, string tokenPreview,
        DateTime expiresAtUtc, int issuedByUserId)
    {
        if (expiresAtUtc <= DateTime.UtcNow)
            throw new DomainException("TOKEN_EXPIRED_AT_CREATE",
                "Token expiration must be in the future.");
        if (serviceEndDate <= serviceStartDate)
            throw new DomainException("TOKEN_DATE_RANGE",
                "Service end date must be after service start date.");

        return new CommanderToken
        {
            FirstName = firstName,
            LastName = lastName,
            NationalCode = nationalCode,
            PersonnelCode = personnelCode,
            Rank = rank?.Trim() ?? string.Empty,
            ServiceStartDate = serviceStartDate,
            ServiceEndDate = serviceEndDate,
            Purpose = purpose,
            Status = TokenStatus.Active,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            TokenHash = tokenHash,
            TokenPreview = tokenPreview,
            IssuedByUserId = issuedByUserId
        };
    }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

    /// <summary>
    /// Mark the token consumed by a single-time activation.
    /// Throws if already used, revoked, or expired.
    /// </summary>
    public void MarkUsed(int usedByUserId, DateTime nowUtc)
    {
        EnsureUsable(nowUtc);
        Status = TokenStatus.Used;
        UsedAtUtc = nowUtc;
        UsedByUserId = usedByUserId;
    }

    public void Revoke(string reason, DateTime nowUtc)
    {
        if (Status is TokenStatus.Used or TokenStatus.Revoked)
            throw new DomainException("TOKEN_NOT_REVOKABLE",
                "Only active or expired tokens can be revoked.");
        Status = TokenStatus.Revoked;
        RevocationReason = reason;
    }

    private void EnsureUsable(DateTime nowUtc)
    {
        if (Status == TokenStatus.Used)
            throw new DomainException("TOKEN_ALREADY_USED", "This token has already been used.");
        if (Status == TokenStatus.Revoked)
            throw new DomainException("TOKEN_REVOKED", "This token has been revoked.");
        if (IsExpired(nowUtc))
            throw new DomainException("TOKEN_EXPIRED", "This token has expired.");
    }
}
