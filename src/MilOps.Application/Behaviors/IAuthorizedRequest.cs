using MilOps.Application.Security;

namespace MilOps.Application.Behaviors;

/// <summary>
/// Marker for requests that require a specific permission. Handlers are
/// authorized automatically via <see cref="AuthorizationBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IAuthorizedRequest
{
    Permission RequiredPermission { get; }
}
