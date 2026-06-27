using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilOps.Application.Common;
using MilOps.Domain.Repositories;
using MilOps.Domain.Security;
using MilOps.Infrastructure.Db;
using MilOps.Infrastructure.Persistence;
using MilOps.Infrastructure.Security;

namespace MilOps.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, security, and auditing services.
///
    /// Lifetime notes:
    ///   - <see cref="MilOpsDbContext"/> is SCOPED (one per DI scope / request).
    ///   - <see cref="DbContextAccessor"/> is SCOPED and owns that scope's context.
    ///   - Repositories are SCOPED and resolve the scoped DbContext.
    ///   - <see cref="SecretProtector"/>/<see cref="TpmKeyProtector"/> are SINGLETON
    ///     (read-once secrets, cached).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SecurityOptions>(config.GetSection("Security"));

        // Security: secret protection + crypto ports
        services.TryAddSingleton<TpmKeyProtector>();
        services.TryAddSingleton<SecretProtector>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenGenerator, CryptoTokenGenerator>();
        services.AddScoped<IAuditHasher, HmacAuditHasher>();

        // Persistence: encrypted DbContext + accessor + repositories
        services.TryAddSingleton<EncryptedDbContextFactory>();
        services.AddScoped<DbContextAccessor>();
        services.AddScoped<MilOpsDbContext>(sp => sp.GetRequiredService<DbContextAccessor>().Context);

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAuditRepository, EfAuditRepository>();

        services.AddScoped<DatabaseInitializer>();
        return services;
    }

    /// <summary>Convenience: initialize + seed the encrypted database on startup.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var init = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await init.InitializeAsync(ct);
    }
}
