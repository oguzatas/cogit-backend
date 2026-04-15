namespace backend.Application.Common.Interfaces;

/// <summary>
/// Executes the full NCalc scoring pipeline for a completed (or manually-graded) assignment.
///
/// Pipeline:
///   1. Load all TestVariables for the test → seed memoryPool with DefaultValues.
///   2. Walk AssignmentAnswers:
///        a. NumberValue answers → add to memoryPool via Question.VariableKey.
///        b. SelectedOptionIds   → inject OPT_{optionId} = true boolean flags.
///        c. QuestionOptionPoints resolved from SelectedOptionIds → accumulate into variable keys.
///   3. Add ManualGrade.Points for the assignment into matching variable keys.
///   4. For each ScoringScale (ordered by CalculationOrder):
///        a. Bind memoryPool to NCalc expression parameters.
///        b. Evaluate formula → store numeric result in CalculatedScore
///           OR string result in ResultText.
///        c. Cascade: inject the result back into memoryPool under scale.Key
///           so subsequent formulas can reference it.
///   5. Upsert AssignmentResult rows, set assignment.Status = Completed,
///      then call SaveChangesAsync — the engine owns the final persistence step.
/// </summary>
public interface IScoringEngineService
{
    /// <summary>
    /// Calculates and persists AssignmentResult rows for the given assignment,
    /// then transitions the assignment to <see cref="backend.Domain.Enums.AssignmentStatus.Completed"/>.
    /// Calls SaveChangesAsync internally — callers must NOT call it again for the
    /// scoring transaction.
    /// </summary>
    Task CalculateResultsAsync(int assignmentId, CancellationToken cancellationToken = default);
}
