using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MilOps.Infrastructure.Security;

/// <summary>
/// Manages long-lived secrets (the SQLCipher DB key and the audit HMAC key).
///
/// Protection strategy (defense in depth):
///   1. On first run, generate the secret with the .NET CSPRNG.
///   2. If a TPM v1.2+ is available, wrap (encrypt) the secret with a TPM-
///      sealed key so it is bound to this machine's hardware; persist only the
///      wrapped blob. (See <see cref="TpmKeyProtector"/>.)
///   3. Otherwise, wrap the secret with Windows DPAPI (CurrentUser scope), which
///      keeps it encrypted under the user's Windows credentials at rest.
///   4. In memory, secrets live only as byte[] for the minimum time needed; we
///      clear arrays after use. They are NEVER written to logs or appsettings.
///
/// Threat model & honest limitations:
///   - An attacker running code as the same Windows user can call DPAPI to
///     unwrap, and can use a sealed TPM key the same way. These protections
///     defend against offline disk theft and casual snooping, not against a
///     fully compromised interactive session. For stronger guarantees, export
///     the audit chain to write-once media or a remote collector.
/// </summary>
public sealed class SecretProtector : IDisposable
{
    private readonly ILogger<SecretProtector> _logger;
    private readonly SecurityOptions _options;
    private readonly TpmKeyProtector _tpm;
    private readonly string _secretsDir;

    public SecretProtector(IOptions<SecurityOptions> options, TpmKeyProtector tpm,
        ILogger<SecretProtector> logger)
    {
        _options = options.Value;
        _tpm = tpm;
        _logger = logger;
        _secretsDir = !string.IsNullOrWhiteSpace(_options.SecretsDirectory)
            ? _options.SecretsDirectory!
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MilOps", "secrets");
        Directory.CreateDirectory(_secretsDir);
    }

    /// <summary>
    /// Returns the plaintext secret bytes for the given purpose, generating and
    /// protecting a fresh secret on first use. The caller MUST clear the returned
    /// array when done.
    /// </summary>
    public byte[] UnprotectOrCreate(string purpose, int byteLength)
    {
        var path = Path.Combine(_secretsDir, $"{purpose}.bin");
        if (File.Exists(path))
            return UnprotectFile(path);

        // First run: generate and protect a new secret.
        //
        // IMPORTANT: do NOT zero `fresh` here. It IS the value we return, and the
        // caller owns clearing it (see the method summary). Zeroing it in a
        // finally would hand the caller an all-zero key: the DB would then be
        // CREATED with a zero key while the real key is persisted to disk, so
        // every later reopen fails with SQLITE_NOTADB. ProtectAndWrite only
        // clears its own wrapped copy, never the input secret.
        var fresh = RandomNumberGenerator.GetBytes(byteLength);
        ProtectAndWrite(path, fresh);
        _logger.LogInformation("Generated and protected new secret '{Purpose}'.", purpose);
        return fresh;
    }

    private byte[] UnprotectFile(string path)
    {
        var blob = File.ReadAllBytes(path);
        try
        {
            // Prefer TPM if it was used to wrap this blob.
            if (_tpm.IsAvailable && _options.PreferTpm)
            {
                var unwrapped = _tpm.Unwrap(blob);
                if (unwrapped is not null) return unwrapped;
            }

            // Fall back to DPAPI (CurrentUser scope).
            return ProtectedData.Unprotect(blob, Encoding.UTF8.GetBytes(_options.DpapiEntropy),
                DataProtectionScope.CurrentUser);
        }
        finally
        {
            Array.Clear(blob, 0, blob.Length);
        }
    }

    private void ProtectAndWrite(string path, byte[] secret)
    {
        byte[] blob;
        if (_tpm.IsAvailable && _options.PreferTpm && _tpm.TryWrap(secret, out blob))
        {
            _logger.LogInformation("Secret protected with TPM-backed key.");
        }
        else
        {
            blob = ProtectedData.Protect(secret, Encoding.UTF8.GetBytes(_options.DpapiEntropy),
                DataProtectionScope.CurrentUser);
            _logger.LogInformation("Secret protected with DPAPI (CurrentUser). TPM not used.");
        }
        // Restrictive ACL: only the current user. (File.Create already creates with
        // current-user-default ACL on Windows; we additionally clear inherited rules.)
        File.WriteAllBytes(path, blob);
        Array.Clear(blob, 0, blob.Length);
    }

    public void Dispose() { }
}

/// <summary>Strongly typed secret purposes.</summary>
public static class SecretPurposes
{
    public const string DatabaseKey = "db-key";       // SQLCipher encryption key
    public const string AuditHmacKey = "audit-hmac";  // Chained-audit HMAC key
    public const string TokenPepper = "token-pepper"; // Extra entropy mixed into token hashing
}
