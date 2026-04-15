namespace backend.Domain.Entities;

/// <summary>
/// Records a human reviewer's point award for a specific TestVariable on a specific Assignment.
///
/// Used when the test contains TextInput questions that cannot be auto-scored.
/// The scoring engine adds these values to the variable accumulation pool before
/// evaluating ScoringScale NCalc formulas.
///
/// Unique constraint: (AssignmentId, TestVariableId) — one grade per variable per assignment.
/// Calling the manual-grade endpoint a second time for the same variable replaces the previous value.
/// </summary>
public class ManualGrade : BaseAuditableEntity
{
    public int    AssignmentId   { get; set; }
    public int    TenantId       { get; set; }
    public int    TestVariableId { get; set; }

    /// <summary>Points to add to this variable's accumulation pool.</summary>
    public double Points { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Assignment   Assignment   { get; set; } = default!;
    public virtual TestVariable TestVariable { get; set; } = default!;
}
