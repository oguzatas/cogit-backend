namespace backend.Domain.Entities;

/// <summary>
/// Centralised variable registry for a Test.
/// Each variable is a named scoring slot that NCalc formulas reference by Key.
///
/// Example: Name = "Leadership Score", Key = "LDR", DefaultValue = 0.
/// ScoringScale.FormulaExpression can then reference "LDR" and the engine
/// resolves it to the accumulated points for that variable.
/// </summary>
public class TestVariable : BaseAuditableEntity
{
    public int TestId { get; set; }

    /// <summary>Human-readable label shown in the test builder UI.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Short alias used in NCalc formulas (e.g. "LDR", "PHQ_TOTAL").</summary>
    public string Key { get; set; } = default!;

    /// <summary>Starting value before any option points are applied.</summary>
    public double DefaultValue { get; set; } = 0;

    public bool IsDeleted { get; set; }

    // Navigation
    public virtual Test Test { get; set; } = default!;
    public virtual ICollection<QuestionOptionPoint> QuestionOptionPoints { get; set; } = new List<QuestionOptionPoint>();
}
