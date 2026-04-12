namespace backend.Domain.Entities;

/// <summary>
/// Identity entity — represents a human who can log in with a password.
/// Only SuperAdmin and TenantStaff roles are permitted here.
/// TenantEmployees are NOT AppUsers; they access the system via AccessKey magic-links.
/// </summary>
public class AppUser : BaseAuditableEntity
{
    /// <summary>
    /// NULL  → SuperAdmin (platform-wide, not scoped to any tenant).
    /// Value → TenantStaff (scoped to one tenant).
    /// </summary>
    public int? TenantId { get; set; }

    public string Email { get; set; } = default!;

    /// <summary>Hashed credential. Never stored in plain text.</summary>
    public string PasswordHash { get; set; } = default!;

    /// <summary>SuperAdmin or TenantStaff — never TenantEmployee.</summary>
    public UserRole Role { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Assignments this staff member created for TenantEmployees.</summary>
    public virtual ICollection<Assignment> AssignmentsAsStaff { get; set; } = new List<Assignment>();
}
