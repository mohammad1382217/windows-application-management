using Microsoft.EntityFrameworkCore;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Security;
using MilOps.Domain.ValueObjects;

namespace MilOps.Infrastructure.Db;

/// <summary>
/// Initializes the encrypted database: applies schema and seeds a default
/// Commander account on first run (with a password that MUST be changed at
/// first login). The default password is intentionally documented and weak so
/// operators are forced to rotate it; the seed flags it for mandatory change.
/// </summary>
public sealed class DatabaseInitializer
{
    public const string DefaultCommanderUsername = "commander";
    public const string DefaultCommanderPassword = "ChangeMe!2024"; // MUST be changed on first login
    private const string DefaultSeedFullName = "System Commander";

    private readonly MilOpsDbContext _db;
    private readonly IPasswordHasher _hasher;

    public DatabaseInitializer(MilOpsDbContext db, IPasswordHasher hasher)
    { _db = db; _hasher = hasher; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);

        if (!await _db.Users.AnyAsync(ct))
        {
            var commander = User.Create(
                PersonName.Create(DefaultSeedFullName, "Full name"),
                DefaultCommanderUsername, Role.Commander,
                _hasher.Hash(DefaultCommanderPassword));
            commander.CreatedBy = "system-seed";
            _db.Users.Add(commander);
            await _db.SaveChangesAsync(ct);
        }
    }
}
