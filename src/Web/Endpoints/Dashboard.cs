using backend.Application.Dashboard.Queries.GetDashboardStats;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Dashboard statistics endpoint.
///
/// GET /api/Dashboard/stats  [TenantStaff|SuperAdmin]
///   Returns aggregated counts and chart data for the admin home page.
///   TenantStaff: scoped to their own tenant via global query filters.
///   SuperAdmin:  sees all-tenant aggregates.
/// </summary>
public class Dashboard : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetStats, "stats")
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));
    }

    [EndpointSummary("Get Dashboard Statistics")]
    [EndpointDescription(
        "Returns aggregated statistics for the admin home page: " +
        "total published tests, assignment counts by status, active tenant count " +
        "(SuperAdmin only — always 0 for TenantStaff), " +
        "and a month-by-month activity breakdown for the trailing 12 months.")]
    public static async Task<Ok<DashboardStatsDto>> GetStats(ISender sender)
        => TypedResults.Ok(await sender.Send(new GetDashboardStatsQuery()));
}
