using System.Data;
using System.Text.RegularExpressions;
using backend.Application.Common.Interfaces;

namespace backend.Application.Common.Services;

/// <inheritdoc cref="IFormulaValidationService"/>
public sealed class FormulaValidationService : IFormulaValidationService
{
    // Matches tokens like [Q1], [PHQ_2], [SCALE_A] — letters, digits, underscores.
    private static readonly Regex VariablePattern =
        new(@"\[([A-Za-z0-9_]+)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<string> ExtractVariableKeys(string formula)
    {
        Guard.Against.NullOrWhiteSpace(formula);

        return VariablePattern.Matches(formula)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryEvaluate(string formula, out string? errorMessage)
    {
        Guard.Against.NullOrWhiteSpace(formula);

        // Replace every [VariableKey] with the dummy literal 1 so we get a
        // pure numeric expression that DataTable.Compute() can evaluate.
        var testExpression = VariablePattern.Replace(formula, "1");

        try
        {
            new DataTable().Compute(testExpression, null);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
