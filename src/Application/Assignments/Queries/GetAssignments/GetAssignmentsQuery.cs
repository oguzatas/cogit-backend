using backend.Application.Common.Extensions;
using backend.Application.Common.Interfaces;
using backend.Application.Common.Models;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Queries.GetAssignments;

// ── Request ───────────────────────────────────────────────────────────────────

/// <summary>
/// Returns a paginated, optionally-filtered list of assignments.
///
/// Tenant isolation is enforced automatically by the global query filter on
/// <see cref="backend.Domain.Entities.Assignment"/>: TenantStaff sees only their
/// own tenant's rows; SuperAdmin (TenantId == null) sees all tenants.
/// </summary>
public record GetAssignmentsQuery : IRequest<PagedResult<AssignmentSummaryDto>>
{
    // ── Pagination ────────────────────────────────────────────────────────────
    public int PageNumber { get; init; } = 1;
    public int PageSize   { get; init; } = 10;

    // ── Optional filters ──────────────────────────────────────────────────────

    /// <summary>Restrict to assignments with this status.</summary>
    public AssignmentStatus? Status { get; init; }

    /// <summary>Restrict to assignments for this test.</summary>
    public int? TestId { get; init; }

    /// <summary>
    /// Restrict to assignments whose employee belongs to this department.
    /// Resolved via <c>TenantEmployee.DepartmentId</c>.
    /// </summary>
    public int? DepartmentId { get; init; }
}

// ── DTO ───────────────────────────────────────────────────────────────────────

public record AssignmentSummaryDto(
    int              AssignmentId,
    int              TestId,
    string           TestName,
    int              TenantEmployeeId,
    string           EmployeeFullName,
    string           DepartmentName,
    AssignmentStatus Status,
    DateTimeOffset   CreatedDate,
    DateTimeOffset?  CompletedDate);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetAssignmentsQueryHandler
    : IRequestHandler<GetAssignmentsQuery, PagedResult<AssignmentSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAssignmentsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<PagedResult<AssignmentSummaryDto>> Handle(
        GetAssignmentsQuery request, CancellationToken cancellationToken)
    {
        // Clamp page size — mirror PaginationParams.MaxPageSize.
        var pageSize   = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(request.PageNumber, 1);

        // The global query filter on Assignment restricts rows to the caller's
        // tenant automatically.  No manual TenantId predicate is needed here.
        var query = _context.Assignments
            .AsNoTracking()
            .Include(a => a.Test)
            .Include(a => a.TenantEmployee)
                .ThenInclude(e => e.Department)
            .AsQueryable();

        // ── Optional filters ──────────────────────────────────────────────────

        if (request.Status.HasValue)
            query = query.Where(a => a.Status == request.Status.Value);

        if (request.TestId.HasValue)
            query = query.Where(a => a.TestId == request.TestId.Value);

        if (request.DepartmentId.HasValue)
            query = query.Where(a => a.TenantEmployee.DepartmentId == request.DepartmentId.Value);

        // ── Projection + stable sort ──────────────────────────────────────────
        //
        // Sorted newest-first so the dashboard always shows the most recent
        // activity at the top.  Stable secondary sort on Id prevents
        // non-determinism for rows created in the same millisecond.
        var projected = query
            .OrderByDescending(a => a.Created)
            .ThenByDescending(a => a.Id)
            .Select(a => new AssignmentSummaryDto(
                a.Id,
                a.TestId,
                a.Test.Name,
                a.TenantEmployeeId,
                a.TenantEmployee.FullName,
                a.TenantEmployee.Department.Name,
                a.Status,
                a.Created,
                // CompletedDate: only meaningful once the assignment is in a
                // terminal state. We expose LastModified as the completion
                // timestamp because the audit interceptor stamps it whenever
                // Status changes to Completed.
                a.Status == AssignmentStatus.Completed ? a.LastModified : (DateTimeOffset?)null));

        return await projected.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
    }
}
