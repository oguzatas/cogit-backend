namespace backend.Domain.Entities;

/// <summary>
/// Pre-aggregation mapping: one QuestionOption row can award points to
/// multiple TestVariables simultaneously.
///
/// Example: selecting option "Strongly Agree" awards +3 to LDR and +1 to EMP.
/// The scoring engine sums all QuestionOptionPoint rows for each chosen
/// SelectedOptionId in AssignmentAnswer to build the variable totals before
/// evaluating the NCalc formula.
/// </summary>
public class QuestionOptionPoint : BaseAuditableEntity
{
    public int OptionId { get; set; }

    public int TestVariableId { get; set; }

    public double Points { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual QuestionOption Option { get; set; } = default!;
    public virtual TestVariable TestVariable { get; set; } = default!;
}
