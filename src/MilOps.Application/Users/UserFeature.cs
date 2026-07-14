using FluentValidation;
using MediatR;
using MilOps.Application.Authentication;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;
using MilOps.Domain.Security;
using MilOps.Domain.ValueObjects;

namespace MilOps.Application.Users;

public record UserDto(int Id, string Username, string FullName, Role Role, bool IsActive,
    bool IsActivated);

public record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.UserManage;
}

public record CreateUserCommand(string FullName, string Username, Role Role, string Password)
    : IRequest<Result<int>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.UserManage;
}

public record ChangePasswordCommand(int UserId, string NewPassword)
    : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.UserManage;
}

public record DeactivateUserCommand(int UserId) : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.UserManage;
}

public record ChangeRoleCommand(int UserId, Role NewRole) : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.UserManage;
}

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator(AuthenticationOptions opts)
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Username).NotEmpty().Matches("^[a-zA-Z0-9._-]{3,40}$");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(opts.MinPasswordLength).MaximumLength(128);
    }
}

public class UserHandlers :
    IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>,
    IRequestHandler<CreateUserCommand, Result<int>>,
    IRequestHandler<ChangePasswordCommand, Result>,
    IRequestHandler<DeactivateUserCommand, Result>,
    IRequestHandler<ChangeRoleCommand, Result>
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditRepository _audit;

    public UserHandlers(IRepository<User> users, IUnitOfWork uow, ICurrentUser user,
        IPasswordHasher hasher, IAuditRepository audit)
    { _users = users; _uow = uow; _user = user; _hasher = hasher; _audit = audit; }

    public async Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery q, CancellationToken ct)
    {
        var items = await _users.ListAsync(null, ct);
        return items.Select(Map).ToList();
    }

    public async Task<Result<int>> Handle(CreateUserCommand c, CancellationToken ct)
    {
        try
        {
            var existing = await _users.FirstOrDefaultAsync(
                new UserByUsernameSpec(c.Username), ct);
            if (existing is not null)
                return Result.Failure<int>("USER_EXISTS", "این نام کاربری قبلاً ثبت شده است.");

            // New accounts stay unusable until the holder redeems a
            // commander-issued activation token at first login.
            var user = User.Create(
                PersonName.Create(c.FullName, "Full name"),
                c.Username, c.Role, _hasher.Hash(c.Password),
                requiresActivation: true);
            user.CreatedBy = _user.Username;
            _users.Add(user);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.UserCreated, _user.UserId, _user.Username,
                nameof(User), user.Id.ToString(), $"Created user {user.Username} ({user.Role})", ct);
            return Result.Success(user.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(ChangePasswordCommand c, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(c.UserId, ct);
        if (user is null) return Result.Failure("NOT_FOUND", "کاربر یافت نشد.");
        try
        {
            user.ChangePassword(_hasher.Hash(c.NewPassword));
            user.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.PasswordChanged, _user.UserId, _user.Username,
                nameof(User), user.Id.ToString(), $"Password changed for {user.Username}", ct);

            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(DeactivateUserCommand c, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(c.UserId, ct);
        if (user is null) return Result.Failure("NOT_FOUND", "کاربر یافت نشد.");
        if (user.Id == _user.UserId)
            return Result.Failure("USER_SELF", "امکان غیرفعال‌سازی حساب خود وجود ندارد.");
        try
        {
            user.Deactivate();
            user.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.UserDeactivated, _user.UserId, _user.Username,
                nameof(User), user.Id.ToString(), $"Deactivated {user.Username}", ct);

            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(ChangeRoleCommand c, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(c.UserId, ct);
        if (user is null) return Result.Failure("NOT_FOUND", "کاربر یافت نشد.");
        if (user.Id == _user.UserId)
            return Result.Failure("USER_SELF", "امکان تغییر نقش حساب خود وجود ندارد.");
        if (user.Role == c.NewRole) return Result.Success();
        try
        {
            var oldRole = user.Role;
            user.ChangeRole(c.NewRole);
            user.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.UserUpdated, _user.UserId, _user.Username,
                nameof(User), user.Id.ToString(),
                $"Role of {user.Username} changed {oldRole} -> {c.NewRole}", ct);

            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }

    private static UserDto Map(User u) =>
        new(u.Id, u.Username, u.FullName.ToString(), u.Role, u.IsActive, u.IsActivated);
}
