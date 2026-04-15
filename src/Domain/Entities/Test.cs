namespace backend.Domain.Entities;

/// <summary>
/// Global entity owned by System Admin. No TenantId by design.
/// </summary>
public class Test : BaseAuditableEntity
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsPublished { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    public virtual ICollection<ScoringScale> ScoringScales { get; set; } = new List<ScoringScale>();
    public virtual ICollection<TestVariable> TestVariables { get; set; } = new List<TestVariable>();
    public virtual ICollection<TenantTestAccess> TenantTestAccesses { get; set; } = new List<TenantTestAccess>();
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
