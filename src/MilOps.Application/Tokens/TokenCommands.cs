using FluentValidation;
using MediatR;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;
using MilOps.Domain.Repositories;
using MilOps.Domain.Security;
using MilOps.Domain.ValueObjects;

namespace MilOps.Application.Tokens;

// ============================================================
// Generate
// ============================================================

public record GenerateTokenCommand(
    string FirstName, string LastName, string NationalCode, string PersonnelCode,
    string Rank, DateOnly ServiceStartDate, DateOnly ServiceEndDate,
    TokenPurpose Purpose, int ValidDays = 7)
    : IRequest<Result<GeneratedTokenDto>>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.TokenManage;
}

public record RevokeTokenCommand(int Id, string Reason)
    : IRequest<Result>, IAuthorizedRequest
{
    public Permission RequiredPermission => Permission.TokenManage;
}

public class GenerateTokenValidator : AbstractValidator<GenerateTokenCommand>
{
    public GenerateTokenValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.NationalCode).NotEmpty().Matches("^[0-9]{10}$");
        RuleFor(x => x.PersonnelCode).NotEmpty().MaximumLength(12);
        RuleFor(x => x.Rank).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ServiceEndDate).GreaterThan(x => x.ServiceStartDate);
        RuleFor(x => x.ValidDays).InclusiveBetween(1, 90);
    }
}

public class TokenCommandHandlers :
    IRequestHandler<GenerateTokenCommand, Result<GeneratedTokenDto>>,
    IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IRepository<CommanderToken> _tokens;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IDateTime _time;
    private readonly ITokenGenerator _generator;
    private readonly IAuditRepository _audit;

    public TokenCommandHandlers(IRepository<CommanderToken> tokens, IUnitOfWork uow,
        ICurrentUser user, IDateTime time, ITokenGenerator generator, IAuditRepository audit)
    { _tokens = tokens; _uow = uow; _user = user; _time = time;
      _generator = generator; _audit = audit; }

    public async Task<Result<GeneratedTokenDto>> Handle(GenerateTokenCommand c, CancellationToken ct)
    {
        try
        {
            var generated = _generator.Generate(c.Purpose);
            var expiresAt = _time.UtcNow.AddDays(c.ValidDays);

            var token = CommanderToken.Create(
                PersonName.Create(c.FirstName, "First name"),
                PersonName.Create(c.LastName, "Last name"),
                NationalCode.Create(c.NationalCode),
                PersonnelCode.Create(c.PersonnelCode),
                c.Rank, c.ServiceStartDate, c.ServiceEndDate,
                c.Purpose, generated.Hash, generated.Preview,
                expiresAt, _user.UserId ?? 0);

            token.CreatedBy = _user.Username;
            _tokens.Add(token);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.TokenGenerated, _user.UserId, _user.Username,
                nameof(CommanderToken), token.Id.ToString(),
                $"صدور توکن برای {c.FirstName} {c.LastName} (کد ملی: {c.NationalCode})", ct);

            // Plaintext returned exactly once; only the hash is stored.
            return Result.Success(new GeneratedTokenDto(
                token.Id, generated.Plaintext, generated.Preview,
                c.FirstName, c.LastName, c.NationalCode, c.PersonnelCode, c.Rank,
                c.ServiceStartDate, c.ServiceEndDate, c.Purpose, expiresAt));
        }
        catch (DomainException ex) { return Result.Failure<GeneratedTokenDto>(ex.Code, ex.Message); }
    }

    public async Task<Result> Handle(RevokeTokenCommand c, CancellationToken ct)
    {
        var token = await _tokens.GetByIdAsync(c.Id, ct);
        if (token is null) return Result.Failure("NOT_FOUND", "توکن یافت نشد.");

        try
        {
            token.Revoke(c.Reason, _time.UtcNow);
            token.Touch(_user.Username);
            await _uow.SaveChangesAsync(ct);

            await _audit.AppendAsync(AuditAction.TokenRevoked, _user.UserId, _user.Username,
                nameof(CommanderToken), token.Id.ToString(), $"ابطال توکن: {c.Reason}", ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Code, ex.Message); }
    }
}
