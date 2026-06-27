using MediatR;
using Microsoft.Extensions.Logging;

namespace MilOps.Application.Behaviors;

/// <summary>
/// Logs unhandled exceptions from any handler at Error level, with the request
/// type name (never the payload, which may contain secrets) for diagnostics.
/// </summary>
public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    public UnhandledExceptionBehavior(ILogger<TRequest> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try { return await next(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error handling {Request}", typeof(TRequest).Name);
            throw;
        }
    }
}
