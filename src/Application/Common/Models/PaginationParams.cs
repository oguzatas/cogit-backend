namespace backend.Application.Common.Models;

/// <summary>
/// Standard query-string pagination parameters.
/// PageSize is clamped server-side to prevent runaway DB queries.
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    /// <summary>1-based page index. Defaults to 1.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Number of records per page. Defaults to 10.
    /// Values above 100 are silently clamped to 100.
    /// Values below 1 are silently clamped to 1.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }
}
