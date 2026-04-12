namespace backend.Domain.Entities;

/// <summary>
/// Multi-tenancy root entity (a Clinic or corporate Client in the B2B2C model).
/// Does not carry a self-referential TenantId.
/// </summary>
public class Tenant : BaseAuditableEntity
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();
    public virtual ICollection<TenantEmployee> TenantEmployees { get; set; } = new List<TenantEmployee>();
    public virtual ICollection<TenantTestAccess> TenantTestAccesses { get; set; } = new List<TenantTestAccess>();
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public virtual ICollection<InviteCode> InviteCodes { get; set; } = new List<InviteCode>();
}
