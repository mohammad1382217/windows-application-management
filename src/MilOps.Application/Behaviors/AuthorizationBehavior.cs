using MediatR;
using MilOps.Application.Security;

namespace MilOps.Application.Behaviors;

/// <summary>
/// Enforces RBAC: any request implementing <see cref="IAuthorizedRequest"/> is
/// checked against the current user's role before the handler runs.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _user;
    public AuthorizationBehavior(ICurrentUser user) => _user = user;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IAuthorizedRequest authed)
        {
            if (!_user.IsAuthenticated)
                throw new AuthorizationException("Authentication required.");
            if (!_user.Has(authed.RequiredPermission))
                throw new AuthorizationException(
                    $"Your role ({_user.Role}) is not permitted to perform this action.");
        }
        return await next();
    }
}
