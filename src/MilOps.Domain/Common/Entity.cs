namespace MilOps.Domain.Common;

/// <summary>
/// Base class for all entities with a strongly-typed identifier.
/// Identity is the only equality that matters for entities.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity() { }
    protected Entity(TId id) => Id = id;

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);
    public override int GetHashCode() => Id is null ? 0 : EqualityComparer<TId>.Default.GetHashCode(Id);

    public static bool operator ==(Entity<TId>? a, Entity<TId>? b) =>
        a is null ? b is null : a.Equals(b);
    public static bool operator !=(Entity<TId>? a, Entity<TId>? b) => !(a == b);
}

/// <summary>Default int-keyed entity base.</summary>
public abstract class Entity : Entity<int>
{
    protected Entity() { }
    protected Entity(int id) : base(id) { }
}
