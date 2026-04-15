using backend.Application.Common.Interfaces;

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
    private readonly IApplicationDbContext _context;

    public GetAssignmentResultsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<AssignmentResultsDto> Handle(
        GetAssignmentResultsQuery request, CancellationToken cancellationToken)
    {
        var assignment = await _context.Assignments
            .AsNoTracking()
            .Include(a => a.Test)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);

        Guard.Against.NotFound(request.AssignmentId, assignment);

        // Raw answers — load question text for readability.
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

        // Manual grades (if any).
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

        // Calculated results.
        var results = await _context.AssignmentResults
            .AsNoTracking()
            .Where(r => r.AssignmentId == request.AssignmentId)
            .Include(r => r.Scale)
            .OrderBy(r => r.Scale.Name)
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
