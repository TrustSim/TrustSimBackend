using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// DTO dòng lịch sử biến động doanh thu sàn (hoa hồng, phạt cọc, rút lợi nhuận).
/// </summary>
public class PlatformRevenueHistoryDto
{
    /// <summary>ID chứng từ sổ cái</summary>
    public Guid JournalId { get; set; }

    /// <summary>Mã chứng từ sổ cái (VD: JRN-20261002-123456)</summary>
    public string JournalCode { get; set; } = string.Empty;

    /// <summary>Loại nghiệp vụ tài chính</summary>
    public LedgerTransactionType TransactionType { get; set; }

    /// <summary>Tên hiển thị loại nghiệp vụ</summary>
    public string TransactionTypeName { get; set; } = string.Empty;

    /// <summary>Số tiền biến động (VNĐ) (> 0: Tăng doanh thu, < 0: Admin rút lợi nhuận)</summary>
    public decimal Amount { get; set; }

    /// <summary>Diễn giải chi tiết bút toán</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Loại đối tượng tham chiếu (Escrow, AuctionDeposit, AdminProfitWithdraw...)</summary>
    public string? ReferenceType { get; set; }

    /// <summary>ID đối tượng tham chiếu</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Mã băm chữ ký chứng từ</summary>
    public string? SignedHash { get; set; }

    /// <summary>Thời điểm ghi nhận chứng từ (UTC)</summary>
    public DateTime PostedAt { get; set; }
}
