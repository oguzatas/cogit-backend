using backend.Application.TenantEmployees.Commands.AddTenantEmployee;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// TenantEmployee (passwordless test-taker) management.
///
/// Flow A — Silent Provisioning: SystemAdmin creates an employee directly.
/// Flow B — Self-Service Invite: handled by <see cref="InviteCodes"/>.
/// </summary>
public class TenantEmployees : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(AddTenantEmployee)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));
    }

    [EndpointSummary("Add a TenantEmployee (Flow A — Silent Provisioning)")]
    [EndpointDescription(
        "Directly creates a TenantEmployee under the specified Tenant and Department. " +
        "The employee is active immediately. No email is sent. " +
        "Restricted to SystemAdmin.")]
    public static async Task<Created<int>> AddTenantEmployee(
        ISender sender, AddTenantEmployeeCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/TenantEmployees/{id}", id);
    }
}
