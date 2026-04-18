using backend.Application.Common.Extensions;
using backend.Application.Common.Interfaces;
using backend.Application.Common.Models;

namespace backend.Application.AuditLogs.Queries.GetAuditLogs;

// ── Request ───────────────────────────────────────────────────────────────────

/// <summary>
/// Returns a paginated list of <see cref="AuditLogDto"/> records.
///
/// Tenant scoping is applied manually (no global query filter on AuditLog):
///   • TenantStaff: restricted to their own <c>TenantId</c>.
///   • SuperAdmin  (TenantId == null): sees all records.
/// </summary>
public record GetAuditLogsQuery : IRequest<PagedResult<AuditLogDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize   { get; init; } = 20;

    // ── Optional filters ──────────────────────────────────────────────────────
    /// <summary>Filter to a specific actor's Identity sub claim.</summary>
    public string? UserId     { get; init; }

    /// <summary>Filter by simplified entity name (e.g. "Test", "Assignment").</summary>
    public string? EntityName { get; init; }

    /// <summary>Filter by command name (e.g. "CreateTestCommand").</summary>
    public string? ActionName { get; init; }

    /// <summary>Only return entries at or after this UTC timestamp.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only return entries at or before this UTC timestamp.</summary>
    public DateTimeOffset? To   { get; init; }
}

// ── DTO ───────────────────────────────────────────────────────────────────────

public record AuditLogDto(
    int            Id,
    string?        UserId,
    string         ActionName,
    string         EntityName,
    int?           EntityId,
    DateTimeOffset Timestamp,
    int?           TenantId);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetAuditLogsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var pageSize   = Math.Clamp(request.PageSize, 1, 100);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        // ── Tenant isolation ──────────────────────────────────────────────────
        //
        // AuditLog has no global query filter (it has no IsDeleted column).
        // We enforce tenant scoping here: TenantStaff can only see logs that
        // were produced within their tenant context.
        if (_currentUser.TenantId.HasValue)
            query = query.Where(a => a.TenantId == _currentUser.TenantId);

        // ── Optional filters ──────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.UserId))
            query = query.Where(a => a.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.EntityName))
            query = query.Where(a => a.EntityName == request.EntityName);

        if (!string.IsNullOrWhiteSpace(request.ActionName))
            query = query.Where(a => a.ActionName == request.ActionName);

        if (request.From.HasValue)
            query = query.Where(a => a.Timestamp >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(a => a.Timestamp <= request.To.Value);

        // Most-recent entries first.
        var projected = query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.ActionName,
                a.EntityName,
                a.EntityId,
                a.Timestamp,
                a.TenantId));

        return await projected.ToPagedResultAsync(pageNumber, pageSize, cancellationToken);
    }
}
