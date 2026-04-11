namespace backend.Domain.Entities;

/// <summary>
/// Multi-tenancy root entity. Does not carry a self-referential TenantId.
/// </summary>
public class Tenant : BaseAuditableEntity
{
    public string Name { get; set; } = default!;

    public SubscriptionPlan SubscriptionPlan { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<TenantTestAccess> TenantTestAccesses { get; set; } = new List<TenantTestAccess>();
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
