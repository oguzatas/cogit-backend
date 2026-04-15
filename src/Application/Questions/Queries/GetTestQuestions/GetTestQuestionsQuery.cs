using backend.Application.Common.Interfaces;
using backend.Domain.ValueObjects;

namespace backend.Application.Questions.Queries.GetTestQuestions;

public record GetTestQuestionsQuery(int TestId) : IRequest<List<QuestionWithOptionsDto>>;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record QuestionWithOptionsDto(
    int                       Id,
    int                       TestId,
    string                    Text,
    string                    QuestionType,
    int                       OrderIndex,
    string                    VariableKey,
    QuestionSettings?         Settings,
    List<OptionWithPointsDto> Options);

public record OptionWithPointsDto(
    int                       Id,
    string                    Text,
    decimal?                  NumericValue,
    int                       OrderIndex,
    List<OptionPointDto>      OptionPoints);

public record OptionPointDto(
    int    Id,
    int    TestVariableId,
    string VariableKey,
    string VariableName,
    double Points);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetTestQuestionsQueryHandler
    : IRequestHandler<GetTestQuestionsQuery, List<QuestionWithOptionsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTestQuestionsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<QuestionWithOptionsDto>> Handle(
        GetTestQuestionsQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NotFound(
            request.TestId,
            await _context.Tests.FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken));

        // Load the full aggregate in one query.
        // Global query filters on Questions, QuestionOptions, QuestionOptionPoints,
        // and TestVariables automatically hide soft-deleted rows.
        var questions = await _context.Questions
            .AsNoTracking()
            .Where(q => q.TestId == request.TestId)
            .Include(q => q.Options.OrderBy(o => o.OrderIndex))
                .ThenInclude(o => o.QuestionOptionPoints)
                    .ThenInclude(p => p.TestVariable)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(cancellationToken);

        return questions.Select(q => new QuestionWithOptionsDto(
            q.Id,
            q.TestId,
            q.Text,
            q.QuestionType.ToString(),
            q.OrderIndex,
            q.VariableKey,
            q.Settings,
            q.Options.Select(o => new OptionWithPointsDto(
                o.Id,
                o.Text,
                o.NumericValue,
                o.OrderIndex,
                o.QuestionOptionPoints.Select(p => new OptionPointDto(
                    p.Id,
                    p.TestVariableId,
                    p.TestVariable.Key,
                    p.TestVariable.Name,
                    p.Points))
                .ToList()))
            .ToList()))
        .ToList();
    }
}
