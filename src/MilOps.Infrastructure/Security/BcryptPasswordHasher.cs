using BCrypt.Net;
using Microsoft.Extensions.Options;
using MilOps.Domain.Security;

namespace MilOps.Infrastructure.Security;

/// <summary>
/// BCrypt password hasher. BCrypt is self-describing (the salt and cost are
/// embedded in the hash string), so verification needs no separate salt column.
/// Work factor is configurable via <see cref="SecurityOptions.BcryptWorkFactor"/>.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;
    public BcryptPasswordHasher(IOptions<SecurityOptions> options)
        => _workFactor = options.Value.BcryptWorkFactor;

    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, _workFactor);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; } // malformed hash => treat as no-match (fail closed)
    }
}
