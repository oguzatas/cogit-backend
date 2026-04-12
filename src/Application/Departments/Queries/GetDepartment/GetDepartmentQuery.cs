using backend.Application.Common.Interfaces;
using backend.Application.Departments.Queries.GetDepartments;

namespace backend.Application.Departments.Queries.GetDepartment;

public record GetDepartmentQuery(int Id) : IRequest<DepartmentDto>;

public class GetDepartmentQueryHandler : IRequestHandler<GetDepartmentQuery, DepartmentDto>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<DepartmentDto> Handle(GetDepartmentQuery request, CancellationToken cancellationToken)
    {
        var dept = await _context.Departments
            .AsNoTracking()
            .Where(d => d.Id == request.Id)
            .Select(d => new DepartmentDto(d.Id, d.TenantId, d.Name, d.Created))
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, dept);

        return dept;
    }
}
