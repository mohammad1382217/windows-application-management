using FluentValidation;
using MediatR;
using MilOps.Application.Common;
using MilOps.Application.Security;
using DomainEx = MilOps.Domain.Exceptions.DomainException;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Repositories;
using MilOps.Domain.Security;

namespace MilOps.Application.Authentication;

// ============================================================
// Login / Logout
// ============================================================

public record LoginCommand(string Username, string Password)
    : IRequest<Result<LoginResult>>;

public record LoginResult(int UserId, string Username, string FullName, Role Role);

public record LogoutCommand : IRequest;

// ------------------------------------------------------------
// Validator
// ------------------------------------------------------------

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

// ------------------------------------------------------------
// Handler
// ------------------------------------------------------------

/// <summary>
/// Authenticates a user. Implements a constant-time-ish failure path (we always
/// hash-compare even when the user is missing) and account lockout after
/// repeated failures. On success, the session is established via
/// <see cref="ISessionRegistry"/>.
/// </summary>
public class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResult>>,
    IRequestHandler<LogoutCommand>
{
    private readonly IRepository<User> _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _time;
    private readonly ISessionRegistry _sessions;
    private readonly IAuditRepository _audit;
    private readonly AuthenticationOptions _options;

    public const int MaxFailedAttempts = 5;

    public LoginHandler(IRepository<User> users, IPasswordHasher hasher, IUnitOfWork uow,
        IDateTime time, ISessionRegistry sessions, IAuditRepository audit,
        Microsoft.Extensions.Options.IOptions<AuthenticationOptions> options)
    {
        _users = users; _hasher = hasher; _uow = uow; _time = time;
        _sessions = sessions; _audit = audit; _options = options.Value;
    }

    public async Task<Result<LoginResult>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var spec = new UserByUsernameSpec(cmd.Username);
        var user = await _users.FirstOrDefaultAsync(spec, ct);

        // Always compute a dummy hash compare to keep timing roughly constant
        // whether or not the account exists (mitigates user-enumeration timing).
        var passwordOk = user is not null
            ? _hasher.Verify(cmd.Password, user.PasswordHash)
            : _hasher.Verify(cmd.Password, "$2a$11$dummyhashdummyhashdummyhashdummyhashdummyhashdummy");

        if (user is null || !passwordOk)
        {
            if (user is not null)
            {
                try
                {
                    var locked = user.RecordFailedLogin(MaxFailedAttempts);
                    await _uow.SaveChangesAsync(ct);
                    await _audit.AppendAsync(AuditAction.LoginFailed, user.Id, user.Username,
                        nameof(User), null, "Failed login", ct);
                }
                catch (DomainEx)
                {
                    // Swallow persistence errors on failed-login tracking so the
                    // caller still gets the AUTH_FAILED result rather than a DB error.
                }
            }
            return Result.Failure<LoginResult>("AUTH_FAILED", "Invalid username or password.");
        }

        if (!user.IsActive)
            return Result.Failure<LoginResult>("AUTH_DISABLED", "Account is disabled.");
        if (user.IsLockedOut)
            return Result.Failure<LoginResult>("AUTH_LOCKED", "Account is locked after repeated failures.");

        user.RecordSuccessfulLogin();
        user.ResetLockout();
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DomainEx ex)
        {
            // A persistence failure updating the user record should not block the
            // login the credentials have already validated; surface it instead of
            // crashing the flow.
            return Result.Failure<LoginResult>(ex.Code, ex.Message);
        }

        _sessions.Establish(user.Id, user.Username, user.FullName.ToString(), user.Role);

        // Audit logging is best-effort on the success path: a failed audit write
        // must not invalidate an otherwise-successful authentication.
        try
        {
            await _audit.AppendAsync(AuditAction.Login, user.Id, user.Username,
                nameof(User), user.Id.ToString(), "Login successful", ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not System.Threading.ThreadAbortException)
        {
            // ignored: audit is non-critical
        }

        return Result.Success(new LoginResult(user.Id, user.Username, user.FullName.ToString(), user.Role));
    }

    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var s = _sessions.Current;
        _sessions.Clear();
        if (s is not null)
            await _audit.AppendAsync(AuditAction.Logout, s.UserId, s.Username,
                nameof(User), s.UserId.ToString(), "Logout", ct);
    }
}

/// <summary>Locates a user by username (case-insensitive).</summary>
public sealed class UserByUsernameSpec : Specification<User>
{
    public UserByUsernameSpec(string username)
    {
        var u = username.Trim().ToLowerInvariant();
        Criteria = x => x.Username.ToLower() == u;
    }
}

// ------------------------------------------------------------
// Session
// ------------------------------------------------------------

/// <summary>
/// Holds the live session. Scoped per app run. The Presentation layer sets it
/// on login and reads it for the <see cref="ICurrentUser"/> adapter.
/// </summary>
public interface ISessionRegistry
{
    SessionInfo? Current { get; }
    void Establish(int userId, string username, string fullName, Role role);
    void Clear();
}

public sealed record SessionInfo(int UserId, string Username, string FullName, Role Role);

/// <summary>Mutable, session-scoped implementation of <see cref="ISessionRegistry"/>.</summary>
public sealed class SessionRegistry : ISessionRegistry
{
    public SessionInfo? Current { get; private set; }
    public void Establish(int userId, string username, string fullName, Role role) =>
        Current = new SessionInfo(userId, username, fullName, role);
    public void Clear() => Current = null;
}

/// <summary>Adapts <see cref="ISessionRegistry"/> to <see cref="ICurrentUser"/>.</summary>
public sealed class CurrentUserAdapter : ICurrentUser
{
    private readonly ISessionRegistry _sessions;
    public CurrentUserAdapter(ISessionRegistry sessions) => _sessions = sessions;

    public int? UserId => _sessions.Current?.UserId;
    public string Username => _sessions.Current?.Username ?? string.Empty;
    public string FullName => _sessions.Current?.FullName ?? string.Empty;
    public Role Role => _sessions.Current?.Role ?? Role.ReadOnly;
    public bool IsAuthenticated => _sessions.Current is not null;

    public bool Has(Permission permission) =>
        _sessions.Current is { } s && RolePermissions.Has(s.Role, permission);

    public void Ensure(Permission permission)
    {
        if (!IsAuthenticated)
            throw new AuthorizationException("Authentication required.");
        if (!Has(permission))
            throw new AuthorizationException($"Role {Role} lacks permission {permission}.");
    }
}

/// <summary>Auth-related configuration (lockout policy etc.).</summary>
public sealed class AuthenticationOptions
{
    public const int DefaultMaxFailedAttempts = 5;
    public int MaxFailedAttempts { get; set; } = DefaultMaxFailedAttempts;
    public int MinPasswordLength { get; set; } = 8;
}
