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

        // Persian validation messages + field display names, app-wide.
        ValidationLocalization.Apply();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.Configure<AuthenticationOptions>(config.GetSection("Authentication"));
        // Some validators (e.g. CreateUserValidator) take the options object
        // directly; expose the bound instance alongside IOptions<T>.
        services.AddSingleton(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthenticationOptions>>().Value);

        // Pipeline behavior order: auth first (fail fast), then validation, then logging wrapper.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));

        // The session spans the whole app run (one signed-in operator per
        // desktop instance), and the shell opens a fresh DI scope per module
        // navigation. So the session store MUST be a singleton — a scoped
        // registry would give every module scope an empty session and every
        // request would fail with "Authentication required". The ICurrentUser
        // adapter stays scoped and simply reads the singleton session.
        services.AddSingleton<ISessionRegistry, SessionRegistry>();
        services.AddScoped<ICurrentUser, CurrentUserAdapter>();

        services.AddSingleton<IDateTime, SystemDateTime>();

        return services;
    }
}
