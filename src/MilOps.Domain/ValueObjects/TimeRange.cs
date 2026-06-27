using MilOps.Domain.Common;
using MilOps.Domain.Exceptions;

namespace MilOps.Domain.ValueObjects;

/// <summary>
/// A closed [Start, End] time-of-day range (used for shift hours).
/// Overnight ranges (Start > End) are allowed to model night shifts.
/// </summary>
public sealed class TimeRange : ValueObject
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    private TimeRange(TimeOnly start, TimeOnly end) { Start = start; End = end; }

    public static TimeRange Create(TimeOnly start, TimeOnly end)
    {
        if (start == end)
            throw new DomainException("TIMERANGE_EMPTY", "Shift start and end cannot be identical.");
        return new TimeRange(start, end);
    }

    public TimeSpan Duration => End > Start ? End - Start : (End - Start).Add(TimeSpan.FromDays(1));

    /// <summary>True if this time falls within the range (overnight-aware).</summary>
    public bool Contains(TimeOnly t) => End > Start
        ? t >= Start && t <= End
        : t >= Start || t <= End;

    public string ToDisplay() => $"{Start:HH:mm}-{End:HH:mm}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
