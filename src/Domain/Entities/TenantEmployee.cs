namespace backend.Domain.Entities;

/// <summary>
/// Business entity — the test-taker in the B2B2C model.
/// Has NO password, NO role, NO Identity account.
/// Accesses their assigned tests exclusively through AccessKey magic-links.
/// </summary>
public class TenantEmployee : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int DepartmentId { get; set; }

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual Department Department { get; set; } = default!;

    /// <summary>Tests assigned to this employee.</summary>
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
