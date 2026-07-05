using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MilOps.Presentation.Services;

/// <summary>
/// Persists the "remember me" session token on disk, encrypted with Windows
/// DPAPI (CurrentUser scope + app-specific entropy). Security properties:
///   - The file is unreadable by other Windows accounts and useless if copied
///     to another machine/user profile (DPAPI key derivation).
///   - The DB stores only a peppered SHA-256 of this token, so DB + file are
///     each useless alone; the pepper itself is DPAPI/TPM protected.
///   - Tokens are single-use: every successful auto-login rotates the value,
///     so a silently copied file dies at the next legitimate app start.
///   - Any read/decrypt failure deletes the file (fail closed → normal login).
/// </summary>
public interface ISessionTokenStore
{
    string? TryLoad();
    void Save(string token);
    void Delete();
}

public sealed class SessionTokenStore : ISessionTokenStore
{
    // App-specific DPAPI entropy: not a secret, but binds blobs to this app so
    // other same-user processes cannot decrypt them by accident.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MilOps.SessionToken.v1");

    private readonly ILogger<SessionTokenStore> _logger;
    private readonly string _path;

    public SessionTokenStore(ILogger<SessionTokenStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MilOps");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "session.bin");
    }

    public string? TryLoad()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var blob = File.ReadAllBytes(_path);
            var plain = ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or FormatException)
        {
            // Corrupt, tampered, or foreign-profile blob: fail closed.
            _logger.LogWarning(ex, "Stored session token unreadable; deleting it.");
            Delete();
            return null;
        }
    }

    public void Save(string token)
    {
        var plain = Encoding.UTF8.GetBytes(token);
        try
        {
            var blob = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_path, blob);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            // Persistence is a convenience; never block login over it.
            _logger.LogWarning(ex, "Failed to persist session token.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public void Delete()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to delete session token file.");
        }
    }
}
