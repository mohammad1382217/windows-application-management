using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MilOps.Application.Authentication;
using MilOps.Application.Behaviors;
using MilOps.Application.Common;
using MilOps.Application.Security;

namespace MilOps.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR, FluentValidation, the pipeline behaviors (auth,
    /// validation, exception logging), and the session/current-user services.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.Configure<AuthenticationOptions>(config.GetSection("Authentication"));

        // Pipeline behavior order: auth first (fail fast), then validation, then logging wrapper.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));

        // Session-scoped services.
        services.AddScoped<ISessionRegistry, SessionRegistry>();
        services.AddScoped<ICurrentUser, CurrentUserAdapter>();

        services.AddSingleton<IDateTime, SystemDateTime>();

        return services;
    }
}
