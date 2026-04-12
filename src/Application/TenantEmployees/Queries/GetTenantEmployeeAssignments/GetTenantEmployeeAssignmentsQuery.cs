using backend.Application.Common.Interfaces;
using backend.Domain.Enums;

namespace backend.Application.TenantEmployees.Queries.GetTenantEmployeeAssignments;

public record GetTenantEmployeeAssignmentsQuery(int TenantEmployeeId)
    : IRequest<List<TenantEmployeeAssignmentDto>>;

public record TenantEmployeeAssignmentDto(
    int              Id,
    int              TestId,
    string           TestName,
    AssignmentStatus Status,
    string           AccessKey,
    DateTimeOffset   Created,
    DateTimeOffset   LastModified);

public class GetTenantEmployeeAssignmentsQueryHandler
    : IRequestHandler<GetTenantEmployeeAssignmentsQuery, List<TenantEmployeeAssignmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTenantEmployeeAssignmentsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<TenantEmployeeAssignmentDto>> Handle(
        GetTenantEmployeeAssignmentsQuery request, CancellationToken cancellationToken)
    {
        // Guard: employee must exist and be visible to the caller (global filter enforces tenant scope).
        var employeeExists = await _context.TenantEmployees
            .AsNoTracking()
            .AnyAsync(e => e.Id == request.TenantEmployeeId, cancellationToken);

        Guard.Against.NotFound(request.TenantEmployeeId, employeeExists ? (object?)new { } : null);

        return await _context.Assignments
            .AsNoTracking()
            .Include(a => a.Test)
            .Where(a => a.TenantEmployeeId == request.TenantEmployeeId)
            .OrderByDescending(a => a.Created)
            .Select(a => new TenantEmployeeAssignmentDto(
                a.Id,
                a.TestId,
                a.Test.Name,
                a.Status,
                a.AccessKey,
                a.Created,
                a.LastModified))
            .ToListAsync(cancellationToken);
    }
}
