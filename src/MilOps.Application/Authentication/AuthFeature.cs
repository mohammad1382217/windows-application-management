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

/// <param name="PersistentToken">
/// Plaintext "remember me" token, returned exactly once per login/refresh for
/// the Presentation layer to store DPAPI-protected on disk. Never persisted.
/// </param>
public record LoginResult(int UserId, string Username, string FullName, Role Role,
    string? PersistentToken = null);

/// <summary>
/// Silent login on app start using the persisted session token. On success the
/// token is ROTATED: the result carries a fresh <see cref="LoginResult.PersistentToken"/>
/// which the caller must store in place of the old one.
/// </summary>
public record AutoLoginCommand(string Token) : IRequest<Result<LoginResult>>;

/// <summary>
/// First-login activation: verifies the credentials AND a commander-issued
/// one-time activation token. On success the token is consumed (MarkUsed),
/// the account is activated, and a full session is established.
/// </summary>
public record ActivateAccountCommand(string Username, string Password, string Token)
    : IRequest<Result<LoginResult>>;

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
    IRequestHandler<AutoLoginCommand, Result<LoginResult>>,
    IRequestHandler<ActivateAccountCommand, Result<LoginResult>>,
    IRequestHandler<LogoutCommand>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<AuthSession> _authSessions;
    private readonly IRepository<CommanderToken> _commanderTokens;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenGenerator _tokens;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _time;
    private readonly ISessionRegistry _sessions;
    private readonly IAuditRepository _audit;
    private readonly AuthenticationOptions _options;

    public const int MaxFailedAttempts = 5;

    public LoginHandler(IRepository<User> users, IRepository<AuthSession> authSessions,
        IRepository<CommanderToken> commanderTokens,
        IPasswordHasher hasher, ITokenGenerator tokens, IUnitOfWork uow,
        IDateTime time, ISessionRegistry sessions, IAuditRepository audit,
        Microsoft.Extensions.Options.IOptions<AuthenticationOptions> options)
    {
        _users = users; _authSessions = authSessions; _commanderTokens = commanderTokens;
        _hasher = hasher; _tokens = tokens;
        _uow = uow; _time = time; _sessions = sessions; _audit = audit;
        _options = options.Value;
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
            return Result.Failure<LoginResult>("AUTH_FAILED", "نام کاربری یا گذرواژه اشتباه است.");
        }

        if (!user.IsActive)
            return Result.Failure<LoginResult>("AUTH_DISABLED", "حساب کاربری غیرفعال است.");
        if (user.IsLockedOut)
            return Result.Failure<LoginResult>("AUTH_LOCKED", "حساب به دلیل تلاش‌های مکرر قفل شده است.");
        if (!user.IsActivated)
            return Result.Failure<LoginResult>("ACTIVATION_REQUIRED",
                "حساب شما هنوز فعال نشده است. توکن فعال‌سازی دریافتی از فرمانده را وارد کنید.");

        return await EstablishInteractiveSessionAsync(user, "Login successful", ct);
    }

    /// <summary>
    /// Shared success path for interactive login and token activation: record
    /// login, establish the in-memory session, issue the persistent token, audit.
    /// </summary>
    private async Task<Result<LoginResult>> EstablishInteractiveSessionAsync(
        User user, string auditDetail, CancellationToken ct)
    {
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

        // Persistent "remember me" session: one active session per user+machine.
        // Old sessions on this machine are revoked first so a re-login cannot
        // leave stale usable tokens behind. Best-effort: a failure here must not
        // block an otherwise-successful interactive login.
        string? persistentToken = null;
        try
        {
            persistentToken = await IssuePersistentTokenAsync(user.Id, ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not System.Threading.ThreadAbortException)
        {
            // ignored: persistent session is a convenience, not a requirement
        }

        // Audit logging is best-effort on the success path: a failed audit write
        // must not invalidate an otherwise-successful authentication.
        try
        {
            await _audit.AppendAsync(AuditAction.Login, user.Id, user.Username,
                nameof(User), user.Id.ToString(), auditDetail, ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not System.Threading.ThreadAbortException)
        {
            // ignored: audit is non-critical
        }

        return Result.Success(new LoginResult(user.Id, user.Username, user.FullName.ToString(),
            user.Role, persistentToken));
    }

    /// <summary>
    /// Redeem a commander-issued activation token on first login. Credentials
    /// are fully re-verified so the token alone can never open a session.
    /// </summary>
    public async Task<Result<LoginResult>> Handle(ActivateAccountCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Token))
            return Result.Failure<LoginResult>("TOKEN_EMPTY", "توکن فعال‌سازی را وارد کنید.");

        var user = await _users.FirstOrDefaultAsync(new UserByUsernameSpec(cmd.Username), ct);
        var passwordOk = user is not null
            ? _hasher.Verify(cmd.Password, user.PasswordHash)
            : _hasher.Verify(cmd.Password, "$2a$11$dummyhashdummyhashdummyhashdummyhashdummyhashdummy");
        if (user is null || !passwordOk)
            return Result.Failure<LoginResult>("AUTH_FAILED", "نام کاربری یا گذرواژه اشتباه است.");
        if (!user.IsActive)
            return Result.Failure<LoginResult>("AUTH_DISABLED", "حساب کاربری غیرفعال است.");
        if (user.IsLockedOut)
            return Result.Failure<LoginResult>("AUTH_LOCKED", "حساب به دلیل تلاش‌های مکرر قفل شده است.");
        if (user.IsActivated)
            return await EstablishInteractiveSessionAsync(user, "Login successful", ct);

        // Look the token up by its peppered hash — same scheme as generation.
        var hash = _tokens.Hash(cmd.Token.Trim());
        var token = await _commanderTokens.FirstOrDefaultAsync(new CommanderTokenByHashSpec(hash), ct);
        if (token is null)
            return Result.Failure<LoginResult>("TOKEN_INVALID", "توکن فعال‌سازی نامعتبر است.");
        if (token.Purpose != TokenPurpose.AccountActivation)
            return Result.Failure<LoginResult>("TOKEN_WRONG_PURPOSE",
                "این توکن برای فعال‌سازی حساب صادر نشده است.");

        var now = _time.UtcNow;
        try
        {
            token.MarkUsed(user.Id, now); // throws if used / revoked / expired
        }
        catch (DomainEx ex)
        {
            var message = ex.Code switch
            {
                "TOKEN_ALREADY_USED" => "این توکن قبلاً استفاده شده است.",
                "TOKEN_REVOKED" => "این توکن باطل شده است.",
                "TOKEN_EXPIRED" => "این توکن منقضی شده است.",
                _ => ex.Message
            };
            return Result.Failure<LoginResult>(ex.Code, message);
        }

        user.CompleteActivation();
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DomainEx ex)
        {
            return Result.Failure<LoginResult>(ex.Code, ex.Message);
        }

        try
        {
            await _audit.AppendAsync(AuditAction.TokenUsed, user.Id, user.Username,
                nameof(CommanderToken), token.Id.ToString(),
                $"Account '{user.Username}' activated with commander token", ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not System.Threading.ThreadAbortException)
        {
            // ignored: audit is non-critical
        }

        return await EstablishInteractiveSessionAsync(user,
            "Login successful (account activated with token)", ct);
    }

    /// <summary>
    /// Silent startup login: verify the persisted token, re-check the account
    /// state (active / not locked), then ROTATE the token so each stored token
    /// is single-use. Any failure invalidates the stored token client-side.
    /// </summary>
    public async Task<Result<LoginResult>> Handle(AutoLoginCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Token))
            return Result.Failure<LoginResult>("SESSION_INVALID", "نشست معتبر نیست.");

        var now = _time.UtcNow;

        // Lookup by peppered hash: without the DPAPI/TPM-protected pepper an
        // attacker holding only the DB cannot forge or verify candidate tokens.
        var hash = _tokens.Hash(cmd.Token);
        var session = await _authSessions.FirstOrDefaultAsync(new AuthSessionByHashSpec(hash), ct);
        if (session is null || !session.IsUsable(now))
            return Result.Failure<LoginResult>("SESSION_INVALID", "نشست منقضی شده است. دوباره وارد شوید.");

        // Re-validate the ACCOUNT on every auto-login: a deactivated or locked
        // user must not slip back in through a previously issued session.
        var user = await _users.GetByIdAsync(session.UserId, ct);
        if (user is null || !user.IsActive || user.IsLockedOut || !user.IsActivated)
        {
            session.Revoke(now);
            try { await _uow.SaveChangesAsync(ct); } catch (DomainEx) { /* revocation is best-effort */ }
            return Result.Failure<LoginResult>("SESSION_INVALID", "حساب کاربری معتبر نیست. دوباره وارد شوید.");
        }

        // Rotate: the old token dies here; the caller must store the new one.
        var fresh = _tokens.Generate(TokenPurpose.AccountActivation);
        session.Refresh(fresh.Hash, now, TimeSpan.FromDays(_options.PersistentSessionDays));
        user.RecordSuccessfulLogin();

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DomainEx)
        {
            // If rotation cannot be persisted, fail closed: do NOT establish a
            // session from a token we could not rotate.
            return Result.Failure<LoginResult>("SESSION_INVALID", "خطا در تازه‌سازی نشست. دوباره وارد شوید.");
        }

        _sessions.Establish(user.Id, user.Username, user.FullName.ToString(), user.Role);

        try
        {
            await _audit.AppendAsync(AuditAction.Login, user.Id, user.Username,
                nameof(User), user.Id.ToString(), "Auto login (persistent session refresh)", ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not System.Threading.ThreadAbortException)
        {
            // ignored: audit is non-critical
        }

        return Result.Success(new LoginResult(user.Id, user.Username, user.FullName.ToString(),
            user.Role, fresh.Plaintext));
    }

    /// <summary>Revoke existing sessions for this user+machine and issue a fresh one.</summary>
    private async Task<string> IssuePersistentTokenAsync(int userId, CancellationToken ct)
    {
        var now = _time.UtcNow;
        var machine = Environment.MachineName;

        var existing = await _authSessions.ListAsync(
            new ActiveSessionsByUserSpec(userId, machine), ct);
        foreach (var s in existing)
            s.Revoke(now);

        var generated = _tokens.Generate(TokenPurpose.AccountActivation);
        _authSessions.Add(AuthSession.Create(userId, generated.Hash, machine, now,
            TimeSpan.FromDays(_options.PersistentSessionDays)));
        await _uow.SaveChangesAsync(ct);
        return generated.Plaintext;
    }

    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var s = _sessions.Current;
        _sessions.Clear();
        if (s is null) return;

        // Explicit logout revokes ALL of this user's persistent sessions so no
        // machine can silently re-enter after the user chose to sign out.
        try
        {
            var now = _time.UtcNow;
            var active = await _authSessions.ListAsync(
                new ActiveSessionsByUserSpec(s.UserId), ct);
            foreach (var session in active)
                session.Revoke(now);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DomainEx)
        {
            // Best-effort: the in-memory session is already cleared either way.
        }

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

