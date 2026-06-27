namespace MilOps.Application.Common;

/// <summary>
/// Abstraction over the clock so domain logic and tests are deterministic.
/// All persisted timestamps are UTC; the Presentation layer converts for display.
/// </summary>
public interface IDateTime
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}

public class SystemDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
