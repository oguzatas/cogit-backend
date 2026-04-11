namespace backend.Domain.Entities;

/// <summary>
/// Global entity owned by System Admin. No TenantId by design.
/// Defines a named scoring dimension for a Test and its calculation formula.
/// </summary>
public class ScoringScale : BaseAuditableEntity
{
    public int TestId { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>
    /// Mathematical formula referencing VariableKeys (e.g. "Q1 + Q2 + Q3").
    /// Evaluated by the scoring engine after assignment completion.
    /// </summary>
    public string FormulaExpression { get; set; } = default!;

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Test Test { get; set; } = default!;
    public virtual ICollection<AssignmentResult> AssignmentResults { get; set; } = new List<AssignmentResult>();
}