/// <summary>Locates a persistent session by its peppered token hash.</summary>
public sealed class AuthSessionByHashSpec : Specification<AuthSession>
{
    public AuthSessionByHashSpec(string tokenHash) =>
        Criteria = x => x.TokenHash == tokenHash;
}

/// <summary>Locates a commander-issued one-time token by its peppered hash.</summary>
public sealed class CommanderTokenByHashSpec : Specification<CommanderToken>
{
    public CommanderTokenByHashSpec(string tokenHash) =>
        Criteria = x => x.TokenHash == tokenHash;
}

/// <summary>Active (non-revoked) sessions for a user, optionally scoped to one machine.</summary>
public sealed class ActiveSessionsByUserSpec : Specification<AuthSession>
{
    public ActiveSessionsByUserSpec(int userId, string? machineName = null)
    {
        Criteria = machineName is null
            ? x => x.UserId == userId && x.RevokedAtUtc == null
            : x => x.UserId == userId && x.RevokedAtUtc == null && x.MachineName == machineName;
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
    public Role Role => _sessions.Current?.Role ?? Role.Soldier;
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

    /// <summary>
    /// Sliding lifetime (days) of the persistent "remember me" session. Each
    /// successful auto-login rotates the token and restarts this window.
    /// </summary>
    public int PersistentSessionDays { get; set; } = 30;
}
