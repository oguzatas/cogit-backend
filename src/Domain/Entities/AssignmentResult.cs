namespace backend.Domain.Entities;

/// <summary>
/// Stores the calculated score for one ScoringScale dimension of a completed assignment.
/// One assignment produces one AssignmentResult per ScoringScale defined on the Test.
/// </summary>
public class AssignmentResult : BaseAuditableEntity
{
    public int TenantId { get; set; }

    public int AssignmentId { get; set; }

    /// <summary>FK to the ScoringScale whose formula produced this result.</summary>
    public int ScaleId { get; set; }

    public decimal CalculatedScore { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Assignment Assignment { get; set; } = default!;
    public virtual ScoringScale Scale { get; set; } = default!;
}
