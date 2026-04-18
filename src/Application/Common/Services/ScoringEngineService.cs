using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NCalc;

namespace backend.Application.Common.Services;

/// <inheritdoc cref="IScoringEngineService"/>
///
/// NCalc formula syntax examples:
///   • "[LDR] + [EMP] * 2"                   — numeric accumulation
///   • "if([OPT_42] == true, 'ENTJ', 'INTJ')" — string result via boolean option flag
///   • "[LEADERSHIP_SCORE] > 80"              — cascade reference to a prior scale result
///
/// MemoryPool keys:
///   • Variable keys (e.g. "LDR", "EMP")     — seeded from TestVariable.DefaultValue,
///     then accumulated from QuestionOptionPoints and ManualGrades.
///   • "OPT_{optionId}" (e.g. "OPT_42")      — boolean true for every selected option.
///   • Scale keys (e.g. "LEADERSHIP_SCORE")  — injected after each scale is evaluated
///     (cascade feature) so later formulas can reference earlier results.
public sealed class ScoringEngineService : IScoringEngineService
{
    private readonly IApplicationDbContext          _context;
    private readonly ILogger<ScoringEngineService>  _logger;

    public ScoringEngineService(
        IApplicationDbContext         context,
        ILogger<ScoringEngineService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    /// <inheritdoc/>
    public async Task CalculateResultsAsync(
        int assignmentId, CancellationToken cancellationToken = default)
    {
        // ── 1. Load assignment ────────────────────────────────────────────────
        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Assignment {assignmentId} not found.");

        // ── 2. Seed memoryPool from TestVariable default values ───────────────
        //
        // All variable keys start at their configured default (typically 0.0).
        // Typed as Dictionary<string, object?> to match NCalc's IDictionary<string, object?>
        // parameter contract. Both doubles (numeric) and booleans (OPT_ flags) fit cleanly.

        var variables = await _context.TestVariables
            .AsNoTracking()
            .Where(v => v.TestId == assignment.TestId)
            .ToListAsync(cancellationToken);

        // { "LDR": 0.0,  "EMP": 2.5, ... }
        var memoryPool = variables.ToDictionary<TestVariable, string, object?>(
            v => v.Key,
            v => (object?)v.DefaultValue);

        var variableById = variables.ToDictionary(v => v.Id);

        // ── 3. Walk AssignmentAnswers ─────────────────────────────────────────

        // Load answers together with their parent question (need VariableKey + QuestionType).
        var answers = await _context.AssignmentAnswers
            .AsNoTracking()
            .Include(a => a.Question)
            .Where(a => a.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        // Collect all unique selected option IDs in one pass (used for the batch DB query below).
        var allSelectedOptionIds = answers
            .SelectMany(a => a.SelectedOptionIds)
            .Distinct()
            .ToList();

        // 3a. NumberValue → add to the corresponding variable key.
        //     The formula designer maps question.VariableKey to a TestVariable.Key, so
        //     we can accumulate it directly.
        foreach (var answer in answers)
        {
            if (answer.NumberValue.HasValue
                && !string.IsNullOrEmpty(answer.Question?.VariableKey)
                && memoryPool.ContainsKey(answer.Question.VariableKey))
            {
                memoryPool[answer.Question.VariableKey] =
                    (double)(memoryPool[answer.Question.VariableKey] ?? 0d) + answer.NumberValue.Value;
            }

            // 3b. OPT_ boolean flags — one per selected option ID.
            //     These allow formula authors to write direct option checks:
            //       if([OPT_42] == true, 10, 0)
            foreach (var optionId in answer.SelectedOptionIds)
            {
                memoryPool[$"OPT_{optionId}"] = (object?)true;
            }
        }

        // 3c. QuestionOptionPoints → accumulate Points into variable keys.
        //     Single round-trip: load all matching point rows for selected options on this test.
        if (allSelectedOptionIds.Count > 0)
        {
            var optionPoints = await _context.QuestionOptionPoints
                .AsNoTracking()
                .Where(p => allSelectedOptionIds.Contains(p.OptionId)
                         && p.TestVariable.TestId == assignment.TestId)
                .Select(p => new { p.TestVariableId, p.Points })
                .ToListAsync(cancellationToken);

            foreach (var pt in optionPoints)
            {
                if (variableById.TryGetValue(pt.TestVariableId, out var v)
                    && memoryPool.ContainsKey(v.Key))
                {
                    memoryPool[v.Key] = (double)(memoryPool[v.Key] ?? 0d) + pt.Points;
                }
            }
        }

        // ── 4. Add ManualGrade contributions ─────────────────────────────────
        var manualGrades = await _context.ManualGrades
            .AsNoTracking()
            .Where(g => g.AssignmentId == assignmentId)
            .Select(g => new { g.TestVariableId, g.Points })
            .ToListAsync(cancellationToken);

        foreach (var grade in manualGrades)
        {
            if (variableById.TryGetValue(grade.TestVariableId, out var v)
                && memoryPool.ContainsKey(v.Key))
            {
                memoryPool[v.Key] = (double)(memoryPool[v.Key] ?? 0d) + grade.Points;
            }
        }

        // ── 5. Evaluate NCalc formulas & upsert AssignmentResults ─────────────
        //
        // Scales are processed in ascending CalculationOrder so that cascade
        // references (a later formula reading an earlier scale's result) work.

        var scales = await _context.ScoringScales
            .AsNoTracking()
            .Where(s => s.TestId == assignment.TestId)
            .OrderBy(s => s.CalculationOrder)
            .ThenBy(s => s.Id)          // stable tie-break
            .ToListAsync(cancellationToken);

        // Load existing results (for upsert) — bypass soft-delete filter so we
        // can restore a previously deleted result row if the assignment is re-scored.
        var existingResults = await _context.AssignmentResults
            .IgnoreQueryFilters()
            .Where(r => r.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        var existingByScale = existingResults.ToDictionary(r => r.ScaleId);

        foreach (var scale in scales)
        {
            object? evalResult = null;
            bool evalSucceeded = false;

            try
            {
                var expression = new Expression(scale.FormulaExpression);

                // Bind the entire memoryPool so variables, booleans and cascade
                // keys are all available to the formula author.
                expression.Parameters = memoryPool;

                evalResult    = expression.Evaluate();
                evalSucceeded = true;

                _logger.LogDebug(
                    "Scale {ScaleId} ('{ScaleName}', key={ScaleKey}) evaluated to {Result}",
                    scale.Id, scale.Name, scale.Key, evalResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "NCalc evaluation failed for scale {ScaleId} ('{ScaleName}') " +
                    "on assignment {AssignmentId}. Formula: [{Formula}]",
                    scale.Id, scale.Name, assignmentId, scale.FormulaExpression);

                // Graceful degradation: continue scoring remaining scales.
                // Mark the result row with a sentinel text so the frontend can
                // render a meaningful fallback rather than an empty cell, and so
                // the reviewer knows which metric needs attention.
                evalResult    = null;
                evalSucceeded = false;
            }

            // ── Determine result type and build/update the entity ─────────────
            decimal? calculatedScore = null;

            // Default to null; overwritten below for successful evaluations, or
            // set to the error sentinel when the formula threw an exception.
            string? resultText = evalSucceeded ? null : "Evaluation Error";

            if (evalSucceeded && evalResult is not null)
            {
                // NCalc may return int, long, double, float, decimal, or string.
                // Anything IConvertible + numeric category → CalculatedScore.
                // String (or other non-numeric) → ResultText.
                resultText = evalResult switch
                {
                    string s                           => s,
                    _ when IsNumeric(evalResult)       => null,   // handled below
                    _                                  => evalResult.ToString()
                };

                if (IsNumeric(evalResult))
                {
                    calculatedScore = Convert.ToDecimal(evalResult);
                }
            }

            // Upsert the result row.
            if (existingByScale.TryGetValue(scale.Id, out var existing))
            {
                existing.CalculatedScore = calculatedScore;
                existing.ResultText      = resultText;
                existing.IsDeleted       = false;   // restore if previously soft-deleted
            }
            else
            {
                _context.AssignmentResults.Add(new AssignmentResult
                {
                    AssignmentId    = assignmentId,
                    TenantId        = assignment.TenantId,
                    ScaleId         = scale.Id,
                    CalculatedScore = calculatedScore,
                    ResultText      = resultText,
                    IsDeleted       = false
                });
            }

            // ── Cascade: inject this scale's result back into memoryPool ──────
            //
            // This allows a formula for scale with CalculationOrder=2 to reference
            // [LEADERSHIP_SCORE] — the result of scale with CalculationOrder=1.
            // Only inject when evaluation succeeded (don't pollute with nulls).
            if (evalSucceeded && evalResult is not null)
            {
                // Inject the raw evaluated value so downstream formulas receive
                // the same type (double, string, bool) rather than a decimal.
                memoryPool[scale.Key] = evalResult;
            }
        }

        // ── 6. Transition to Completed & persist everything ───────────────────
        assignment.Status = AssignmentStatus.Completed;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assignment {AssignmentId} scored successfully. {ScaleCount} scale(s) processed.",
            assignmentId, scales.Count);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Returns true for the numeric types NCalc can return.</summary>
    private static bool IsNumeric(object value) => value is
        int or long or short or byte or
        uint or ulong or ushort or sbyte or
        float or double or decimal;
}
