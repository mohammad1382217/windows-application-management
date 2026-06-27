using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.ValueObjects;

/// <summary>
/// A person's name component (first/last/father). Validates length and
/// strips surrounding whitespace. Allows Persian/Arabic/Latin letters.
/// </summary>
public sealed class PersonName : ValueObject
{
    private static readonly Regex s_pattern =
        new(@"^[\p{L}\p{M} \-.'’]{2,60}$", RegexOptions.Compiled);

    public string Value { get; }

    private PersonName(string value) => Value = value;

    public static PersonName Create(string input, string fieldName = "Name")
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("NAME_EMPTY", $"{fieldName} is required.");

        var value = input.Trim();
        if (value.Length > 60)
            throw new DomainException("NAME_TOO_LONG", $"{fieldName} must be at most 60 characters.");

        if (!s_pattern.IsMatch(value))
            throw new DomainException("NAME_FORMAT",
                $"{fieldName} may contain letters, spaces, hyphens, apostrophes, and dots only.");

        return new PersonName(value);
    }

    public override string ToString() => Value;
    public static implicit operator string(PersonName? n) => n?.Value ?? string.Empty;

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
