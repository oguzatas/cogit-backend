namespace backend.Application.Common.Models;

/// <summary>
/// Generic server-side pagination envelope returned by all paginated endpoints.
/// </summary>
public record PagedResult<T>
{
    /// <summary>The items for the current page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Total number of records matching the query (before pagination).</summary>
    public required int TotalRecords { get; init; }

    /// <summary>Current 1-based page index.</summary>
    public required int PageNumber { get; init; }

    /// <summary>Number of items per page.</summary>
    public required int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalRecords / (double)PageSize)
        : 0;

    /// <summary>Whether a next page exists.</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Whether a previous page exists.</summary>
    public bool HasPreviousPage => PageNumber > 1;
}
