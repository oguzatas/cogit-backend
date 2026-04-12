using backend.Application.Departments.Commands.CreateDepartment;
using backend.Application.Departments.Commands.DeleteDepartment;
using backend.Application.Departments.Commands.UpdateDepartment;
using backend.Application.Departments.Queries.GetDepartment;
using backend.Application.Departments.Queries.GetDepartments;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Department management within a Tenant.
///
/// GET    /api/Departments?tenantId={id}   [SuperAdmin | TenantStaff]  — list departments for a tenant
/// GET    /api/Departments/{id}            [SuperAdmin | TenantStaff]  — get single department
/// POST   /api/Departments                 [SuperAdmin]                — create department
/// PUT    /api/Departments/{id}            [SuperAdmin]                — rename department
/// DELETE /api/Departments/{id}            [SuperAdmin]                — soft-delete
/// </summary>
public class Departments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Reads are available to both SuperAdmin and TenantStaff.
        groupBuilder.MapGet(GetDepartments)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapGet(GetDepartment, "{id}")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        // Writes are SuperAdmin only.
        groupBuilder.MapPost(CreateDepartment)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapPut(UpdateDepartment, "{id}")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapDelete(DeleteDepartment, "{id}")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));
    }

    // ── GET /api/Departments?tenantId={id} ───────────────────────────────────

    [EndpointSummary("List Departments for a Tenant")]
    [EndpointDescription("Returns all active departments for the specified tenant, ordered by name.")]
    public static async Task<Ok<List<DepartmentDto>>> GetDepartments(ISender sender, int tenantId)
    {
        var result = await sender.Send(new GetDepartmentsQuery(tenantId));
        return TypedResults.Ok(result);
    }

    // ── GET /api/Departments/{id} ────────────────────────────────────────────

    [EndpointSummary("Get a Department")]
    [EndpointDescription("Returns the department with the given id. Returns 404 if not found or soft-deleted.")]
    public static async Task<Ok<DepartmentDto>> GetDepartment(ISender sender, int id)
    {
        var result = await sender.Send(new GetDepartmentQuery(id));
        return TypedResults.Ok(result);
    }

    // ── POST /api/Departments ────────────────────────────────────────────────

    [EndpointSummary("Create a Department")]
    [EndpointDescription("Creates a new department within the specified tenant. Name must be unique within the tenant.")]
    public static async Task<Created<int>> CreateDepartment(ISender sender, CreateDepartmentCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/Departments/{id}", id);
    }

    // ── PUT /api/Departments/{id} ────────────────────────────────────────────

    [EndpointSummary("Rename a Department")]
    [EndpointDescription("Updates the name of an existing department. Name must remain unique within its tenant.")]
    public static async Task<NoContent> UpdateDepartment(
        ISender sender, int id, UpdateDepartmentRequest request)
    {
        await sender.Send(new UpdateDepartmentCommand { Id = id, Name = request.Name });
        return TypedResults.NoContent();
    }

    // ── DELETE /api/Departments/{id} ─────────────────────────────────────────

    [EndpointSummary("Delete a Department")]
    [EndpointDescription("Soft-deletes the department. The row is retained in the database but hidden from all queries.")]
    public static async Task<NoContent> DeleteDepartment(ISender sender, int id)
    {
        await sender.Send(new DeleteDepartmentCommand(id));
        return TypedResults.NoContent();
    }
}

/// <summary>Request body for PUT — id comes from the route, not the body.</summary>
public record UpdateDepartmentRequest(string Name);
