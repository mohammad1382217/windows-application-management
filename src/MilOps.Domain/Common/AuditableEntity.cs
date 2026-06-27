namespace MilOps.Domain.Common;

/// <summary>
/// Auditable entity base. Tracks who created/modified a row and when.
/// The user identity is recorded by name to avoid coupling persistence
/// to the live session and to survive user deletion.
/// </summary>
public abstract class AuditableEntity : Entity, IAggregateRoot
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // RowVersion is a concurrency token mapped by EF Core.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public void Touch(string? byUser)
    {
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = byUser;
    }
}
