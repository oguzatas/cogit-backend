using backend.Application.Tenants.Commands.CreateTenant;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Tenant (Clinic / Corporate Client) management.
/// All operations are restricted to SuperAdmin.
/// </summary>
public class Tenants : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(CreateTenant)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));
    }

    [EndpointSummary("Create a Tenant")]
    [EndpointDescription("Registers a new Clinic or corporate Client in the platform. Restricted to SuperAdmin.")]
    public static async Task<Created<int>> CreateTenant(ISender sender, CreateTenantCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/Tenants/{id}", id);
    }
}
