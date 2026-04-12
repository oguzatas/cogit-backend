using backend.Application.Common.Interfaces;
using backend.Application.TenantEmployees.Queries.GetTenantEmployees;

namespace backend.Application.TenantEmployees.Queries.GetTenantEmployee;

public record GetTenantEmployeeQuery(int Id) : IRequest<TenantEmployeeDto>;

public class GetTenantEmployeeQueryHandler
    : IRequestHandler<GetTenantEmployeeQuery, TenantEmployeeDto>
{
    private readonly IApplicationDbContext _context;

    public GetTenantEmployeeQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<TenantEmployeeDto> Handle(
        GetTenantEmployeeQuery request, CancellationToken cancellationToken)
    {
        var employee = await _context.TenantEmployees
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => e.Id == request.Id)
            .Select(e => new TenantEmployeeDto(
                e.Id,
                e.TenantId,
                e.DepartmentId,
                e.Department.Name,
                e.FullName,
                e.Email,
                e.Created))
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, employee);

        return employee;
    }
}
