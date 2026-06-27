namespace MilOps.Domain.Exceptions;

/// <summary>
/// Base type for all domain rule violations. The Application layer surfaces these
/// to the user without exposing implementation details.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }
    public DomainException(string code, string message) : base(message) => Code = code;
    public DomainException(string code, string message, Exception inner) : base(message, inner) => Code = code;
}
