using backend.Application.Common.Interfaces;

namespace backend.Application.TestVariables.Queries.GetTestVariables;

public record GetTestVariablesQuery(int TestId) : IRequest<List<TestVariableDto>>;

public record TestVariableDto(
    int    Id,
    int    TestId,
    string Name,
    string Key,
    double DefaultValue);

public class GetTestVariablesQueryHandler : IRequestHandler<GetTestVariablesQuery, List<TestVariableDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTestVariablesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<TestVariableDto>> Handle(
        GetTestVariablesQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NotFound(
            request.TestId,
            await _context.Tests.FirstOrDefaultAsync(t => t.Id == request.TestId, cancellationToken));

        return await _context.TestVariables
            .AsNoTracking()
            .Where(v => v.TestId == request.TestId)
            .OrderBy(v => v.Key)
            .Select(v => new TestVariableDto(v.Id, v.TestId, v.Name, v.Key, v.DefaultValue))
            .ToListAsync(cancellationToken);
    }
}
