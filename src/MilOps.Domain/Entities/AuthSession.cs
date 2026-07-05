using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.Entities;

/// <summary>
/// A persistent login session ("remember me") bound to one machine.
///
/// The plaintext session token lives ONLY in a DPAPI-protected file on the
/// user's machine; we persist its peppered SHA-256 hash here. On every app
/// start the token is verified and ROTATED (new token + sliding expiry), so a
/// stolen token file has a limited window and any successful rotation
/// invalidates previously copied tokens.
/// </summary>
public class AuthSession : AuditableEntity
{
    public int UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string MachineName { get; private set; } = string.Empty;
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private AuthSession() { } // EF Core

    public static AuthSession Create(int userId, string tokenHash, string machineName,
        DateTime nowUtc, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
            throw new DomainException("SESSION_LIFETIME", "Session lifetime must be positive.");

        return new AuthSession
        {
            UserId = userId,
            TokenHash = tokenHash,
            MachineName = machineName?.Trim() ?? string.Empty,
            IssuedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + lifetime
        };
    }

    public bool IsUsable(DateTime nowUtc) =>
        RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    /// <summary>Rotate the token and slide the expiry window forward.</summary>
    public void Refresh(string newTokenHash, DateTime nowUtc, TimeSpan lifetime)
    {
        if (!IsUsable(nowUtc))
            throw new DomainException("SESSION_NOT_USABLE",
                "Cannot refresh a revoked or expired session.");
        TokenHash = newTokenHash;
        IssuedAtUtc = nowUtc;
        ExpiresAtUtc = nowUtc + lifetime;
    }

    public void Revoke(DateTime nowUtc)
    {
        if (RevokedAtUtc is null)
            RevokedAtUtc = nowUtc;
    }
}
