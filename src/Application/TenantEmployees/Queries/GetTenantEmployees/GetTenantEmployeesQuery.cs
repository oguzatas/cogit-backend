using backend.Application.Common.Extensions;
using backend.Application.Common.Interfaces;
using backend.Application.Common.Models;

namespace backend.Application.TenantEmployees.Queries.GetTenantEmployees;

public record GetTenantEmployeesQuery : IRequest<PagedResult<TenantEmployeeDto>>
{
    public int? TenantId     { get; init; }
    public int? DepartmentId { get; init; }
    public int  PageNumber   { get; init; } = 1;
    public int  PageSize     { get; init; } = 10;
}

public record TenantEmployeeDto(
    int            Id,
    int            TenantId,
    int            DepartmentId,
    string         DepartmentName,
    string         FullName,
    string         Email,
    DateTimeOffset Created);

public class GetTenantEmployeesQueryHandler
    : IRequestHandler<GetTenantEmployeesQuery, PagedResult<TenantEmployeeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTenantEmployeesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<PagedResult<TenantEmployeeDto>> Handle(
        GetTenantEmployeesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.TenantEmployees
            .AsNoTracking()
            .Include(e => e.Department)
            .AsQueryable();

        if (request.TenantId.HasValue)
            query = query.Where(e => e.TenantId == request.TenantId.Value);

        if (request.DepartmentId.HasValue)
            query = query.Where(e => e.DepartmentId == request.DepartmentId.Value);

        // ORDER BY must be applied before ToPagedResultAsync to guarantee stable pagination.
        return await query
            .OrderBy(e => e.FullName)
            .Select(e => new TenantEmployeeDto(
                e.Id,
                e.TenantId,
                e.DepartmentId,
                e.Department.Name,
                e.FullName,
                e.Email,
                e.Created))
            .ToPagedResultAsync(request.PageNumber, request.PageSize, cancellationToken);
    }
}
