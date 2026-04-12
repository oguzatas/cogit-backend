namespace backend.Domain.Entities;

/// <summary>
/// Organisational unit within a Tenant.
/// TenantEmployees are grouped into Departments; assignments are generated per Department.
/// </summary>
public class Department : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public string Name { get; set; } = default!;

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual ICollection<TenantEmployee> TenantEmployees { get; set; } = new List<TenantEmployee>();
    public virtual ICollection<InviteCode> InviteCodes { get; set; } = new List<InviteCode>();
}
