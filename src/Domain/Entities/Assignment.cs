namespace backend.Domain.Entities;

/// <summary>
/// Represents a test session assigned by TenantStaff to a Client.
/// </summary>
public class Assignment : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int TestId { get; set; }

    /// <summary>FK to the User (Client role) who will take the test.</summary>
    public int ClientId { get; set; }

    /// <summary>FK to the User (TenantStaff role) who created this assignment.</summary>
    public int AssignedByStaffId { get; set; }

    public AssignmentStatus Status { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual Test Test { get; set; } = default!;
    public virtual User Client { get; set; } = default!;
    public virtual User AssignedByStaff { get; set; } = default!;
    public virtual ICollection<ClientAnswer> ClientAnswers { get; set; } = new List<ClientAnswer>();
    public virtual ICollection<AssignmentResult> AssignmentResults { get; set; } = new List<AssignmentResult>();
}
