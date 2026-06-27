using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MilOps.Infrastructure.Security;

// SQLitePCL.Batteries_V2 lives in the SQLitePCLRaw.batteries_v2 assembly (facade).
using SQLitePCL;

namespace MilOps.Infrastructure.Db;

/// <summary>
/// Builds <see cref="MilOpsDbContext"/> instances backed by an encrypted
/// SQLCipher database. The encryption key is unwrapped from DPAPI/TPM on demand
/// and injected into the connection via the SQLCipher PRAGMA key. The key never
/// appears in appsettings or connection strings persisted to disk.
/// </summary>
public sealed class EncryptedDbContextFactory
{
    private readonly SecretProtector _secrets;
    private readonly SecurityOptions _options;
    private readonly ILoggerFactory _logging;
    private readonly ILogger<EncryptedDbContextFactory> _logger;
    private readonly string _dbPath;

    // SQLCipher KDF iteration count. MUST stay stable for the life of a database
    // file: changing it after creation means the existing key derivation won't
    // match and the DB reads as "not a database" (SQLITE_NOTADB / error 26).
    private const int KdfIterations = 64000;

    static EncryptedDbContextFactory()
    {
        // CRITICAL: select the SQLCipher (encrypted) engine. Without this explicit
        // initialization, Microsoft.Data.Sqlite falls back to the plain e_sqlite3
        // engine, which cannot decrypt a SQLCipher file and raises SQLITE_NOTADB
        // (SQLite Error 26). Done once per process.
        //
        // The bundle_e_sqlcipher package's Batteries_V2.Init() registers the
        // e_sqlcipher provider as the active SQLite engine and freezes the choice.
        SQLitePCL.Batteries_V2.Init();
    }

    public EncryptedDbContextFactory(SecretProtector secrets, IOptions<SecurityOptions> options,
        ILoggerFactory logging, ILogger<EncryptedDbContextFactory> logger)
    {
        _secrets = secrets;
        _options = options.Value;
        _logging = logging;
        _logger = logger;
        var dataDir = !string.IsNullOrWhiteSpace(_options.SecretsDirectory)
            ? Path.GetDirectoryName(_options.SecretsDirectory)!
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MilOps");
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "milops.db");
    }

    public MilOpsDbContext Create()
    {
        var key = _secrets.UnprotectOrCreate(SecretPurposes.DatabaseKey, _options.DatabaseKeyBytes);
        try
        {
            var keyHex = ToHex(key);

            // Open WITHOUT putting the key on the connection string. We supply the
            // key via PRAGMA key immediately after open so we fully control the
            // ordering relative to the other cipher pragmas.
            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
                // NOTE: no Password here; key is applied via PRAGMA below.
            }.ToString();

            var connection = new SqliteConnection(connStr);
            connection.Open();
            ApplyKeyAndCipherPragmas(connection, keyHex);
            VerifyReadable(connection);

            var options = new DbContextOptionsBuilder<MilOpsDbContext>()
                .UseSqlite(connection, sql => sql.MigrationsAssembly(typeof(MilOpsDbContext).Assembly.FullName))
                .UseLoggerFactory(_logging)
                .EnableSensitiveDataLogging(false)   // NEVER log parameter values (secrets/PII)
                .Options;

            return new MilOpsDbContext(options);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Applies the SQLCipher key and cipher settings in the order SQLCipher
    /// requires. For a NEW database these defaults are baked in at creation; for
    /// an EXISTING database the defaults are ignored (the file already encodes
    /// its KDF/page-size) and only <c>PRAGMA key</c> matters.
    /// </summary>
    private static void ApplyKeyAndCipherPragmas(SqliteConnection connection, string keyHex)
    {
        // PRAGMA key must be FIRST, before any other cipher pragma. The
        // "x'...'" raw-key form bypasses SQLCipher's own PBKDF2 key derivation
        // and uses the bytes directly (we already generated strong CSPRNG bytes
        // and protect them with DPAPI/TPM, so re-deriving would add no security).
        using var keyCmd = connection.CreateCommand();
        keyCmd.CommandText = $"PRAGMA key = \"x'{keyHex}'\";";
        keyCmd.ExecuteNonQuery();

        // Defaults only take effect when the database is first created. On an
        // existing DB they are read from the file header, so setting them again
        // is harmless and keeps creation/reopen consistent.
        using var cipherCmd = connection.CreateCommand();
        cipherCmd.CommandText = $"""
            PRAGMA cipher_default_kdf_iter = {KdfIterations};
            PRAGMA cipher_default_use_hmac = ON;
            PRAGMA cipher_page_size = 4096;
            PRAGMA foreign_keys = ON;
            """;
        cipherCmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Prove the key is correct. For a brand-new file this creates the schema
    /// header; for an existing file a wrong key makes this fail with
    /// <c>SQLITE_NOTADB</c> (error 26), which we surface as a clear message.
    /// </summary>
    private void VerifyReadable(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            cmd.ExecuteScalar();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26)
        {
            _logger.LogCritical(ex,
                "Database key verification failed (SQLITE_NOTADB). The DPAPI/TPM-protected key " +
                "does not match the existing encrypted database file at {Path}.", _dbPath);
            throw new InvalidOperationException(
                "The encrypted database could not be opened (SQLITE_NOTADB). The stored key does " +
                "not match the existing database file. If you restored an old database, restore " +
                "its matching secrets too (see %LOCALAPPDATA%\\MilOps\\secrets).", ex);
        }
    }

    private static string ToHex(byte[] b)
    {
        var c = new char[b.Length * 2];
        const string hex = "0123456789abcdef";
        for (var i = 0; i < b.Length; i++)
        {
            c[i * 2] = hex[b[i] >> 4];
            c[i * 2 + 1] = hex[b[i] & 0xF];
        }
        return new string(c);
    }
}

/// <summary>
/// Scoped accessor that hands out a single DbContext per scope. The Application
/// layer depends on this abstraction (not on DbContext directly) to stay
/// persistence-agnostic. Disposing the scope disposes the context.
/// </summary>
public sealed class DbContextAccessor : IDisposable, IAsyncDisposable
{
    private readonly EncryptedDbContextFactory _factory;
    private MilOpsDbContext? _context;
    public DbContextAccessor(EncryptedDbContextFactory factory) => _factory = factory;

    public MilOpsDbContext Context => _context ??= _factory.Create();

    /// <summary>The lazy audit reader used by the HMAC verifier.</summary>
    public MilOpsDbContext.AuditDbContextAccessor AuditReader => new(Context);

    public void Dispose() => _context?.Dispose();

    public ValueTask DisposeAsync()
    {
        var ctx = _context;
        _context = null;
        return ctx is null ? ValueTask.CompletedTask : ctx.DisposeAsync();
    }
}
