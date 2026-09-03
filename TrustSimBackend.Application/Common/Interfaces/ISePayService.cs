namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Service Interface xử lý tích hợp cổng thanh toán SePay (VietQR & Webhook).
/// </summary>
public interface ISePayService
{
    /// <summary>
    /// Sinh URL hình ảnh mã VietQR SePay theo thông số giao dịch.
    /// </summary>
    string GeneratePaymentQrUrl(decimal amount, string transactionCode);

    /// <summary>
    /// Kiểm tra tính hợp lệ của Webhook Token / API Key gửi từ SePay.
    /// </summary>
    bool ValidateWebhookToken(string? authorizationHeader);

    /// <summary>
    /// Trích xuất mã giao dịch (VD: "TS123456") từ chuỗi nội dung chuyển khoản của ngân hàng.
    /// </summary>
    string? ExtractTransactionCode(string content);

    /// <summary>
    /// Sinh mã giao dịch nạp tiền ngẫu nhiên không trùng lặp theo prefix cấu hình.
    /// </summary>
    string GenerateTransactionCode();
}
