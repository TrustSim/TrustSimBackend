using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc truy vấn danh sách giao dịch nạp / rút tiền ví cá nhân.
/// </summary>
public class WalletTransactionQueryFilter : BaseQueryFilter<WalletTransactionSortBy>
{
    /// <summary>
    /// Loại giao dịch ví: TOPUP (nạp tiền) hoặc WITHDRAWAL (rút tiền).
    /// </summary>
    public WalletTransactionType? Type { get; set; }

    /// <summary>
    /// Trạng thái giao dịch ví (PENDING, SUCCESS, FAILED...).
    /// </summary>
    public TransactionStatus? Status { get; set; }

    /// <summary>
    /// Số tiền giao dịch tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Số tiền giao dịch tối đa (VNĐ).
    /// </summary>
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Bộ lọc truy vấn toàn bộ lịch sử biến động số dư toàn sàn dành cho Quản trị viên.
/// </summary>
public class AdminWalletTransactionsFilter : WalletTransactionQueryFilter
{
    /// <summary>
    /// Lọc theo người dùng cụ thể.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Lọc theo ví cụ thể.
    /// </summary>
    public Guid? WalletId { get; set; }
}

/// <summary>
/// Bộ lọc truy vấn sao kê tài chính tổng hợp đa nguồn (Nạp rút, Ký quỹ Escrow, Cọc đấu giá...).
/// </summary>
public class UnifiedStatementQueryFilter : BaseQueryFilter<WalletTransactionSortBy>
{
    /// <summary>
    /// Nguồn biến động: WALLET, AUCTION, AUCTION_DEPOSIT, ESCROW, COMPENSATION hoặc null (tất cả).
    /// </summary>
    public string? SourceType { get; set; }

    /// <summary>
    /// Hướng biến động dòng tiền: CREDIT (+), DEBIT (-), LOCK (tạm khóa).
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    /// Trạng thái giao dịch (PENDING, SUCCESS, FAILED...).
    /// </summary>
    public TransactionStatus? Status { get; set; }

    /// <summary>
    /// Phân loại nghiệp vụ chi tiết (TOP_UP, WITHDRAW, AUCTION_DEPOSIT_LOCK, ESCROW_HOLD...).
    /// </summary>
    public string? TransactionType { get; set; }

    /// <summary>
    /// Số tiền biến động tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Số tiền biến động tối đa (VNĐ).
    /// </summary>
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Bộ lọc truy vấn danh sách yêu cầu rút tiền toàn sàn dành cho Quản trị viên.
/// </summary>
public class AdminWithdrawalQueryFilter : BaseQueryFilter<WalletTransactionSortBy>
{
    /// <summary>
    /// Lọc theo người dùng cụ thể.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Lọc theo ví cụ thể.
    /// </summary>
    public Guid? WalletId { get; set; }

    /// <summary>
    /// Trạng thái yêu cầu rút tiền (PENDING, SUCCESS, FAILED...).
    /// </summary>
    public TransactionStatus? Status { get; set; }

    /// <summary>
    /// Số tiền rút tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Số tiền rút tối đa (VNĐ).
    /// </summary>
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Bộ lọc truy vấn danh sách ví người dùng dành cho Quản trị viên.
/// </summary>
public class AdminWalletQueryFilter : BaseQueryFilter<WalletSortBy>
{
    /// <summary>
    /// Trạng thái ví (ACTIVE, FROZEN...).
    /// </summary>
    public WalletStatus? Status { get; set; }

    /// <summary>
    /// Số dư khả dụng tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinBalance { get; set; }

    /// <summary>
    /// Số dư khả dụng tối đa (VNĐ).
    /// </summary>
    public decimal? MaxBalance { get; set; }
}

/// <summary>
/// Bộ lọc tra cứu danh sách chứng từ kế toán ghi kép (Ledger Journal).
/// </summary>
public class LedgerJournalQueryFilter : BaseQueryFilter<LedgerJournalSortBy>
{
    /// <summary>
    /// Loại nghiệp vụ kế toán (Nạp, Rút, Escrow, Phạt cọc, Doanh thu sàn...).
    /// </summary>
    public LedgerTransactionType? TransactionType { get; set; }

    /// <summary>
    /// Danh sách các loại nghiệp vụ kế toán cần lọc kết hợp.
    /// </summary>
    public IEnumerable<LedgerTransactionType>? TransactionTypes { get; set; }

    /// <summary>
    /// Giá trị chứng từ tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Giá trị chứng từ tối đa (VNĐ).
    /// </summary>
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Bộ lọc tra cứu lịch sử doanh thu sàn (Platform Revenue History).
/// </summary>
public class RevenueHistoryQueryFilter : BaseQueryFilter<LedgerJournalSortBy>
{
    /// <summary>
    /// Loại nghiệp vụ doanh thu sàn.
    /// </summary>
    public LedgerTransactionType? TransactionType { get; set; }
}
