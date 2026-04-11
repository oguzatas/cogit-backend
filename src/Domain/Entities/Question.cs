namespace backend.Domain.Entities;

/// <summary>
/// Global entity owned by System Admin. No TenantId by design.
/// </summary>
public class Question : BaseAuditableEntity
{
    public int TestId { get; set; }

    public string Text { get; set; } = default!;

    public QuestionType QuestionType { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>
    /// Symbolic key used by the Rules Engine and scoring formulas (e.g. "Q1", "PHQ_1").
    /// </summary>
    public string VariableKey { get; set; } = default!;

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Test Test { get; set; } = default!;
    public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();

    /// <summary>Rules that originate from this question.</summary>
    public virtual ICollection<BranchingRule> BranchingRulesAsSource { get; set; } = new List<BranchingRule>();

    /// <summary>Rules that point to this question as the next step.</summary>
    public virtual ICollection<BranchingRule> BranchingRulesAsTarget { get; set; } = new List<BranchingRule>();

    public virtual ICollection<ClientAnswer> ClientAnswers { get; set; } = new List<ClientAnswer>();
}
