namespace backend.Application.Common.Interfaces;

/// <summary>
/// Provides stateless formula validation for ScoringScale expressions.
/// Separated as an interface to allow unit-testing validators in isolation.
/// </summary>
public interface IFormulaValidationService
{
    /// <summary>
    /// Extracts every [VariableKey] token from the formula.
    /// E.g. "[Q1] + [Q2] * 5"  →  ["Q1", "Q2"]
    /// </summary>
    IReadOnlyList<string> ExtractVariableKeys(string formula);

    /// <summary>
    /// Replaces every [VariableKey] with the dummy value 1 and attempts to
    /// evaluate the resulting expression using DataTable.Compute().
    /// Returns false when the expression is syntactically invalid.
    /// </summary>
    bool TryEvaluate(string formula, out string? errorMessage);
}
