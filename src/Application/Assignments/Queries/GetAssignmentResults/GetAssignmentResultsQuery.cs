using backend.Application.Common.Interfaces;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Queries.GetAssignmentResults;

public record GetAssignmentResultsQuery(int AssignmentId) : IRequest<AssignmentResultsDto>;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record AssignmentResultsDto(
    int                      AssignmentId,
    int                      TestId,
    string                   TestName,
    int                      TenantId,
    string                   Status,
    List<AnswerSummaryDto>   Answers,
    List<ManualGradeSummary> ManualGrades,
    List<ScaleResultDto>     Results);

public record AnswerSummaryDto(
    int          QuestionId,
    string       QuestionText,
    string       QuestionType,
    List<int>    SelectedOptionIds,
    double?      NumberValue,
    string?      TextValue,
    // ── Enriched fields ──────────────────────────────────────────────────────
    /// <summary>
    /// Human-readable answer label for the report view.
    /// • Choice questions: comma-separated selected option texts (e.g. "Strongly Agree").
    /// • Rating questions: the numeric value as a string (e.g. "7").
    /// • TextInput questions: the verbatim text the respondent typed.
    /// Null if the question was skipped / no answer recorded.
    /// </summary>
    string?      UserAnswerLabel,
    /// <summary>
    /// Total points this specific answer contributed to the scoring pool.
    /// • Choice questions: sum of QuestionOptionPoint.Points for all selected options
    ///   across all variables (matches exactly what the scoring engine accumulates).
    /// • Rating questions: the NumberValue itself (fed directly into the formula
    ///   via Question.VariableKey).
    /// • TextInput questions: 0 — points are recorded separately as ManualGrades.
    /// </summary>
    double       PointsAwarded,
    /// <summary>Symbolic key used by NCalc formulas (e.g. "Q1", "PHQ_1").</summary>
    string       VariableKey);

public record ManualGradeSummary(
    int    TestVariableId,
    string VariableKey,
    string VariableName,
    double Points);

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
        // Load the assignment together with its test.
        // The global query filter on Assignment enforces tenant isolation automatically:
        // TenantStaff will only match rows where e.TenantId == currentUser.TenantId;
        // SuperAdmin (TenantId == null) bypasses the tenant predicate and sees all rows.
        var assignment = await _context.Assignments
            .AsNoTracking()
            .Include(a => a.Test)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        Guard.Against.NotFound(request.AssignmentId, assignment);

        // ── On-demand scoring trigger ────────────────────────────────────────
        //
        // The primary path (SubmitAssignment / ManualGradeAssignment) calls the
        // scoring engine at the end of its pipeline. However if that call ever
        // failed silently (transient error, deployment restart mid-request, etc.)
        // the results rows may be absent even though Status == Completed.
        //
        // When we detect that situation we re-run the engine here so the caller
        // always gets a fully-populated response rather than an empty results list.
        // The engine is idempotent (it upserts result rows) so a double execution is safe.
        //
        // AwaitingManualGrading is intentionally excluded: scoring cannot be
        // finalised until all TextInput questions are manually graded.
        if (assignment.Status == AssignmentStatus.Completed)
        {
            var hasResults = await _context.AssignmentResults
                .AnyAsync(r => r.AssignmentId == request.AssignmentId, cancellationToken);

            if (!hasResults)
                await _scoringEngine.CalculateResultsAsync(request.AssignmentId, cancellationToken);
        }

        // ── Raw answers ───────────────────────────────────────────────────────
        //
        // We load entities rather than projecting directly to the DTO because
        // the enriched fields (UserAnswerLabel, PointsAwarded) require data from
        // two additional tables (QuestionOptions, QuestionOptionPoints) whose
        // join key is stored inside a JSONB column (SelectedOptionIds). EF Core
        // cannot express that JOIN in SQL, so we do two extra batch round-trips
        // and finish the mapping in memory — the same approach the scoring engine uses.
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

        // option id → option text (for UserAnswerLabel on choice questions)
        Dictionary<int, string> optionLabels = new();

        // option id → total points that option contributes across all variables
        Dictionary<int, double> pointsPerOption = new();

        if (allSelectedOptionIds.Count > 0)
        {
            optionLabels = await _context.QuestionOptions
                .AsNoTracking()
                .Where(o => allSelectedOptionIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Text })
                .ToDictionaryAsync(o => o.Id, o => o.Text, cancellationToken);

            // Sum across all TestVariables — this matches exactly what the scoring
            // engine accumulates in its memoryPool for the same selected option.
            var rawPoints = await _context.QuestionOptionPoints
                .AsNoTracking()
                .Where(p => allSelectedOptionIds.Contains(p.OptionId))
                .Select(p => new { p.OptionId, p.Points })
                .ToListAsync(cancellationToken);

            pointsPerOption = rawPoints
                .GroupBy(p => p.OptionId)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Points));
        }

        // ── Map answers to DTOs in memory ─────────────────────────────────────
        var answers = rawAnswers.Select(a =>
        {
            var qType = a.Question.QuestionType;
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
                // Each selected option may award points to multiple variables; we sum
                // the total contribution across all of them, matching the engine's
                // memoryPool accumulation.
                pointsAwarded = a.SelectedOptionIds
                    .Sum(id => pointsPerOption.TryGetValue(id, out var pts) ? pts : 0d);
            }
            else if (qType == QuestionType.Rating && a.NumberValue.HasValue)
            {
                // NumberValue is injected directly into the scoring formula via
                // Question.VariableKey — so the value itself is the contribution.
                pointsAwarded = a.NumberValue.Value;
            }
            // TextInput: contribution is recorded separately in ManualGrades — leave as 0.

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

        // ── Manual grades (reviewer-entered points for TextInput questions) ───
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

        // ── Calculated NCalc results — ordered by scale evaluation order ─────
        //
        // ResultText is populated when the scale's NCalc formula evaluates to a
        // string (e.g. a conditional label such as "High Risk").
        // CalculatedScore is populated when it evaluates to a numeric value.
        // When evaluation fails the engine sets ResultText = "Evaluation Error"
        // so the frontend can render a meaningful fallback instead of an empty cell.
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
            results);
    }
}
