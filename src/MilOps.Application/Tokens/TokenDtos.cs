using MilOps.Domain.Enums;

namespace MilOps.Application.Tokens;

/// <summary>
/// Returned ONLY at generation time. Carries the plaintext token so the
/// commander can copy/relay it. The plaintext is never retrievable again.
/// </summary>
public record GeneratedTokenDto(
    int Id,
    string PlaintextToken, // shown once
    string Preview,
    string FirstName, string LastName,
    string NationalCode, string PersonnelCode, string Rank,
    DateOnly ServiceStartDate, DateOnly ServiceEndDate,
    TokenPurpose Purpose, DateTime ExpiresAtUtc);

public record TokenListItemDto(
    int Id, string Preview,
    string FirstName, string LastName,
    string NationalCode, string PersonnelCode, string Rank,
    TokenPurpose Purpose, TokenStatus Status,
    DateTime IssuedAtUtc, DateTime ExpiresAtUtc, DateTime? UsedAtUtc);
