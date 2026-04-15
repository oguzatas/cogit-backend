using backend.Application.Common.Interfaces;

namespace backend.Application.Tests.Queries.GetTestMetrics;

public record GetTestMetricsQuery(int TestId) : IRequest<List<ScoringScaleDto>>;

public record ScoringScaleDto(
    int    Id,
    int    TestId,
    string Name,
    string FormulaExpression);

public class GetTestMetricsQueryHandler : IRequestHandler<GetTestMetricsQuery, List<ScoringScaleDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTestMetricsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<ScoringScaleDto>> Handle(
        GetTestMetricsQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NotFound(
            request.TestId,
            await _context.Tests.FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken));

        return await _context.ScoringScales
            .AsNoTracking()
            .Where(s => s.TestId == request.TestId)
            .OrderBy(s => s.Name)
            .Select(s => new ScoringScaleDto(s.Id, s.TestId, s.Name, s.FormulaExpression))
            .ToListAsync(cancellationToken);
    }
}
