using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.ValueObjects;

/// <summary>
/// Personnel/service code: 4-12 alphanumerics. Normalized to uppercase.
/// </summary>
public sealed class PersonnelCode : ValueObject
{
    private static readonly Regex s_pattern = new("^[A-Z0-9]{4,12}$", RegexOptions.Compiled);

    public string Value { get; }

    private PersonnelCode(string value) => Value = value;

    public static PersonnelCode Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("PERSONNEL_CODE_EMPTY", "Personnel code is required.");

        var code = input.Trim().ToUpperInvariant();
        if (!s_pattern.IsMatch(code))
            throw new DomainException("PERSONNEL_CODE_FORMAT",
                "Personnel code must be 4-12 alphanumeric characters.");

        return new PersonnelCode(code);
    }

    public override string ToString() => Value;
    public static implicit operator string(PersonnelCode? p) => p?.Value ?? string.Empty;

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
