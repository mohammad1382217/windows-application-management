using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.ValueObjects;

/// <summary>
/// Iranian national code (کد ملی): exactly 10 digits with a valid check digit.
/// Stored as a normalized string. Equality by value.
/// </summary>
public sealed class NationalCode : ValueObject
{
    private static readonly Regex s_pattern = new("^[0-9]{10}$", RegexOptions.Compiled);

    public string Value { get; }

    private NationalCode(string value) => Value = value;

    public static NationalCode Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("NATIONAL_CODE_EMPTY", "National code is required.");

        var code = input.Trim();
        if (!s_pattern.IsMatch(code))
            throw new DomainException("NATIONAL_CODE_FORMAT", "National code must be exactly 10 digits.");

        if (!IsValidCheckDigit(code))
            throw new DomainException("NATIONAL_CODE_CHECKSUM", "National code check digit is invalid.");

        return new NationalCode(code);
    }

    /// <summary>Standard Iranian national-code check-digit algorithm.</summary>
    private static bool IsValidCheckDigit(string code)
    {
        var allSame = code.Distinct().Count() == 1;
        if (allSame) return false; // e.g. 0000000000 is invalid

        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (code[i] - '0') * (10 - i);
        var remainder = sum % 11;
        var check = remainder < 2 ? remainder : 11 - remainder;
        return check == (code[9] - '0');
    }

    public override string ToString() => Value;
    public static implicit operator string(NationalCode? n) => n?.Value ?? string.Empty;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
