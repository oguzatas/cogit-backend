using backend.Application.AuditLogs.Queries.GetAuditLogs;
using backend.Application.Common.Models;
using backend.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Web.Endpoints;

/// <summary>
/// Audit log query endpoint.
///
/// GET /api/AuditLogs  [TenantStaff|SuperAdmin]
///   Returns a paginated list of audit entries.
///   TenantStaff: restricted to their own tenant's log entries.
///   SuperAdmin:  sees all entries (optionally filtered by tenant via query params).
/// </summary>
public class AuditLogs : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetAuditLogs)
            .RequireAuthorization(p =>
                p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.TenantStaff)));
    }

    [EndpointSummary("List Audit Logs (paginated)")]
    [EndpointDescription(
        "Returns a paginated, descending-chronological list of audit entries. " +
        "TenantStaff see only their own tenant's mutations; SuperAdmin sees all tenants. " +
        "Optional query-string filters: " +
        "userId (Identity sub claim), entityName (e.g. 'Test'), " +
        "actionName (e.g. 'CreateTestCommand'), from/to (ISO-8601 UTC timestamps). " +
        "Pagination: pageNumber (default 1), pageSize (default 20, max 100).")]
    public static async Task<Ok<PagedResult<AuditLogDto>>> GetAuditLogs(
        ISender sender,
        int?           pageNumber  = null,
        int?           pageSize    = null,
        string?        userId      = null,
        string?        entityName  = null,
        string?        actionName  = null,
        DateTimeOffset? from       = null,
        DateTimeOffset? to         = null)
    {
        var result = await sender.Send(new GetAuditLogsQuery
        {
            PageNumber = pageNumber ?? 1,
            PageSize   = pageSize   ?? 20,
            UserId     = userId,
            EntityName = entityName,
            ActionName = actionName,
            From       = from,
            To         = to
        });

        return TypedResults.Ok(result);
    }
}
