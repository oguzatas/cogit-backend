namespace backend.Domain.Entities;

/// <summary>
/// Global entity owned by System Admin. No TenantId by design.
/// </summary>
public class QuestionOption : BaseAuditableEntity
{
    public int QuestionId { get; set; }

    public string Text { get; set; } = default!;

    /// <summary>
    /// Numeric weight used in scoring formula calculations.
    /// </summary>
    public decimal? NumericValue { get; set; }

    public int OrderIndex { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Question Question { get; set; } = default!;
    public virtual ICollection<QuestionOptionPoint> QuestionOptionPoints { get; set; } = new List<QuestionOptionPoint>();
}
