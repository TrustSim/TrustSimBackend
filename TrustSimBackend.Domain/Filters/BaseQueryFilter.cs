using System;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Lớp cơ sở cung cấp phân trang, từ khóa tìm kiếm, sắp xếp theo Enum định kiểu mạnh và khoảng thời gian.
/// </summary>
public class BaseQueryFilter<TSortBy> : PaginationParams where TSortBy : struct, Enum
{
    private string? _keyword;

    /// <summary>
    /// Từ khóa tìm kiếm tự do (Tự động cắt tỉa khoảng trắng).
    /// </summary>
    public string? Keyword
    {
        get => _keyword;
        set => _keyword = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Tiêu chí sắp xếp định kiểu mạnh (Enum).
    /// </summary>
    public TSortBy? SortBy { get; set; }

    /// <summary>
    /// Chiều sắp xếp: 'Desc' (Giảm dần) hoặc 'Asc' (Tăng dần). Mặc định là 'Desc'.
    /// </summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Desc;

    /// <summary>
    /// Lọc dữ liệu từ ngày (UTC).
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Lọc dữ liệu đến ngày (UTC).
    /// </summary>
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Lớp cơ sở cung cấp phân trang, từ khóa tìm kiếm, sắp xếp theo chuỗi (string) và khoảng thời gian.
/// </summary>
public class BaseQueryFilter : PaginationParams
{
    private string? _keyword;

    /// <summary>
    /// Từ khóa tìm kiếm tự do (Tự động cắt tỉa khoảng trắng).
    /// </summary>
    public string? Keyword
    {
        get => _keyword;
        set => _keyword = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Tên trường cần sắp xếp (ví dụ: 'price', 'createdAt', 'startAt').
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Chiều sắp xếp: 'Desc' (Giảm dần) hoặc 'Asc' (Tăng dần). Mặc định là 'Desc'.
    /// </summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Desc;

    /// <summary>
    /// Lọc dữ liệu từ ngày (UTC).
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Lọc dữ liệu đến ngày (UTC).
    /// </summary>
    public DateTime? ToDate { get; set; }
}
