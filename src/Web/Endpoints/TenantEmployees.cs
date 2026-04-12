using backend.Application.Common.Interfaces;
using backend.Application.Common.Models;
using backend.Application.TenantEmployees.Commands.AddTenantEmployee;
using backend.Application.TenantEmployees.Queries.GetTenantEmployee;
using backend.Application.TenantEmployees.Queries.GetTenantEmployeeAssignments;
using backend.Application.TenantEmployees.Queries.GetTenantEmployees;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// TenantEmployee (passwordless test-taker) management.
///
/// GET    /api/TenantEmployees                    [SuperAdmin | TenantStaff]  — list, with optional filters
/// GET    /api/TenantEmployees/{id}               [SuperAdmin | TenantStaff]  — single employee
/// GET    /api/TenantEmployees/{id}/assignments   [SuperAdmin | TenantStaff]  — employee's assignments
/// POST   /api/TenantEmployees                    [SuperAdmin]                — add employee (Flow A)
/// </summary>
public class TenantEmployees : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetTenantEmployees)
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapGet(GetTenantEmployee, "{id}")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapGet(GetTenantEmployeeAssignments, "{id}/assignments")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));

        groupBuilder.MapPost(AddTenantEmployee)
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.SuperAdmin)));
    }

    // ── GET /api/TenantEmployees?tenantId={}&departmentId={} ─────────────────

    [EndpointSummary("List TenantEmployees (paginated)")]
    [EndpointDescription(
        "Returns a paginated list of TenantEmployees visible to the caller. " +
        "TenantStaff: results are automatically scoped to their tenant via JWT claims; " +
        "use departmentId to narrow to a specific department. " +
        "SuperAdmin: use tenantId to scope to a specific tenant, departmentId to narrow further. " +
        "Pagination: pageNumber (default 1) and pageSize (default 10, max 100).")]
    public static async Task<Ok<PagedResult<TenantEmployeeDto>>> GetTenantEmployees(
        ISender                     sender,
        ICurrentUserService         currentUser,
        [AsParameters] PaginationParams pagination,
        int?                        tenantId     = null,
        int?                        departmentId = null)
    {
        // TenantStaff: claims TenantId takes precedence — they cannot cross tenant boundaries.
        // SuperAdmin:  no TenantId in claims, so the optional query param drives the filter.
        var effectiveTenantId = currentUser.TenantId ?? tenantId;

        var result = await sender.Send(new GetTenantEmployeesQuery
        {
            TenantId     = effectiveTenantId,
            DepartmentId = departmentId,
            PageNumber   = pagination.PageNumber,
            PageSize     = pagination.PageSize
        });

        return TypedResults.Ok(result);
    }

    // ── GET /api/TenantEmployees/{id} ────────────────────────────────────────

    [EndpointSummary("Get a TenantEmployee")]
    [EndpointDescription(
        "Returns a single TenantEmployee by id. " +
        "The global query filter ensures TenantStaff can only retrieve employees within their tenant.")]
    public static async Task<Ok<TenantEmployeeDto>> GetTenantEmployee(ISender sender, int id)
    {
        var result = await sender.Send(new GetTenantEmployeeQuery(id));
        return TypedResults.Ok(result);
    }

    // ── GET /api/TenantEmployees/{id}/assignments ────────────────────────────

    [EndpointSummary("Get Assignments for a TenantEmployee")]
    [EndpointDescription(
        "Returns all assignments for the specified TenantEmployee, ordered by creation date descending. " +
        "Includes test name, status, and the AccessKey magic-link token for each assignment. " +
        "The global query filter ensures TenantStaff can only access assignments within their tenant.")]
    public static async Task<Ok<List<TenantEmployeeAssignmentDto>>> GetTenantEmployeeAssignments(
        ISender sender, int id)
    {
        var result = await sender.Send(new GetTenantEmployeeAssignmentsQuery(id));
        return TypedResults.Ok(result);
    }

    // ── POST /api/TenantEmployees ────────────────────────────────────────────

    [EndpointSummary("Add a TenantEmployee (Flow A — Silent Provisioning)")]
    [EndpointDescription(
        "Directly creates a TenantEmployee under the specified Tenant and Department. " +
        "The employee is active immediately. No email is sent. Restricted to SuperAdmin.")]
    public static async Task<Created<int>> AddTenantEmployee(
        ISender sender, AddTenantEmployeeCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/TenantEmployees/{id}", id);
    }
}
