using backend.Application.Common.Interfaces;

namespace backend.Application.Tests.Queries.GetTests;

public record GetTestsQuery : IRequest<List<TestDto>>;

public record TestDto(
    int            Id,
    string         Name,
    string?        Description,
    bool           IsPublished,
    DateTimeOffset Created);

public class GetTestsQueryHandler : IRequestHandler<GetTestsQuery, List<TestDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTestsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<TestDto>> Handle(
        GetTestsQuery request, CancellationToken cancellationToken)
        => await _context.Tests
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TestDto(
                t.Id,
                t.Name,
                t.Description,
                t.IsPublished,
                t.Created))
            .ToListAsync(cancellationToken);
}
