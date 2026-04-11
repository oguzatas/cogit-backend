namespace backend.Domain.Entities;

/// <summary>
/// Global entity owned by System Admin. No TenantId by design.
/// Drives conditional navigation between questions at runtime.
/// </summary>
public class BranchingRule : BaseAuditableEntity
{
    public int SourceQuestionId { get; set; }

    /// <summary>
    /// Expression evaluated against the client's answer (e.g. "SelectedValue == 1").
    /// </summary>
    public string ConditionExpression { get; set; } = default!;

    public int TargetQuestionId { get; set; }

    /// <summary>
    /// When true, this rule fires if no other condition matches (fallback/default branch).
    /// </summary>
    public bool IsDefault { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Question SourceQuestion { get; set; } = default!;
    public virtual Question TargetQuestion { get; set; } = default!;
}
