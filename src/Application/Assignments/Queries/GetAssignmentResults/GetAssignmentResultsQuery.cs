using backend.Application.Common.Interfaces;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Queries.GetAssignmentResults;

public record GetAssignmentResultsQuery(int AssignmentId) : IRequest<AssignmentResultsDto>;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record AssignmentResultsDto(
    int                        AssignmentId,
    int                        TestId,
    string                     TestName,
    int                        TenantId,
    string                     Status,
    List<AnswerSummaryDto>     Answers,
    List<ManualGradeSummary>   ManualGrades,
    /// <summary>
    /// Per-variable accumulated totals after all answers and manual grades have
    /// been applied — i.e. the exact values the NCalc formulas received as
    /// their input parameters.
    ///
    /// Key   = <see cref="backend.Domain.Entities.TestVariable.Key"/> (NCalc identifier, e.g. "LDR").
    /// Value = accumulated double (DefaultValue + OptionPoints + ManualGrades).
    ///
    /// Intended for radar / bar charts on the frontend report page.
    /// </summary>
    Dictionary<string, double> VariableTotals,
    List<ScaleResultDto>       Results);

public record AnswerSummaryDto(
    int          QuestionId,
    string       QuestionText,
    string       QuestionType,
    List<int>    SelectedOptionIds,
    double?      NumberValue,
    string?      TextValue,
    /// <summary>
    /// Human-readable answer label for the report view.
    /// • Choice questions: comma-separated selected option texts (e.g. "Strongly Agree").
    /// • Rating questions: the numeric value as a string (e.g. "7").
    /// • TextInput questions: the verbatim text the respondent typed.
    /// Null if the question was skipped / no answer recorded.
    /// </summary>
    string?      UserAnswerLabel,
    /// <summary>
    /// Total points this specific answer contributed to the scoring pool across
    /// all variables (matches exactly what the scoring engine accumulates).
    /// • Choice questions: sum of QuestionOptionPoint.Points for all selected options.
    /// • Rating questions: the NumberValue itself (fed directly into the formula).
    /// • TextInput questions: 0 — contribution is recorded in ManualGrades.
    /// </summary>
    double       PointsAwarded,
    /// <summary>Symbolic key used by NCalc formulas (e.g. "Q1", "PHQ_1").</summary>
    string       VariableKey);

public record ManualGradeSummary(
    int    TestVariableId,
    string VariableKey,
    string VariableName,
    double Points);

