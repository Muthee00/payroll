namespace PayMaster.Web.ViewModels;

/// <summary>Input for the shared _Pager partial. Keeps current query-string filters when paging.</summary>
public record PagerModel(int Page, int TotalPages, int FirstItem, int LastItem, int TotalCount, string ItemLabel = "records")
{
    public static PagerModel From<T>(PayMaster.Application.Common.Models.PagedResult<T> result, string itemLabel = "records") =>
        new(result.Page, result.TotalPages, result.FirstItem, result.LastItem, result.TotalCount, itemLabel);
}
