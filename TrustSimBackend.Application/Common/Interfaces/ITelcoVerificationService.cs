namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng dịch vụ kết nối cổng viễn thông nhà mạng (Viettel, VinaPhone, MobiFone...) để tra cứu và xác thực thông tin thuê bao chính chủ.
/// </summary>
public interface ITelcoVerificationService
{
    /// <summary>
    /// Đối soát thông tin chủ sở hữu số SIM với cơ sở dữ liệu nhà mạng viễn thông.
    /// </summary>
    /// <param name="carrierCode">Mã nhà mạng (VTT, VNP, MBF...).</param>
    /// <param name="phoneNumber">Số điện thoại cần xác thực (định dạng chuẩn 84xxx hoặc 0xxx).</param>
    /// <param name="identityNumber">Số CMND / CCCD / Hộ chiếu của người dùng đã KYC.</param>
    /// <param name="fullName">Họ và tên đầy đủ của người dùng đã KYC.</param>
    /// <param name="cancellationToken">Token hủy tác vụ.</param>
    /// <returns>Kết quả đối soát thông tin từ cổng viễn thông.</returns>
    Task<TelcoVerificationResult> VerifyOwnershipAsync(
        string carrierCode,
        string phoneNumber,
        string identityNumber,
        string fullName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO chứa kết quả phản hồi từ cổng viễn thông nhà mạng.
/// </summary>
public class TelcoVerificationResult
{
    public bool IsSuccess { get; set; }
    public string CarrierCode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? MatchedIdentityNumber { get; set; }
    public string? MatchedFullName { get; set; }
    public string? CarrierTransactionId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? RegisteredDate { get; set; }

    public static TelcoVerificationResult Success(
        string carrierCode,
        string phoneNumber,
        string identityNumber,
        string fullName,
        string transactionId) => new()
    {
        IsSuccess = true,
        CarrierCode = carrierCode,
        PhoneNumber = phoneNumber,
        MatchedIdentityNumber = identityNumber,
        MatchedFullName = fullName,
        CarrierTransactionId = transactionId,
        RegisteredDate = DateTime.UtcNow.AddMonths(-12)
    };

    public static TelcoVerificationResult Failure(
        string carrierCode,
        string phoneNumber,
        string errorMessage) => new()
    {
        IsSuccess = false,
        CarrierCode = carrierCode,
        PhoneNumber = phoneNumber,
        ErrorMessage = errorMessage
    };
}
