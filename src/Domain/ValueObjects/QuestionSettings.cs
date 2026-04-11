namespace backend.Domain.ValueObjects;

/// <summary>
/// Flexible per-question configuration stored as JSONB in PostgreSQL.
/// Holds display and validation hints that vary by QuestionType.
/// </summary>
public record QuestionSettings
{
    /// <summary>Whether the question must be answered before proceeding.</summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>Hint text shown inside a TextInput field.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Minimum numeric value for Rating / LikertScale questions.</summary>
    public int? MinValue { get; init; }

    /// <summary>Maximum numeric value for Rating / LikertScale questions.</summary>
    public int? MaxValue { get; init; }

    /// <summary>Shuffle option order on each render (SingleChoice / MultipleChoice).</summary>
    public bool RandomizeOptions { get; init; } = false;
}
