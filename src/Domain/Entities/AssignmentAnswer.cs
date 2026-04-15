namespace backend.Domain.Entities;

/// <summary>
/// Stores a respondent's answer to a single question within an assignment.
///
/// SelectedOptionIds is serialized as a JSONB column so a single row cleanly
/// represents both SingleChoice (one element) and MultipleChoice (many elements)
/// without a separate join table.
/// NumberValue covers numeric/slider questions; TextValue covers open-text questions.
/// </summary>
public class AssignmentAnswer : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int AssignmentId { get; set; }

    public int QuestionId { get; set; }

    /// <summary>
    /// Populated for SingleChoice / MultipleChoice questions.
    /// Stored as a JSONB array — no FK to QuestionOption (integrity enforced at app layer).
    /// </summary>
    public List<int> SelectedOptionIds { get; set; } = new();

    /// <summary>Populated for numeric / slider questions.</summary>
    public double? NumberValue { get; set; }

    /// <summary>Populated for open-text questions.</summary>
    public string? TextValue { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Assignment Assignment { get; set; } = default!;
    public virtual Question Question { get; set; } = default!;
}
