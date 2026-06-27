namespace MilOps.Domain.Security;

/// <summary>
/// Port for password hashing/verification. Implemented in Infrastructure with
/// BCrypt (cost-tunable). The domain only depends on this abstraction so tests
/// can swap a fast/zero-cost hasher.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hash a plaintext password. Returns a self-describing hash string.</summary>
    string Hash(string password);

    /// <summary>Verify a plaintext password against a stored hash.</summary>
    bool Verify(string password, string hash);
}