/// <summary>
/// One evaluated ScoringScale result.
///
/// NCalc may return either a numeric value or a string:
///   • Numeric  → <see cref="CalculatedScore"/> is set, <see cref="ResultText"/> is null.
///   • String   → <see cref="ResultText"/> is set  (e.g. "ENTJ", "High Risk"),
///                <see cref="CalculatedScore"/> is null.
///   • Error    → <see cref="ResultText"/> == "Evaluation Error",
///                <see cref="CalculatedScore"/> is null.
///
/// The scoring engine handles the type dispatch safely — no casting exception
/// can occur because it inspects the runtime type of the evaluated object before
/// assigning it (see ScoringEngineService.IsNumeric / the switch expression).
/// </summary>
public record ScaleResultDto(
    int      ScaleId,
    string   ScaleName,
    string   FormulaExpression,
    decimal? CalculatedScore,
    string?  ResultText);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetAssignmentResultsQueryHandler
    : IRequestHandler<GetAssignmentResultsQuery, AssignmentResultsDto>
{
    private readonly IApplicationDbContext  _context;
    private readonly IScoringEngineService  _scoringEngine;

    public GetAssignmentResultsQueryHandler(
        IApplicationDbContext context,
        IScoringEngineService scoringEngine)
    {
        _context       = context;
        _scoringEngine = scoringEngine;
    }

    public async Task<AssignmentResultsDto> Handle(
        GetAssignmentResultsQuery request, CancellationToken cancellationToken)
    {
        // ── Load assignment + test ────────────────────────────────────────────
        //
        // The global query filter on Assignment enforces tenant isolation:
        // TenantStaff match only their own tenant; SuperAdmin sees all.
        var assignment = await _context.Assignments
            .AsNoTracking()
            .Include(a => a.Test)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        Guard.Against.NotFound(request.AssignmentId, assignment);

        // ── On-demand scoring trigger ─────────────────────────────────────────
        //
        // If the engine failed silently at submission time the result rows may
        // be absent even though Status == Completed. Re-running is safe: the
        // engine is idempotent (upserts). AwaitingManualGrading is excluded.
        if (assignment.Status == AssignmentStatus.Completed)
        {
            var hasResults = await _context.AssignmentResults
                .AnyAsync(r => r.AssignmentId == request.AssignmentId, cancellationToken);

            if (!hasResults)
                await _scoringEngine.CalculateResultsAsync(request.AssignmentId, cancellationToken);
        }

        // ── TestVariables — needed for VariableTotals ─────────────────────────
        //
        // We replicate the same seed + accumulation that the scoring engine
        // performs when building its memoryPool, so VariableTotals contains
        // the exact per-variable inputs the NCalc formulas received.
        var testVariables = await _context.TestVariables
            .AsNoTracking()
            .Where(v => v.TestId == assignment.TestId)
            .Select(v => new { v.Id, v.Key, v.DefaultValue })
            .ToListAsync(cancellationToken);

        // variableTotals starts at each variable's configured default value.
        var variableTotals = testVariables.ToDictionary(v => v.Key, v => v.DefaultValue);

        // variableById used to map QuestionOptionPoint.TestVariableId → Key.
        var variableById = testVariables.ToDictionary(v => v.Id, v => v.Key);

        // ── Raw answers ───────────────────────────────────────────────────────
        //
        // Load entities (not projected) because the enriched fields require
        // joining on JSONB SelectedOptionIds — EF cannot express that in SQL.
        var rawAnswers = await _context.AssignmentAnswers
            .AsNoTracking()
            .Where(a => a.AssignmentId == request.AssignmentId)
            .Include(a => a.Question)
            .OrderBy(a => a.Question.OrderIndex)
            .ToListAsync(cancellationToken);

        // ── Batch-load option data for all selected options ───────────────────
        var allSelectedOptionIds = rawAnswers
            .SelectMany(a => a.SelectedOptionIds)
            .Distinct()
            .ToList();

        // option id → label text  (for UserAnswerLabel on choice questions)
        Dictionary<int, string> optionLabels = new();

        // Raw point rows — includes TestVariableId so one query serves both
        // PointsAwarded (per-answer total) and VariableTotals (per-variable accumulation).
        List<(int OptionId, int TestVariableId, double Points)> rawOptionPoints = new();

        if (allSelectedOptionIds.Count > 0)
        {
            optionLabels = await _context.QuestionOptions
                .AsNoTracking()
                .Where(o => allSelectedOptionIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Text })
                .ToDictionaryAsync(o => o.Id, o => o.Text, cancellationToken);

            rawOptionPoints = await _context.QuestionOptionPoints
                .AsNoTracking()
                .Where(p => allSelectedOptionIds.Contains(p.OptionId))
                .Select(p => new { p.OptionId, p.TestVariableId, p.Points })
                .ToListAsync(cancellationToken)
                .ContinueWith(t => t.Result
                    .Select(p => (p.OptionId, p.TestVariableId, p.Points))
                    .ToList(), cancellationToken);
        }

        // option id → total points across all variables (for AnswerSummaryDto.PointsAwarded)
        var pointsPerOption = rawOptionPoints
            .GroupBy(p => p.OptionId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Points));

        // ── Accumulate NumberValue answers into variableTotals ─────────────────
        foreach (var answer in rawAnswers)
        {
            if (answer.NumberValue.HasValue
                && !string.IsNullOrEmpty(answer.Question?.VariableKey)
                && variableTotals.ContainsKey(answer.Question.VariableKey))
            {
                variableTotals[answer.Question.VariableKey] += answer.NumberValue.Value;
            }
        }

        // ── Accumulate option points into variableTotals ───────────────────────
        foreach (var (_, testVariableId, points) in rawOptionPoints)
        {
            if (variableById.TryGetValue(testVariableId, out var key)
                && variableTotals.ContainsKey(key))
            {
                variableTotals[key] += points;
            }
        }

        // ── Map answers to DTOs in memory ─────────────────────────────────────
        var answers = rawAnswers.Select(a =>
        {
            var qType    = a.Question.QuestionType;
            bool isChoice = qType is QuestionType.SingleChoice
                                   or QuestionType.MultipleChoice
                                   or QuestionType.LikertScale;

            // UserAnswerLabel
            string? label = null;
            if (isChoice && a.SelectedOptionIds.Count > 0)
            {
                label = string.Join(", ", a.SelectedOptionIds.Select(id =>
                    optionLabels.TryGetValue(id, out var t) ? t : $"Option {id}"));
            }
            else if (qType == QuestionType.TextInput)
            {
                label = a.TextValue;
            }
            else if (qType == QuestionType.Rating && a.NumberValue.HasValue)
            {
                label = a.NumberValue.Value.ToString("G");
            }

            // PointsAwarded
            double pointsAwarded = 0d;
            if (isChoice)
            {
                pointsAwarded = a.SelectedOptionIds
                    .Sum(id => pointsPerOption.TryGetValue(id, out var pts) ? pts : 0d);
            }
            else if (qType == QuestionType.Rating && a.NumberValue.HasValue)
            {
                // NumberValue is fed directly into the formula via VariableKey.
                pointsAwarded = a.NumberValue.Value;
            }
            // TextInput: captured separately in ManualGrades — leave as 0.

            return new AnswerSummaryDto(
                a.QuestionId,
                a.Question.Text,
                a.Question.QuestionType.ToString(),
                a.SelectedOptionIds,
                a.NumberValue,
                a.TextValue,
                label,
                pointsAwarded,
                a.Question.VariableKey);
        }).ToList();

        // ── Manual grades ─────────────────────────────────────────────────────
        var manualGrades = await _context.ManualGrades
            .AsNoTracking()
            .Where(g => g.AssignmentId == request.AssignmentId)
            .Include(g => g.TestVariable)
            .Select(g => new ManualGradeSummary(
                g.TestVariableId,
                g.TestVariable.Key,
                g.TestVariable.Name,
                g.Points))
            .ToListAsync(cancellationToken);

        // ── Accumulate manual grades into variableTotals ───────────────────────
        //
        // Must happen after manualGrades is loaded, because ManualGrade rows are
        // the last input the scoring engine processes before evaluating formulas.
        foreach (var mg in manualGrades)
        {
            if (variableTotals.ContainsKey(mg.VariableKey))
                variableTotals[mg.VariableKey] += mg.Points;
        }

        // ── Calculated NCalc results ───────────────────────────────────────────
        //
        // CalculatedScore is set for numeric formula results; ResultText for
        // string results (e.g. "ENTJ", "High Risk") or "Evaluation Error" on
        // failure.  The scoring engine handles the type dispatch safely without
        // any casting — see ScoringEngineService.IsNumeric.
        var results = await _context.AssignmentResults
            .AsNoTracking()
            .Where(r => r.AssignmentId == request.AssignmentId)
            .Include(r => r.Scale)
            .OrderBy(r => r.Scale.CalculationOrder)
            .ThenBy(r => r.Scale.Name)
            .Select(r => new ScaleResultDto(
                r.ScaleId,
                r.Scale.Name,
                r.Scale.FormulaExpression,
                r.CalculatedScore,
                r.ResultText))
            .ToListAsync(cancellationToken);

        return new AssignmentResultsDto(
            assignment.Id,
            assignment.TestId,
            assignment.Test.Name,
            assignment.TenantId,
            assignment.Status.ToString(),
            answers,
            manualGrades,
            variableTotals,
            results);
    }
}
