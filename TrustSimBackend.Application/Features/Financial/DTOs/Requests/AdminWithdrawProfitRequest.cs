namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO Request cho yêu cầu Admin rút tiền lợi nhuận sàn về tài khoản cá nhân.
/// </summary>
public class AdminWithdrawProfitRequest
{
    /// <summary>Số tiền muốn rút (VNĐ, tối thiểu 50.000 VNĐ, không vượt quá lợi nhuận khả dụng)</summary>
    public decimal Amount { get; set; }

    /// <summary>Mã ngân hàng nhận tiền (VD: VCB, MB, TCB, STB...)</summary>
    public string DestinationBankCode { get; set; } = string.Empty;

    /// <summary>Tên ngân hàng nhận tiền</summary>
    public string DestinationBankName { get; set; } = string.Empty;

    /// <summary>Số tài khoản ngân hàng cá nhân của Admin</summary>
    public string DestinationAccountNumber { get; set; } = string.Empty;

    /// <summary>Tên chủ tài khoản ngân hàng cá nhân của Admin</summary>
    public string DestinationAccountHolderName { get; set; } = string.Empty;

    /// <summary>Ghi chú lý do rút tiền (tùy chọn)</summary>
    public string? Note { get; set; }

    /// <summary>Khóa chống lặp giao dịch (tùy chọn)</summary>
    public string? IdempotencyKey { get; set; }
}
