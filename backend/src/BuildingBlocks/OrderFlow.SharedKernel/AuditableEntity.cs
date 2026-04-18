namespace OrderFlow.SharedKernel;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; protected init; }
    public DateTime? UpdatedAt { get; protected set; }

    protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;
}