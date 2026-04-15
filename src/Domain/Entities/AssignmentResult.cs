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

    /// <summary>
    /// Populated when the formula returns a numeric value (double/int/decimal).
    /// Null when the formula returns a string result (see ResultText).
    /// </summary>
    public decimal? CalculatedScore { get; set; }

    /// <summary>
    /// Populated when the formula returns a string value (e.g. "ENTJ", "High Risk").
    /// Null when the formula returns a numeric result (see CalculatedScore).
    /// </summary>
    public string? ResultText { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Assignment Assignment { get; set; } = default!;
    public virtual ScoringScale Scale { get; set; } = default!;
}
