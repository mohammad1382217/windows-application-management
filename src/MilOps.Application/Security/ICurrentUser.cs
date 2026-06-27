using MilOps.Domain.Enums;

namespace MilOps.Application.Security;

/// <summary>
/// The current authenticated principal, accessible to handlers. Populated by
/// the Presentation layer after a successful login. Implementations should be
/// scoped per session (not singleton) to avoid leaking identity across users.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string Username { get; }
    string FullName { get; }
    Role Role { get; }
    bool IsAuthenticated { get; }

    /// <summary>Throw if the current user lacks the given permission.</summary>
    void Ensure(Permission permission);

    bool Has(Permission permission);
}

/// <summary>Thrown by <see cref="ICurrentUser.Ensure"/> on missing permission.</summary>
public class AuthorizationException : Exception
{
    public AuthorizationException(string message) : base(message) { }
}
