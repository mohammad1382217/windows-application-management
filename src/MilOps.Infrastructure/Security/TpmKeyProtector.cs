using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MilOps.Infrastructure.Security;

/// <summary>
/// TPM integration layer.
///
/// Design choice: layered, optional, no hard Tbsi dependency.
/// This class performs lightweight, dependency-free detection of TPM
/// availability via the Windows TBS (TPM Base Services) P/Invoke. If a TPM is
/// present, secrets can be wrapped with a TPM-bound symmetric key. We do NOT
/// take a hard dependency on a TPM library so the app still runs on machines
/// without a TPM or without TPM provisioning: DPAPI is always the fallback.
///
/// What "TPM-backed" means here:
///   - A symmetric AES-GCM key is generated once and itself protected: sealed
///     with TPM-owned storage when the optional native helper is present,
///     otherwise DPAPI-protecting the wrapping key as fallback.
///   - Secrets are encrypted with that key; the wrapped blob is persisted.
///     Decryption requires the same machine + user context.
///
/// NOTE: Native TPM sealing is intentionally stubbed behind
/// <see cref="TrySealWithNativeTpm"/> so the project builds anywhere. Provide a
/// real implementation via your platform's TPM library (e.g. Microsoft.TSS.Api
/// or a custom Tbsi wrapper) to enable full hardware sealing. Detection of TPM
/// presence is real (TBS P/Invoke) and used to gate the strategy.
/// </summary>
public sealed class TpmKeyProtector
{
    private readonly ILogger<TpmKeyProtector> _logger;
    private readonly SecurityOptions _options;
    private readonly Lazy<bool> _available;
    private readonly string _wrappingKeyPath;

    private static readonly byte[] s_aad = Encoding.UTF8.GetBytes("MilOps-TpmWrap-v1");

    public TpmKeyProtector(IOptions<SecurityOptions> options, ILogger<TpmKeyProtector> logger)
    {
        _options = options.Value;
        _logger = logger;
        _available = new Lazy<bool>(DetectTpm);
        var dir = !string.IsNullOrWhiteSpace(_options.SecretsDirectory)
            ? _options.SecretsDirectory!
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MilOps", "secrets");
        Directory.CreateDirectory(dir);
        _wrappingKeyPath = Path.Combine(dir, "tpm-wrapping-key.bin");
    }

    /// <summary>True if a TPM is detected on this machine (via TBS).</summary>
    public bool IsAvailable => _available.Value;

    /// <summary>
    /// Wrap a secret. Returns a self-describing blob (nonce || ciphertext || tag)
    /// encrypted with the wrapping key, which is itself sealed (TPM or DPAPI).
    /// </summary>
    public bool TryWrap(byte[] secret, out byte[] blob)
    {
        blob = Array.Empty<byte>();
        if (!IsAvailable) return false;

        try
        {
            var key = GetOrCreateWrappingKey();
            try
            {
                var (nonce, ciphertext, tag) = AesGcmEncrypt(key, secret);
                blob = Combine(nonce, ciphertext, tag);
                return true;
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TPM wrap failed; falling back to DPAPI.");
            return false;
        }
    }

    public byte[]? Unwrap(byte[] blob)
    {
        if (!IsAvailable) return null;
        try
        {
            var key = GetOrCreateWrappingKey();
            try
            {
                var (nonce, ciphertext, tag) = Split(blob);
                return AesGcmDecrypt(key, nonce, ciphertext, tag);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TPM unwrap failed.");
            return null;
        }
    }

    private byte[] GetOrCreateWrappingKey()
    {
        if (File.Exists(_wrappingKeyPath))
        {
            var sealedBlob = File.ReadAllBytes(_wrappingKeyPath);
            try
            {
                if (TrySealWithNativeTpm(sealedBlob, out var unsealedKey)) return unsealedKey!;
            }
            catch { /* fall through to DPAPI */ }
            return ProtectedData.Unprotect(sealedBlob, Encoding.UTF8.GetBytes(_options.DpapiEntropy),
                DataProtectionScope.CurrentUser);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        byte[] wrapped;
        if (TrySealWithNativeTpm(key, out wrapped!))
            _logger.LogInformation("Wrapping key sealed with native TPM.");
        else
        {
            wrapped = ProtectedData.Protect(key, Encoding.UTF8.GetBytes(_options.DpapiEntropy),
                DataProtectionScope.CurrentUser);
            _logger.LogInformation("Wrapping key protected with DPAPI (native TPM sealing unavailable).");
        }
        File.WriteAllBytes(_wrappingKeyPath, wrapped);
        Array.Clear(wrapped, 0, wrapped.Length);
        return key;
    }

    /// <summary>
    /// OPTIONAL native TPM sealing seam. Default returns false so DPAPI is used.
    /// Replace the body with a call to your TPM library (e.g. Microsoft.TSS.Api)
    /// to enable true hardware-bound sealing.
    /// </summary>
    private bool TrySealWithNativeTpm(byte[] data, out byte[]? sealedData)
    {
        sealedData = null;
        return false;
    }

    private static (byte[] nonce, byte[] ciphertext, byte[] tag) AesGcmEncrypt(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, s_aad);
        return (nonce, ciphertext, tag);
    }

    private static byte[] AesGcmDecrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, s_aad);
        return plaintext;
    }

    private static byte[] Combine(byte[] a, byte[] b, byte[] c)
    {
        var r = new byte[a.Length + b.Length + c.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        Buffer.BlockCopy(c, 0, r, a.Length + b.Length, c.Length);
        return r;
    }

    private static (byte[] nonce, byte[] ciphertext, byte[] tag) Split(byte[] blob)
    {
        var nonce = blob[..12];
        var tag = blob[^16..];
        var ciphertext = blob[12..^16];
        return (nonce, ciphertext, tag);
    }

    /// <summary>Dependency-free TPM presence detection via the TPM Base Services API.</summary>
    private bool DetectTpm()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        try
        {
            // The Windows TBS API expects a POINTER to a TBS_CONTEXT_PARAMS2 struct,
            // not the version enum value itself. Marshaling the struct by-reference
            // and only ever closing the actual returned handle avoids the access
            // violation that the previous literal-int P/Invoke caused (0xC0000005).
            var contextParams = new TBS_CONTEXT_PARAMS2 { Version = 2 };
            var rc = Tbsi_Context_Create(ref contextParams, out var context);
            if (rc != TBS_SUCCESS)
            {
                _logger.LogDebug("TBS context could not be created (rc={Rc}); assuming no TPM.", rc);
                return false;
            }
            try { if (context != IntPtr.Zero) TbsipContext_Close(context); }
            catch (Exception closeEx) { _logger.LogDebug(closeEx, "TbsipContext_Close failed non-fatally."); }
            _logger.LogInformation("TPM detected via TBS.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TPM detection via TBS failed; assuming no TPM.");
            return false;
        }
    }

    private const uint TBS_SUCCESS = 0;

    /// <summary>
    /// TBS_CONTEXT_PARAMS2: first DWORD is the version (2), which is the only
    /// field the API reads for context creation. Passed by reference as a pointer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TBS_CONTEXT_PARAMS2
    {
        public uint Version;
    }

    [DllImport("tbs.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint Tbsi_Context_Create(ref TBS_CONTEXT_PARAMS2 contextParams, out IntPtr context);

    [DllImport("tbs.dll")]
    private static extern uint TbsipContext_Close(IntPtr context);
}
