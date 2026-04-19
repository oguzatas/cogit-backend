using System.Security.Cryptography;
using backend.Application.Common.Interfaces;
using backend.Domain.Entities;
using backend.Domain.Enums;

namespace backend.Application.Assignments.Commands.GenerateDepartmentAssignments;

/// <summary>
/// Assigns a Test to every TenantEmployee in a Department.
/// Employees who already have an Assignment for this Test are skipped.
///
/// Security contract:
///   <see cref="TenantId"/> MUST be injected by the endpoint from JWT claims (TenantStaff)
///   or validated from the request body (SuperAdmin).
///   <see cref="AssignedByStaffId"/> is set from JWT claims for TenantStaff; null for SuperAdmin.
/// </summary>
public record GenerateDepartmentAssignmentsCommand : IRequest<GenerateDepartmentAssignmentsResult>
{
    /// <summary>Injected from JWT claims (TenantStaff) or request body (SuperAdmin).</summary>
    public int TenantId { get; init; }

    public int DepartmentId { get; init; }

    public int TestId { get; init; }

    /// <summary>
    /// Set from <c>domain_user_id</c> JWT claim when called by TenantStaff.
    /// NULL when called by SuperAdmin (system-level operation).
    /// </summary>
    public int? AssignedByStaffId { get; init; }
}

public record GenerateDepartmentAssignmentsResult(int Created, int Skipped);

public class GenerateDepartmentAssignmentsCommandHandler
    : IRequestHandler<GenerateDepartmentAssignmentsCommand, GenerateDepartmentAssignmentsResult>
{
    private readonly IApplicationDbContext _context;

    public GenerateDepartmentAssignmentsCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<GenerateDepartmentAssignmentsResult> Handle(
        GenerateDepartmentAssignmentsCommand request, CancellationToken cancellationToken)
    {
        // Guard: Test must exist and be published.
        var test = await _context.Tests
            .FirstOrDefaultAsync(t => t.Id == request.TestId && t.IsPublished, cancellationToken);

        Guard.Against.NotFound(request.TestId, test);

        // Load all active TenantEmployees in the Department.
        var employees = await _context.TenantEmployees
            .Where(e =>
                e.TenantId     == request.TenantId &&
                e.DepartmentId == request.DepartmentId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        // Skip employees who already have an Assignment for this Test.
        var alreadyAssigned = await _context.Assignments
            .Where(a =>
                a.TenantId == request.TenantId &&
                a.TestId   == request.TestId &&
                employees.Contains(a.TenantEmployeeId))
            .Select(a => a.TenantEmployeeId)
            .ToListAsync(cancellationToken);

        var toCreate = employees.Except(alreadyAssigned).ToList();

        foreach (var employeeId in toCreate)
        {
            _context.Assignments.Add(new Assignment
            {
                TenantId          = request.TenantId,
                TestId            = request.TestId,
                TenantEmployeeId  = employeeId,
                AssignedByStaffId = request.AssignedByStaffId,
                Status            = AssignmentStatus.Pending,
                // 32 random bytes → 64-char hex magic-link token.
                AccessKey         = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
                                        .ToLowerInvariant(),
                IsDeleted         = false
            });
        }

        if (toCreate.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return new GenerateDepartmentAssignmentsResult(
            Created: toCreate.Count,
            Skipped: alreadyAssigned.Count);
    }
}
