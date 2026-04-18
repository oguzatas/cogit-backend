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
    string?      TextValue);

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
        // always gets a fully-populated response rather than an empty results
        // list.  The engine is idempotent (it upserts result rows) so a double
        // execution is safe.
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

        // ── Raw answers — ordered by question position in the test ───────────
        //
        // The global query filter on AssignmentAnswer also scopes by TenantId,
        // so the join is already constrained to the correct tenant.
        var answers = await _context.AssignmentAnswers
            .AsNoTracking()
            .Where(a => a.AssignmentId == request.AssignmentId)
            .Include(a => a.Question)
            .OrderBy(a => a.Question.OrderIndex)
            .Select(a => new AnswerSummaryDto(
                a.QuestionId,
                a.Question.Text,
                a.Question.QuestionType.ToString(),
                a.SelectedOptionIds,
                a.NumberValue,
                a.TextValue))
            .ToListAsync(cancellationToken);

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
        // string (e.g. a conditional label such as "High Risk"). CalculatedScore
        // is populated when it evaluates to a numeric value. Exactly one of the
        // two is non-null for any completed result row.
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
