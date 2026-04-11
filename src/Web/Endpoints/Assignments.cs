using backend.Application.Assignments.Commands.AssignTest;
using backend.Application.Common.Interfaces;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Manages test assignments within a Tenant.
/// Restricted to TenantStaff — only staff members may assign tests to clients.
/// </summary>
public class Assignments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // ── Authorization boundary ─────────────────────────────────────────────
        // Only TenantStaff may create assignments.
        // SystemAdmin and Clients are forbidden at the group level.
        groupBuilder.RequireAuthorization(policy =>
            policy.RequireRole(nameof(UserRole.TenantStaff)));

        groupBuilder.MapPost(AssignTest);
    }

    [EndpointSummary("Assign a Test to a Client")]
    [EndpointDescription(
        "Creates a new Assignment linking a global Test to a Client within the caller's Tenant. " +
        "TenantId and AssignedByStaffId are sourced exclusively from the caller's JWT claims — " +
        "they cannot be supplied or overridden via the request body. " +
        "Restricted to TenantStaff.")]
    public static async Task<Results<Created<int>, ValidationProblem, ForbidHttpResult>> AssignTest(
        ISender sender,
        ICurrentUserService currentUserService,
        AssignTestRequest request)
    {
        // ── B2B2C Security Boundary ────────────────────────────────────────────
        // TenantId and AssignedByStaffId are injected from the validated JWT claims.
        // A TenantStaff member can NEVER assign a test on behalf of another tenant,
        // even if they craft a malicious request body.
        if (currentUserService.TenantId is null || currentUserService.DomainUserId is null)
            return TypedResults.Forbid();

        var command = new AssignTestCommand
        {
            TenantId          = currentUserService.TenantId.Value,
            AssignedByStaffId = currentUserService.DomainUserId.Value,
            TestId            = request.TestId,
            ClientId          = request.ClientId
        };

        var id = await sender.Send(command);

        return TypedResults.Created($"/api/Assignments/{id}", id);
    }
}

/// <summary>
/// The subset of assignment data that a TenantStaff member supplies in the request body.
/// TenantId and AssignedByStaffId are deliberately excluded — they come from JWT claims.
/// </summary>
public record AssignTestRequest
{
    /// <summary>The global Test to be assigned.</summary>
    public int TestId { get; init; }

    /// <summary>The domain User (Client role) who will take the test.</summary>
    public int ClientId { get; init; }
}
