using MilOps.Domain.Enums;

namespace MilOps.Domain.Security;

/// <summary>
/// Generates one-time authorization tokens and their SHA-256 storage hash.
/// Implemented in Infrastructure using a CSPRNG + SHA-256. Returns the plaintext
/// exactly once so the Application layer can show it to the commander; only the
/// hash is persisted.
/// </summary>
public interface ITokenGenerator
{
    /// <summary>Generate a fresh token and its SHA-256 hash.</summary>
    GeneratedToken Generate(TokenPurpose purpose);
}

/// <param name="Plaintext">Shown once to the commander; never persisted.</param>
/// <param name="Hash">SHA-256 hex; safe to persist.</param>
/// <param name="Preview">First chars for harmless UI display.</param>
public sealed record GeneratedToken(string Plaintext, string Hash, string Preview);
