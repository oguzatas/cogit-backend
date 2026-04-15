namespace backend.Domain.Entities;

/// <summary>
/// Represents a test session assigned to a TenantEmployee.
/// AccessKey is the magic-link token used by the TenantEmployee to access their test
/// without a password.
/// </summary>
public class Assignment : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int TestId { get; set; }

    /// <summary>FK to the TenantEmployee who will take the test.</summary>
    public int TenantEmployeeId { get; set; }

    /// <summary>
    /// FK to the AppUser (TenantStaff) who created this assignment.
    /// NULL for system-generated bulk assignments.
    /// </summary>
    public int? AssignedByStaffId { get; set; }

    public AssignmentStatus Status { get; set; }

    /// <summary>
    /// Cryptographically unique token embedded in the magic-link URL.
    /// The TenantEmployee uses this instead of a password to open their test.
    /// </summary>
    public string AccessKey { get; set; } = default!;

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Tenant Tenant { get; set; } = default!;
    public virtual Test Test { get; set; } = default!;
    public virtual TenantEmployee TenantEmployee { get; set; } = default!;
    public virtual AppUser? AssignedByStaff { get; set; }
    public virtual ICollection<AssignmentAnswer> AssignmentAnswers { get; set; } = new List<AssignmentAnswer>();
    public virtual ICollection<AssignmentResult> AssignmentResults { get; set; } = new List<AssignmentResult>();
}
