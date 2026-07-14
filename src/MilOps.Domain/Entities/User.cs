using MilOps.Domain.Common;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.ValueObjects;

namespace MilOps.Domain.Entities;

/// <summary>
/// An authenticated user account. Passwords are stored ONLY as a salted hash;
/// the domain never holds a plaintext password. The domain defines the
/// <see cref="IPasswordHasher"/> port for hashing/verifying.
/// </summary>
public class User : AuditableEntity
{
    private static readonly Regex s_usernamePattern = new("^[a-zA-Z0-9._-]{3,40}$", RegexOptions.Compiled);

    public string Username { get; private set; } = string.Empty;
    public PersonName FullName { get; private set; } = null!;
    public Role Role { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime? PasswordChangedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public bool IsLockedOut { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// False until the holder redeems a commander-issued activation token at
    /// first login. Accounts created without token flow (seed) start activated.
    /// </summary>
    public bool IsActivated { get; private set; }

    private User() { } // EF Core

    public static User Create(PersonName fullName, string username, Role role, string passwordHash,
        bool requiresActivation = false)
    {
        ValidateUsername(username);
        var user = new User
        {
            FullName = fullName,
            Username = username.Trim(),
            Role = role,
            PasswordHash = passwordHash,
            PasswordChangedAtUtc = DateTime.UtcNow,
            IsActive = true,
            IsActivated = !requiresActivation
        };
        return user;
    }

    /// <summary>Called when a commander-issued activation token is redeemed.</summary>
    public void CompleteActivation() => IsActivated = true;

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("USER_PASSWORD_EMPTY", "Password hash is required.");
        PasswordHash = newPasswordHash;
        PasswordChangedAtUtc = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        IsLockedOut = false;
    }

    public void RecordSuccessfulLogin() => LastLoginAtUtc = DateTime.UtcNow;

    public bool RecordFailedLogin(int maxAttemptsBeforeLockout)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttemptsBeforeLockout)
        {
            IsLockedOut = true;
            return true;
        }
        return false;
    }

    public void ResetLockout()
    {
        FailedLoginAttempts = 0;
        IsLockedOut = false;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    public void ChangeRole(Role role) => Role = role;

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || !s_usernamePattern.IsMatch(username.Trim()))
            throw new DomainException("USER_USERNAME_FORMAT",
                "Username must be 3-40 chars: letters, digits, dot, underscore, hyphen.");
    }
}
