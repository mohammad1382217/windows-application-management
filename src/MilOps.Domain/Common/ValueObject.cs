namespace MilOps.Domain.Common;

/// <summary>
/// Base class for value objects. Value objects compare by all of their components,
/// have no identity, and should be immutable. Equality operators are implemented.
/// </summary>
public abstract class ValueObject
{
    /// <summary>Return the atomic values that define equality for this VO.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents()) hash.Add(component);
        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? a, ValueObject? b) =>
        a is null ? b is null : a.Equals(b);
    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);
}
