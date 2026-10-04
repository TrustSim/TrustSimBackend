namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// DTO Kết quả sau khi Admin thực hiện rút tiền lợi nhuận sàn thành công.
/// </summary>
public class AdminProfitWithdrawResultDto
{
    /// <summary>ID chứng từ sổ cái kế toán kép</summary>
    public Guid JournalId { get; set; }

    /// <summary>Mã chứng từ sổ cái (VD: JRN-20261002-123456)</summary>
    public string JournalCode { get; set; } = string.Empty;

    /// <summary>Mã giao dịch rút tiền nội bộ</summary>
    public string TransactionCode { get; set; } = string.Empty;

    /// <summary>Số tiền đã rút (VNĐ)</summary>
    public decimal Amount { get; set; }

    /// <summary>Tên ngân hàng nhận tiền</summary>
    public string DestinationBankName { get; set; } = string.Empty;

    /// <summary>Số tài khoản ngân hàng nhận tiền (đã che bớt số bảo mật)</summary>
    public string DestinationAccountNumberMasked { get; set; } = string.Empty;

    /// <summary>Tên chủ tài khoản nhận tiền</summary>
    public string DestinationAccountHolderName { get; set; } = string.Empty;

    /// <summary>Mã tham chiếu chuyển tiền ngân hàng</summary>
    public string ExternalPayoutRef { get; set; } = string.Empty;

    /// <summary>Trạng thái giao dịch (COMPLETED)</summary>
    public string Status { get; set; } = "COMPLETED";

    /// <summary>Số dư lợi nhuận sàn còn lại sau khi rút (VNĐ)</summary>
    public decimal RemainingAvailableProfit { get; set; }

    /// <summary>Thời điểm hoàn tất giao dịch (UTC)</summary>
    public DateTime CompletedAt { get; set; }
}
