using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MilOps.Infrastructure.Db;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can construct the
/// <see cref="MilOpsDbContext"/> without the live DPAPI/TPM key pipeline.
/// It points at a throwaway unencrypted SQLite file used ONLY for scaffolding
/// the migration; at runtime the encrypted factory is used instead.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MilOpsDbContext>
{
    public MilOpsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MilOpsDbContext>()
            .UseSqlite("Data Source=milops-design.db")
            .Options;
        return new MilOpsDbContext(options);
    }
}
