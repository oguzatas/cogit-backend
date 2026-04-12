using backend.Application.Tenants.Commands.CreateTenant;
using backend.Application.Tenants.Commands.DeleteTenant;
using backend.Application.Tenants.Commands.UpdateTenant;
using backend.Application.Tenants.Queries.GetTenant;
using backend.Application.Tenants.Queries.GetTenants;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Tenant (Clinic / Corporate Client) management.
/// All operations restricted to SuperAdmin.
///
/// GET    /api/Tenants         — list all tenants
/// GET    /api/Tenants/{id}    — get single tenant
/// POST   /api/Tenants         — create tenant
/// PUT    /api/Tenants/{id}    — update name / description
/// DELETE /api/Tenants/{id}    — soft-delete
/// </summary>
public class Tenants : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));

        groupBuilder.MapGet(GetTenants);
        groupBuilder.MapGet(GetTenant, "{id}");
        groupBuilder.MapPost(CreateTenant);
        groupBuilder.MapPut(UpdateTenant, "{id}");
        groupBuilder.MapDelete(DeleteTenant, "{id}");
    }

    // ── GET /api/Tenants ─────────────────────────────────────────────────────

    [EndpointSummary("List all Tenants")]
    [EndpointDescription("Returns all active (non-deleted) tenants ordered by name.")]
    public static async Task<Ok<List<TenantDto>>> GetTenants(ISender sender)
    {
        var result = await sender.Send(new GetTenantsQuery());
        return TypedResults.Ok(result);
    }

    // ── GET /api/Tenants/{id} ────────────────────────────────────────────────

    [EndpointSummary("Get a Tenant")]
    [EndpointDescription("Returns the tenant with the given id. Returns 404 if not found or soft-deleted.")]
    public static async Task<Results<Ok<TenantDto>, NotFound>> GetTenant(ISender sender, int id)
    {
        var result = await sender.Send(new GetTenantQuery(id));
        return TypedResults.Ok(result);
    }

    // ── POST /api/Tenants ────────────────────────────────────────────────────

    [EndpointSummary("Create a Tenant")]
    [EndpointDescription("Registers a new Clinic or corporate Client. Name must be unique.")]
    public static async Task<Created<int>> CreateTenant(ISender sender, CreateTenantCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/Tenants/{id}", id);
    }

    // ── PUT /api/Tenants/{id} ────────────────────────────────────────────────

    [EndpointSummary("Update a Tenant")]
    [EndpointDescription("Updates the name and/or description of an existing tenant. Name must remain unique.")]
    public static async Task<Results<NoContent, NotFound>> UpdateTenant(
        ISender sender, int id, UpdateTenantRequest request)
    {
        await sender.Send(new UpdateTenantCommand
        {
            Id          = id,
            Name        = request.Name,
            Description = request.Description
        });
        return TypedResults.NoContent();
    }

    // ── DELETE /api/Tenants/{id} ─────────────────────────────────────────────

    [EndpointSummary("Delete a Tenant")]
    [EndpointDescription("Soft-deletes the tenant. The row is retained in the database but hidden from all queries.")]
    public static async Task<Results<NoContent, NotFound>> DeleteTenant(ISender sender, int id)
    {
        await sender.Send(new DeleteTenantCommand(id));
        return TypedResults.NoContent();
    }
}

/// <summary>Request body for PUT — id comes from the route, not the body.</summary>
public record UpdateTenantRequest(string Name, string? Description);
