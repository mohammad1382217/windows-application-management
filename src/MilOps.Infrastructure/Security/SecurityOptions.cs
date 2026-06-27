namespace MilOps.Infrastructure.Security;

/// <summary>Security-related configuration, bound from appsettings.json.</summary>
public sealed class SecurityOptions
{
    /// <summary>Directory where DPAPI/TPM-wrapped secret blobs are stored.</summary>
    public string? SecretsDirectory { get; set; }

    /// <summary>
    /// Application-specific entropy mixed into DPAPI protection. Not secret;
    /// adds another domain-separation layer so another app under the same user
    /// cannot trivially re-use the protected blob.
    /// </summary>
    public string DpapiEntropy { get; set; } = "MilOps-v1-secret-entropy";

    /// <summary>Prefer TPM-backed wrapping when a TPM is present.</summary>
    public bool PreferTpm { get; set; } = true;

    /// <summary>Length (in bytes) of the generated SQLCipher key (256-bit).</summary>
    public int DatabaseKeyBytes { get; set; } = 32;

    /// <summary>Length (in bytes) of the audit HMAC key (256-bit).</summary>
    public int AuditHmacKeyBytes { get; set; } = 32;

    /// <summary>BCrypt work factor for password hashing.</summary>
    public int BcryptWorkFactor { get; set; } = 12;

    /// <summary>Token plaintext length in bytes before base64 encoding.</summary>
    public int TokenBytes { get; set; } = 32;
}
