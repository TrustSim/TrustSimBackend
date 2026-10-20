namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Tham số phân trang chuẩn hóa và an toàn chống DoS/OOM (pageIndex >= 1, pageSize clamped 1..100).
/// </summary>
public class PaginationParams
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _pageIndex = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// Số thứ tự trang hiện tại (Bắt đầu từ 1).
    /// </summary>
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }

    /// <summary>
    /// Số lượng bản ghi trên một trang (Từ 1 đến 100, Mặc định 20).
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? DefaultPageSize : value);
    }
}
