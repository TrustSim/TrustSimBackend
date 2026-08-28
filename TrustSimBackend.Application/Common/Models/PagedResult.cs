namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Chuẩn hóa cấu trúc kết quả cho các truy vấn phân trang (Pagination).
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của từng phần tử trong danh sách</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int count, int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = count;
        Items = items;
    }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
    {
        return new PagedResult<T>(items, totalCount, pageIndex, pageSize);
    }
}
