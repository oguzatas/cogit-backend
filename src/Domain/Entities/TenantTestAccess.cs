namespace backend.Domain.Entities;

/// <summary>
/// Junction entity that grants a Tenant the right to assign a global Test to its clients.
/// </summary>
public class TenantTestAccess : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int TestId { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual Test Test { get; set; } = default!;
}
