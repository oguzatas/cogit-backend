using backend.Application.Common.Interfaces;

namespace backend.Application.Departments.Queries.GetDepartments;

public record GetDepartmentsQuery(int TenantId) : IRequest<List<DepartmentDto>>;

public record DepartmentDto(
    int            Id,
    int            TenantId,
    string         Name,
    int            EmployeeCount,
    DateTimeOffset Created);

public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, List<DepartmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DepartmentDto>> Handle(
        GetDepartmentsQuery request, CancellationToken cancellationToken)
        => await _context.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == request.TenantId)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id,
                d.TenantId,
                d.Name,
                d.TenantEmployees.Count(e => !e.IsDeleted),
                d.Created))
            .ToListAsync(cancellationToken);
}
