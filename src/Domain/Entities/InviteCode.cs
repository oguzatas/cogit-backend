namespace backend.Domain.Entities;

/// <summary>
/// A single-use, time-limited token that allows a person to self-register as a
/// TenantEmployee under a specific Tenant and Department (Flow B).
/// </summary>
public class InviteCode : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int DepartmentId { get; set; }

    /// <summary>Cryptographically random token shared with the invitee (URL-safe).</summary>
    public string Code { get; set; } = default!;

    public bool IsUsed { get; set; }

    /// <summary>NULL means the code never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual Department Department { get; set; } = default!;
}
