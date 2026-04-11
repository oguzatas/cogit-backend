namespace backend.Domain.Entities;

/// <summary>
/// Stores a client's response to a single question within an assignment.
/// </summary>
public class ClientAnswer : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int AssignmentId { get; set; }

    public int QuestionId { get; set; }

    /// <summary>Populated for SingleChoice / MultipleChoice questions.</summary>
    public int? SelectedOptionId { get; set; }

    /// <summary>Populated for TextInput questions.</summary>
    public string? TextResponse { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Assignment Assignment { get; set; } = default!;
    public virtual Question Question { get; set; } = default!;
    public virtual QuestionOption? SelectedOption { get; set; }
}
