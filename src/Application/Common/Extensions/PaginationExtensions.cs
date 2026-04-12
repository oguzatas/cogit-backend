using backend.Application.Common.Models;

namespace backend.Application.Common.Extensions;

/// <summary>
/// EF Core IQueryable extension for server-side pagination.
///
/// Placement note: lives in the Application layer (not Infrastructure) because
/// Application already references Microsoft.EntityFrameworkCore (see GlobalUsings.cs)
/// and all paginated query handlers are in Application. Moving it to Infrastructure
/// would invert the dependency direction (Application → Infrastructure is forbidden).
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Executes two database round-trips:
    ///   1. COUNT(*) to get the total matching records.
    ///   2. SKIP / TAKE to fetch the current page.
    ///
    /// The query must already have ORDER BY applied before calling this method
    /// to guarantee stable pagination across pages.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalRecords = await query.CountAsync(cancellationToken);

        var items = totalRecords == 0
            ? []
            : await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items        = items,
            TotalRecords = totalRecords,
            PageNumber   = pageNumber,
            PageSize     = pageSize
        };
    }
}
