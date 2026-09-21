namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ Sandbox mô phỏng Cổng chi hộ Ngân hàng (Mock Bank Payout Gateway).
/// Phục vụ kịch bản Rút tiền tự động 2 Pha (Lock -> Bank Payout Callback) mà không cần liên kết ngân hàng thật.
/// </summary>
public interface IMockBankPayoutGatewayService
{
    /// <summary>
    /// Giả lập gửi lệnh chi hộ sang ngân hàng và nhận kết quả tức thời (dùng cho luồng rút tiền thực tế trong Web App).
    /// </summary>
    Task<string> RequestInstantPayoutAsync(
        string bankCode,
        string accountNumber,
        string accountHolderName,
        decimal amount,
        string transactionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giả lập ngân hàng gửi Webhook Callback xác nhận kết quả chi trả tiền rút.
    /// </summary>
    /// <param name="transactionCode">Mã giao dịch rút tiền nội bộ (VD: "TS-WD-9812").</param>
    /// <param name="isSuccess">True: Chi tiền thành công (Pha 2A), False: Chi tiền thất bại (Pha 2B - tự động hoàn).</param>
    /// <param name="failureReason">Lý do thất bại nếu isSuccess = false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True nếu xử lý callback thành công.</returns>
    Task<bool> SimulateBankPayoutCallbackAsync(
        string transactionCode,
        bool isSuccess,
        string? failureReason = null,
        CancellationToken cancellationToken = default);
}
