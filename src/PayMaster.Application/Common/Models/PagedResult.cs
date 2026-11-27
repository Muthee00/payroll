using Microsoft.EntityFrameworkCore;

namespace PayMaster.Application.Common.Models;

/// <summary>Standard paging/sorting/search input used by every list endpoint.</summary>
public record PageQuery
{
    public const int MaxPageSize = 200;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);

    public static PagedResult<T> Empty(int pageSize = 20) => new([], 0, 1, pageSize);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageQuery page, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page.SafePage - 1) * page.SafePageSize)
            .Take(page.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, total, page.SafePage, page.SafePageSize);
    }
}
