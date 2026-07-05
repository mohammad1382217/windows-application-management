using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MilOps.Domain.Enums;
using MilOps.Domain.Security;

namespace MilOps.Infrastructure.Security;

/// <summary>
/// Generates cryptographically secure one-time tokens.
///
/// Token shape: Base64Url(CSPRNG bytes), human-friendly grouped with hyphens.
/// Storage: we persist SHA-256(token + pepper) so the plaintext is never stored.
/// The pepper (<see cref="SecretPurposes.TokenPepper"/>) is itself DPAPI/TPM
/// protected, adding a second defense layer: an attacker with only the DB cannot
/// verify guesses without also unwrapping the pepper.
/// </summary>
public sealed class CryptoTokenGenerator : ITokenGenerator
{
    private readonly SecretProtector _secrets;
    private readonly SecurityOptions _options;

    public CryptoTokenGenerator(SecretProtector secrets, IOptions<SecurityOptions> options)
    { _secrets = secrets; _options = options.Value; }

    public GeneratedToken Generate(TokenPurpose purpose)
    {
        var bytes = RandomNumberGenerator.GetBytes(_options.TokenBytes);
        var plaintext = ToGroupedBase64Url(bytes);   // shown once to the commander

        // Hash with a DPAPI/TPM-protected pepper for storage.
        var pepper = _secrets.UnprotectOrCreate(SecretPurposes.TokenPepper, 32);
        try
        {
            var hash = Sha256Hex(Encoding.UTF8.GetBytes(plaintext), pepper);
            var preview = plaintext.Length > 12 ? plaintext[..12] : plaintext;
            return new GeneratedToken(plaintext, hash, preview);
        }
        finally { CryptographicOperations.ZeroMemory(pepper); }
    }

    /// <summary>Peppered storage hash for a supplied plaintext (for lookups).</summary>
    public string Hash(string plaintext)
    {
        var pepper = _secrets.UnprotectOrCreate(SecretPurposes.TokenPepper, 32);
        try { return Sha256Hex(Encoding.UTF8.GetBytes(plaintext), pepper); }
        finally { CryptographicOperations.ZeroMemory(pepper); }
    }

    /// <summary>Verify a supplied plaintext against a stored hash (constant-ish).</summary>
    public bool Verify(string suppliedPlaintext, string storedHash)
    {
        var pepper = _secrets.UnprotectOrCreate(SecretPurposes.TokenPepper, 32);
        try
        {
            var candidate = Sha256Hex(Encoding.UTF8.GetBytes(suppliedPlaintext), pepper);
            // Fixed-time comparison to avoid timing leaks.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(storedHash));
        }
        finally { CryptographicOperations.ZeroMemory(pepper); }
    }

    private static string ToGroupedBase64Url(byte[] bytes)
    {
        var b64 = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        // Group into 5-char chunks for easier reading/typing: XXXXX-XXXXX-...
        var sb = new StringBuilder(b64.Length + 8);
        for (var i = 0; i < b64.Length; i += 5)
        {
            if (i > 0) sb.Append('-');
            sb.Append(i + 5 <= b64.Length ? b64.Substring(i, 5) : b64[i..]);
        }
        return sb.ToString();
    }

    private static string Sha256Hex(byte[] token, byte[] pepper)
    {
        var combined = new byte[token.Length + pepper.Length];
        Buffer.BlockCopy(token, 0, combined, 0, token.Length);
        Buffer.BlockCopy(pepper, 0, combined, token.Length, pepper.Length);
        var hash = SHA256.HashData(combined);
        Array.Clear(combined, 0, combined.Length);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
