using backend.Application.Common.Interfaces;
using backend.Domain.Enums;

namespace backend.Application.Dashboard.Queries.GetDashboardStats;

public record GetDashboardStatsQuery : IRequest<DashboardStatsDto>;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record DashboardStatsDto(
    /// <summary>Total published tests (global — not tenant-scoped).</summary>
    int TotalTests,

    /// <summary>
    /// Total assignments visible to the caller.
    /// TenantStaff: their tenant only (via global query filter).
    /// SuperAdmin: all tenants.
    /// </summary>
    int TotalAssignments,

    /// <summary>Assignments with Status == Completed.</summary>
    int CompletedAssignments,

    /// <summary>Assignments with Status == Pending or InProgress.</summary>
    int ActiveAssignments,

    /// <summary>
    /// Number of non-deleted tenants.
    /// Always 0 for TenantStaff (they are scoped to exactly one tenant).
    /// SuperAdmin receives the real count.
    /// </summary>
    int ActiveTenantsCount,

    /// <summary>
    /// Assignment counts grouped by calendar month for the trailing 12 months,
    /// ordered oldest → newest.  Used to render the activity trend chart.
    /// </summary>
    List<MonthlyCountDto> AssignmentsByMonth,

    /// <summary>
    /// Assignment counts by status for the pie / donut chart.
    /// Keys are the enum string names (e.g. "Pending", "Completed").
    /// </summary>
    Dictionary<string, int> AssignmentsByStatus);

/// <summary>Aggregated assignment count for one calendar month.</summary>
public record MonthlyCountDto(
    /// <summary>ISO-8601 year-month string, e.g. "2026-01".</summary>
    string Month,
    int    Count);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetDashboardStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<DashboardStatsDto> Handle(
        GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        // ── Tests (global — not tenant-scoped) ────────────────────────────────
        //
        // Tests have no TenantId; the global query filter only removes soft-deleted
        // rows.  Both TenantStaff and SuperAdmin see the same published count.
        var totalTests = await _context.Tests
            .AsNoTracking()
            .CountAsync(t => t.IsPublished, cancellationToken);

        // ── Assignments ───────────────────────────────────────────────────────
        //
        // The global query filter on Assignment automatically scopes TenantStaff
        // to their own tenant.  SuperAdmin (TenantId == null) sees all rows.
        var assignmentQuery = _context.Assignments.AsNoTracking();

        var totalAssignments = await assignmentQuery.CountAsync(cancellationToken);

        var completedAssignments = await assignmentQuery
            .CountAsync(a => a.Status == AssignmentStatus.Completed, cancellationToken);

        var activeAssignments = await assignmentQuery
            .CountAsync(
                a => a.Status == AssignmentStatus.Pending
                  || a.Status == AssignmentStatus.InProgress,
                cancellationToken);

        // ── Active tenants (SuperAdmin only) ──────────────────────────────────
        //
        // Tenants have no TenantId filter; the global filter only removes
        // soft-deleted rows.  For TenantStaff we return 0 — they are not
        // authorised to see cross-tenant data.
        var activeTenantsCount = _currentUser.TenantId is null
            ? await _context.Tenants.AsNoTracking().CountAsync(cancellationToken)
            : 0;

        // ── Assignments by month (trailing 12 months) ─────────────────────────
        //
        // We pull only the Created timestamps to avoid large row transfers.
        // Grouping is done in memory because EF Core / Npgsql cannot translate
        // DateTimeOffset.ToString("yyyy-MM") into a stable SQL projection on all
        // versions — and the data set is small (at most ~12 distinct values after
        // in-memory grouping).
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-11).Date; // start of 12-month window

        var createdDates = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.Created >= cutoff)
            .Select(a => a.Created)
            .ToListAsync(cancellationToken);

        var assignmentsByMonth = createdDates
            .GroupBy(d => $"{d.Year:D4}-{d.Month:D2}")
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyCountDto(g.Key, g.Count()))
            .ToList();

        // ── Assignments by status ─────────────────────────────────────────────
        var statusCounts = await _context.Assignments
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var assignmentsByStatus = statusCounts.ToDictionary(
            s => s.Status.ToString(),
            s => s.Count);

        return new DashboardStatsDto(
            totalTests,
            totalAssignments,
            completedAssignments,
            activeAssignments,
            activeTenantsCount,
            assignmentsByMonth,
            assignmentsByStatus);
    }
}
