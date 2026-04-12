using backend.Application.Departments.Commands.CreateDepartment;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Department management within a Tenant.
/// All operations are restricted to SystemAdmin.
/// </summary>
public class Departments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(CreateDepartment)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));
    }

    [EndpointSummary("Create a Department")]
    [EndpointDescription(
        "Creates a new Department within the specified Tenant. " +
        "Department names must be unique within a Tenant. Restricted to SystemAdmin.")]
    public static async Task<Created<int>> CreateDepartment(ISender sender, CreateDepartmentCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/Departments/{id}", id);
    }
}
