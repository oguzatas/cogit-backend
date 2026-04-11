namespace backend.Domain.Entities;

public class User : BaseAuditableEntity
{
    /// <summary>
    /// NULL = System Admin (no tenant). Any value = scoped to that Tenant.
    /// </summary>
    public int? TenantId { get; set; }

    public string Email { get; set; } = default!;

    public UserRole Role { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Assignments where this user is the test-taker.</summary>
    public virtual ICollection<Assignment> AssignmentsAsClient { get; set; } = new List<Assignment>();

    /// <summary>Assignments this user created on behalf of a client.</summary>
    public virtual ICollection<Assignment> AssignmentsAsStaff { get; set; } = new List<Assignment>();
}
