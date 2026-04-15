using backend.Application.Common.Interfaces;
using backend.Application.Tests.Queries.GetTests;

namespace backend.Application.Tests.Queries.GetTest;

public record GetTestQuery(int Id) : IRequest<TestDto>;

public class GetTestQueryHandler : IRequestHandler<GetTestQuery, TestDto>
{
    private readonly IApplicationDbContext _context;

    public GetTestQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<TestDto> Handle(
        GetTestQuery request, CancellationToken cancellationToken)
    {
        var test = await _context.Tests
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new TestDto(
                t.Id,
                t.Name,
                t.Description,
                t.IsPublished,
                t.Created))
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, test);

        return test;
    }
}
