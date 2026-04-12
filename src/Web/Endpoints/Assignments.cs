using backend.Application.Assignments.Commands.GenerateDepartmentAssignments;
using backend.Application.Common.Interfaces;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Assignment management.
///
/// POST /api/Assignments   [TenantStaff | SuperAdmin]
///   Assigns a Test to every TenantEmployee in a Department.
///   TenantStaff: TenantId and AssignedByStaffId are sourced from JWT claims.
///   SuperAdmin:  TenantId must be provided in the request body; AssignedByStaffId is null.
/// </summary>
public class Assignments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(AssignTestToDepartment)
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.TenantStaff), nameof(UserRole.SuperAdmin)));
    }

    [EndpointSummary("Assign a Test to a Department")]
    [EndpointDescription(
        "Creates one Assignment (with a unique AccessKey magic-link) for every TenantEmployee " +
        "in the specified Department. Employees who already have an Assignment for this Test " +
        "are skipped. Returns the count of created and skipped assignments. " +
        "TenantStaff: TenantId is taken from the JWT claim — omit it from the body. " +
        "SuperAdmin: TenantId must be provided in the request body.")]
    public static async Task<Results<Ok<GenerateDepartmentAssignmentsResult>, ForbidHttpResult>> AssignTestToDepartment(
        ISender sender,
        ICurrentUserService currentUserService,
        AssignTestToDepartmentRequest request)
    {
        // TenantStaff: both TenantId and DomainUserId come from claims.
        // SuperAdmin:  no TenantId claim — it must be in the request body.
        var tenantId = currentUserService.TenantId ?? request.TenantId;

        if (tenantId is null)
            return TypedResults.Forbid();

        var command = new GenerateDepartmentAssignmentsCommand
        {
            TenantId          = tenantId.Value,
            DepartmentId      = request.DepartmentId,
            TestId            = request.TestId,
            // Populated for TenantStaff; null for SuperAdmin (system-level operation).
            AssignedByStaffId = currentUserService.DomainUserId
        };

        var result = await sender.Send(command);
        return TypedResults.Ok(result);
    }
}

/// <summary>
/// Request body for assigning a Test to a Department.
/// TenantStaff callers should omit TenantId — it is sourced from the JWT claim.
/// SuperAdmin callers must provide TenantId.
/// </summary>
public record AssignTestToDepartmentRequest
{
    public int? TenantId { get; init; }
    public int DepartmentId { get; init; }
    public int TestId { get; init; }
}
