using backend.Application.Common.Interfaces;

namespace backend.Application.ScoringScales.Commands.CreateScoringScale;

public class CreateScoringScaleCommandValidator : AbstractValidator<CreateScoringScaleCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFormulaValidationService _formulaValidation;

    public CreateScoringScaleCommandValidator(
        IApplicationDbContext context,
        IFormulaValidationService formulaValidation)
    {
        _context          = context;
        _formulaValidation = formulaValidation;

        // ── TestId ────────────────────────────────────────────────────────────
        RuleFor(v => v.TestId)
            .GreaterThan(0)
            .WithMessage("A valid TestId is required.");

        // ── Name ──────────────────────────────────────────────────────────────
        RuleFor(v => v.Name)
            .NotEmpty()
            .MaximumLength(300);

        // ── FormulaExpression ─────────────────────────────────────────────────
        RuleFor(v => v.FormulaExpression)
            .NotEmpty()
            .MaximumLength(2000);

        // Step 1 (async): every [VariableKey] in the formula must exist as a
        // Question.VariableKey for the supplied TestId.
        RuleFor(v => v.FormulaExpression)
            .MustAsync(AllVariableKeysExistForTest)
            .When(v => !string.IsNullOrWhiteSpace(v.FormulaExpression))
            .WithMessage(v =>
                $"Formula references variable key(s) that do not exist for TestId {v.TestId}. " +
                 "Ensure every [Key] in the formula maps to a Question.VariableKey on that Test.")
            .WithErrorCode("Formula.UnknownVariableKeys");

        // Step 2 (sync): after replacing variables with 1, the remaining
        // expression must evaluate without throwing (no syntax errors).
        RuleFor(v => v.FormulaExpression)
            .Must(BeValidMathExpression)
            .When(v => !string.IsNullOrWhiteSpace(v.FormulaExpression))
            .WithMessage("Invalid mathematical formula format.")
            .WithErrorCode("Formula.InvalidMathExpression");
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all [VariableKey] tokens from the formula and verifies that
    /// every one of them exists as a Question.VariableKey for request.TestId.
    /// </summary>
    private async Task<bool> AllVariableKeysExistForTest(
        CreateScoringScaleCommand command,
        string formula,
        CancellationToken cancellationToken)
    {
        var referencedKeys = _formulaValidation.ExtractVariableKeys(formula);

        // A formula with no variable references (e.g. a constant "42") is allowed.
        if (referencedKeys.Count == 0)
            return true;

        var existingKeys = await _context.Questions
            .AsNoTracking()
            .Where(q => q.TestId == command.TestId)
            .Select(q => q.VariableKey)
            .ToListAsync(cancellationToken);

        // All referenced keys must be present in the test's question set.
        return referencedKeys.All(k =>
            existingKeys.Contains(k, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Replaces every [VariableKey] token with the dummy value 1 and attempts
    /// to evaluate the expression with DataTable.Compute(). Returns false if
    /// the expression is syntactically invalid (unbalanced parens, bad ops, etc.).
    /// </summary>
    private bool BeValidMathExpression(string formula)
        => _formulaValidation.TryEvaluate(formula, out _);
}
