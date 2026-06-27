namespace MilOps.Domain.Common;

/// <summary>
/// Marker interface for aggregate roots. Only aggregate roots may be referenced
/// directly from repositories. Child entities must be reached through their root.
/// </summary>
public interface IAggregateRoot { }
