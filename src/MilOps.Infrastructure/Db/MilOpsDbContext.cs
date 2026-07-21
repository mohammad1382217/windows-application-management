using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using MilOps.Domain.Common;
using MilOps.Domain.Entities;
using MilOps.Domain.Enums;
using MilOps.Domain.Exceptions;

namespace MilOps.Infrastructure.Db;

/// <summary>
/// EF Core DbContext for MilOps. The connection string (SQLCipher connection
/// with the encryption key) is supplied at runtime by
/// <see cref="DbContextFactory"/> after the DB key has been unwrapped; it never
/// lives in appsettings. Value objects are persisted as strings via conversions.
/// </summary>
public class MilOpsDbContext : DbContext
{
    public MilOpsDbContext(DbContextOptions<MilOpsDbContext> options) : base(options) { }

    /// <summary>
    /// SQLite does not auto-generate rowversion values (that is a SQL Server
    /// feature). EF Core's <c>IsRowVersion()</c> therefore EXCLUDES the column
    /// from INSERT statements, expecting the DB to fill it — which never
    /// happens. We compensate by assigning a random byte[] to any new entity
    /// whose RowVersion is still empty <b>before</b> EF Core builds the INSERT
    /// command. This preserves optimistic-concurrency checking on UPDATE while
    /// letting INSERTs succeed on SQLite.
    /// </summary>
    private void SeedRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.RowVersion.Length == 0)
                entry.Entity.RowVersion = RandomNumberGenerator.GetBytes(8);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SeedRowVersions();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("CONCURRENCY",
                "Another user has modified the same record. Please refresh and try again.");
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException("DB_ERROR", TranslateDbException(ex), ex);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SeedRowVersions();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("CONCURRENCY",
                "Another user has modified the same record. Please refresh and try again.");
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException("DB_ERROR", TranslateDbException(ex), ex);
        }
    }

    /// <summary>
    /// Inspects the entries that caused the <see cref="DbUpdateException"/> and
    /// returns a human-readable message, including the provider's inner
    /// exception text so the root cause is never hidden behind a generic message.
    /// </summary>
    private static string TranslateDbException(DbUpdateException ex)
    {
        var innerMsg = ex.InnerException?.Message ?? string.Empty;
        var entries = ex.Entries;
        var entityDisplay = entries.Count > 0 ? entries[0].Metadata.ClrType.Name : "record";

        // Try to extract a natural key from the tracked entity for a better message.
        if (entries.Count > 0)
        {
            var naturalKey = TryGetNaturalKey(entries[0].Entity);
            if (naturalKey is not null)
                entityDisplay = $"{entityDisplay} '{naturalKey}'";
        }

        if (innerMsg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || innerMsg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
        {
            return $"{entityDisplay} already exists. A record with this value conflicts with a unique constraint.";
        }

        if (innerMsg.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            return entries.Count > 0 && entries[0].State == EntityState.Deleted
                ? $"Cannot delete {entityDisplay} because it is referenced by other records."
                : $"Cannot save {entityDisplay}: the referenced record does not exist.";
        }

        // Fall back to the provider's own message so we never just say
        // "an error occurred" without the actual reason.
        return string.IsNullOrWhiteSpace(innerMsg)
            ? $"An error occurred while saving {entityDisplay}."
            : $"An error occurred while saving {entityDisplay}: {innerMsg}";
    }

    /// <summary>
    /// Attempts to read a human-readable identifier from the entity so the
    /// error message can say "User 'commander'" instead of just "User".
    /// </summary>
    private static string? TryGetNaturalKey(object entity) => entity switch
    {
        User u => u.Username,
        Soldier s => s.PersonnelCode.Value,
        Weapon w => w.WeaponNumber,
        _ => null
    };


    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<Soldier> Soldiers => Set<Soldier>();
    public DbSet<DepartmentHistory> DepartmentHistoryEntries => Set<DepartmentHistory>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<CommanderToken> Tokens => Set<CommanderToken>();
    public DbSet<GuardSchedule> GuardSchedules => Set<GuardSchedule>();
    public DbSet<GuardAssignment> GuardAssignments => Set<GuardAssignment>();
    public DbSet<GuardPostRegisterEntry> GuardPostRegister => Set<GuardPostRegisterEntry>();
    public DbSet<Weapon> Weapons => Set<Weapon>();
    public DbSet<WeaponAssignment> WeaponAssignments => Set<WeaponAssignment>();
    public DbSet<LeaveRecord> Leaves => Set<LeaveRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Small read-only facade used by the audit verifier so it can read audit rows
    /// without depending on the full DbContext graph.
    /// </summary>
    public sealed class AuditDbContextAccessor
    {
        private readonly MilOpsDbContext _db;
        public AuditDbContextAccessor(MilOpsDbContext db) => _db = db;

        public Task<List<AuditLog>> GetOrderedAsync(CancellationToken ct) =>
            _db.AuditLogs.AsNoTracking().OrderBy(a => a.Sequence).ToListAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // User
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(80);
            e.Property(x => x.UpdatedBy).HasMaxLength(80);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            // PersonName value object -> string (persist Value only)
            e.OwnsOne(x => x.FullName, n =>
            {
                n.Property(p => p.Value).HasColumnName("FullName").HasMaxLength(80).IsRequired();
            });
        });

        // AuthSession (persistent "remember me" sessions; only peppered hashes stored)
        b.Entity<AuthSession>(e =>
        {
            e.ToTable("auth_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.MachineName).HasMaxLength(60);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.CreatedBy).HasMaxLength(80);
            e.Property(x => x.UpdatedBy).HasMaxLength(80);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Soldier
        b.Entity<Soldier>(e =>
        {
            e.ToTable("soldiers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Rank).HasMaxLength(40).IsRequired();
            e.Property(x => x.HealthType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.EntryDate).HasConversion<string>();
            e.Property(x => x.ServiceStartDate).HasConversion<string>();
            e.Property(x => x.ServiceEndDate).HasConversion<string>();
            e.Property(x => x.DepartmentName).HasMaxLength(80);
            e.Property(x => x.CreatedBy).HasMaxLength(80);
            e.Property(x => x.UpdatedBy).HasMaxLength(80);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.OwnsOne(x => x.FirstName, n => n.Property(p => p.Value).HasColumnName("FirstName").HasMaxLength(60).IsRequired());
            e.OwnsOne(x => x.LastName, n => n.Property(p => p.Value).HasColumnName("LastName").HasMaxLength(60).IsRequired());
            e.OwnsOne(x => x.FatherName, n => n.Property(p => p.Value).HasColumnName("FatherName").HasMaxLength(60));
            e.OwnsOne(x => x.NationalCode, n => n.Property(p => p.Value).HasColumnName("NationalCode").HasMaxLength(10).IsRequired());
            e.OwnsOne(x => x.PersonnelCode, n => n.Property(p => p.Value).HasColumnName("PersonnelCode").HasMaxLength(12).IsRequired());
        });

        // DepartmentHistory
        b.Entity<DepartmentHistory>(e =>
        {
            e.ToTable("department_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.DepartmentName).HasMaxLength(80).IsRequired();
            e.Property(x => x.EffectiveFrom).HasConversion<string>();
            e.Property(x => x.EffectiveTo).HasConversion<string>();
            e.Property(x => x.CreatedBy).HasMaxLength(80);
            e.Property(x => x.UpdatedBy).HasMaxLength(80);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasIndex(x => new { x.SoldierId, x.EffectiveFrom });
            e.HasOne<Soldier>().WithMany().HasForeignKey(x => x.SoldierId).OnDelete(DeleteBehavior.Cascade);
        });

        // AttendanceRecord
        b.Entity<AttendanceRecord>(e =>
        {
            e.ToTable("attendance_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.CreatedBy).HasMaxLength(80);
            e.Property(x => x.UpdatedBy).HasMaxLength(80);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasIndex(x => new { x.SoldierId, x.Date }).IsUnique();
            e.HasOne<Soldier>().WithMany().HasForeignKey(x => x.SoldierId).OnDelete(DeleteBehavior.Cascade);
        });

        // CommanderToken
        b.Entity<CommanderToken>(e =>
        {
            e.ToTable("tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.TokenPreview).HasMaxLength(40);
            e.Property(x => x.Rank).HasMaxLength(40);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.ServiceStartDate).HasConversion<string>();
            e.Property(x => x.ServiceEndDate).HasConversion<string>();
            e.Property(x => x.RevocationReason).HasMaxLength(300);
            e.OwnsOne(x => x.FirstName, n => n.Property(p => p.Value).HasColumnName("FirstName").HasMaxLength(60).IsRequired());
            e.OwnsOne(x => x.LastName, n => n.Property(p => p.Value).HasColumnName("LastName").HasMaxLength(60).IsRequired());
            e.OwnsOne(x => x.NationalCode, n => n.Property(p => p.Value).HasColumnName("NationalCode").HasMaxLength(10).IsRequired());
            e.OwnsOne(x => x.PersonnelCode, n => n.Property(p => p.Value).HasColumnName("PersonnelCode").HasMaxLength(12).IsRequired());
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.Status);
        });

        // GuardSchedule + child GuardAssignment
        b.Entity<GuardSchedule>(e =>
        {
            e.ToTable("guard_schedules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            var extra = new[] { "ArmedForceMorning1", "ArmedForceMorning2", "ArmedForceMorning3",
                "Watchman", "Armament", "Refuge", "ShelterManager" };
            foreach (var col in extra)
                e.Property<string?>(col).HasMaxLength(120);
            e.HasMany(x => x.Assignments).WithOne().HasForeignKey(a => a.GuardScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Metadata.FindNavigation(nameof(GuardSchedule.Assignments))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<GuardAssignment>(e =>
        {
            e.ToTable("guard_assignments");
            e.HasKey(x => x.Id);
            // NOTE: the schedule<->assignment relationship is configured ONCE, on
            // GuardSchedule (HasMany(Assignments)...HasForeignKey(GuardScheduleId)).
            // A second HasOne<GuardSchedule>().WithMany() here used to create a
            // DUPLICATE relationship with a shadow FK column (GuardScheduleId1),
            // which left the real GuardScheduleId at 0 on insert and made every
            // schedule save fail with a foreign-key violation.
            e.Property(x => x.Post).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.Shift).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ShiftStart).HasConversion<string>();
            e.Property(x => x.ShiftEnd).HasConversion<string>();
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasIndex(x => new { x.GuardScheduleId, x.Post, x.Shift }).IsUnique();
        });

        // GuardPostRegister
        b.Entity<GuardPostRegisterEntry>(e =>
        {
            e.ToTable("guard_post_register");
            e.HasKey(x => x.Id);
            e.Property(x => x.Date).HasConversion<string>();
            e.Property(x => x.Time).HasConversion<string>();
            e.Property(x => x.Post).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.WeaponNumber).HasMaxLength(30).IsRequired();
            e.Property(x => x.Signature).HasMaxLength(120);
            e.Property(x => x.Remarks).HasMaxLength(300);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        // Weapon + assignment history
        b.Entity<Weapon>(e =>
        {
            e.ToTable("weapons");
            e.HasKey(x => x.Id);
            e.Property(x => x.WeaponNumber).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.WeaponNumber).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Model).HasMaxLength(60);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasMany(x => x.History).WithOne().HasForeignKey(a => a.WeaponId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Metadata.FindNavigation(nameof(Weapon.History))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<WeaponAssignment>(e =>
        {
            e.ToTable("weapon_assignments");
            e.HasKey(x => x.Id);
            // NOTE: the weapon<->assignment relationship is configured ONCE, on
            // Weapon (HasMany(History)...HasForeignKey(WeaponId)). A second
            // HasOne<Weapon>().WithMany() here used to create a DUPLICATE
            // relationship with a shadow FK column (WeaponId1) — same defect
            // that broke guard_assignments.
            e.Property(x => x.Note).HasMaxLength(200);
        });

        // Leave
        b.Entity<LeaveRecord>(e =>
        {
            e.ToTable("leaves");
            e.HasKey(x => x.Id);
            e.Property(x => x.StartDate).HasConversion<string>();
            e.Property(x => x.EndDate).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        // AuditLog (append-only; no update/delete path exposed)
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sequence).ValueGeneratedNever();
            e.HasIndex(x => x.Sequence).IsUnique();
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.Username).HasMaxLength(80);
            e.Property(x => x.Category).HasMaxLength(60);
            e.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(60);
            e.Property(x => x.Details).HasMaxLength(1000);
            e.Property(x => x.MachineName).HasMaxLength(60);
            e.Property(x => x.PreviousHash).HasMaxLength(128);
            e.Property(x => x.RowHash).HasMaxLength(128).IsRequired();
        });
    }
}
