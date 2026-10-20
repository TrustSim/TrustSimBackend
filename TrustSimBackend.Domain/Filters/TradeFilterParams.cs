using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc truy vấn danh sách giao dịch mua bán SIM của cá nhân.
/// </summary>
public class MyTradeQueryFilter : BaseQueryFilter<TradeTransactionSortBy>
{
    /// <summary>
    /// Lọc theo trạng thái giao dịch chuyển nhượng SIM.
    /// </summary>
    public TradeTransactionStatus? Status { get; set; }

    /// <summary>
    /// Vai trò tham gia: "buyer" (người mua), "seller" (người bán), hoặc null/all (toàn bộ).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Giá trị giao dịch tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Giá trị giao dịch tối đa (VNĐ).
    /// </summary>
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Bộ lọc truy vấn danh sách giao dịch chuyển nhượng SIM toàn sàn dành cho Quản trị viên.
/// </summary>
public class AdminTradeQueryFilter : BaseQueryFilter<TradeTransactionSortBy>
{
    /// <summary>
    /// Lọc theo trạng thái giao dịch chuyển nhượng SIM.
    /// </summary>
    public TradeTransactionStatus? Status { get; set; }

    /// <summary>
    /// Lọc theo ID người mua.
    /// </summary>
    public Guid? BuyerId { get; set; }

    /// <summary>
    /// Lọc theo ID người bán.
    /// </summary>
    public Guid? SellerId { get; set; }

    /// <summary>
    /// Giá trị giao dịch tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Giá trị giao dịch tối đa (VNĐ).
    /// </summary>
    public decimal? MaxAmount { get; set; }
}
