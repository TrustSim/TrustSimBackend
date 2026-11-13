namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Thông tin chứng thư số thuê bao trả về từ cổng VNPT SmartCA (API v1/credentials/get_certificate).
/// </summary>
public record SmartCaCertificateDto(
    string CertId,
    string SerialNumber,
    string ServiceType,
    string ServiceName,
    string CertStatus,
    string CertSubject,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string CertDataBase64,
    string? CaCertBase64,
    string? RootCertBase64,
    string? TransactionId = null);

/// <summary>
/// Yêu cầu khởi tạo giao dịch ký số băm (SHA-256 Hash) qua cổng VNPT SmartCA (API v1/signatures/sign hoặc v2/signatures/sign).
/// </summary>
public record SmartCaSignHashRequestDto(
    string UserIdCitizenId,
    string SerialNumber,
    string TransactionId,
    string TransactionDesc,
    string DocId,
    string DataToBeSignedHex,
    string FileType = "pdf",
    string SignType = "hash",
    string? UserPassword = null,
    string? Otp = null);

/// <summary>
/// Kết quả khởi tạo giao dịch ký số trên VNPT SmartCA.
/// </summary>
public record SmartCaSignInitiatedResultDto(
    bool IsSuccess,
    int StatusCode,
    string Message,
    string TransactionId,
    string? TranCode,
    string? Sad = null,
    int? ExpiredIn = null);

/// <summary>
/// Thông tin chữ ký số của từng tài liệu trả về từ VNPT SmartCA.
/// </summary>
public record SmartCaSignatureItemDto(
    string DocId,
    string? SignatureValueBase64,
    string? TimestampSignatureBase64);

/// <summary>
/// Kết quả kiểm tra trạng thái hoặc xác nhận giao dịch ký số từ VNPT SmartCA.
/// </summary>
public record SmartCaSignStatusResultDto(
    bool IsCompleted,
    int StatusCode,
    string Message,
    string TransactionId,
    IReadOnlyList<SmartCaSignatureItemDto> Signatures);

/// <summary>
/// Dịch vụ tích hợp hệ thống ký số từ xa VNPT SmartCA theo Quyết định 769/QĐ-BTTTT (Phiên bản 4.0).
/// </summary>
public interface ISmartCaService
{
    /// <summary>
    /// Lấy danh sách chứng thư số đang hoạt động của thuê bao theo số CCCD/CMND/Hộ chiếu/MST (v1/credentials/get_certificate).
    /// </summary>
    Task<IReadOnlyList<SmartCaCertificateDto>> GetUserCertificatesAsync(
        string citizenIdOrTaxCode,
        string? serialNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi yêu cầu ký số mã băm (SHA-256 Hash) tới VNPT SmartCA (v1/signatures/sign hoặc v2/signatures/sign nếu có OTP).
    /// </summary>
    Task<SmartCaSignInitiatedResultDto> InitiateSignHashAsync(
        SmartCaSignHashRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chủ động kiểm tra trạng thái giao dịch ký số theo tranCode (v1/signatures/sign/{tranId}/status).
    /// </summary>
    Task<SmartCaSignStatusResultDto> GetSignTransactionStatusAsync(
        string tranCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác nhận giao dịch ký số tích hợp SmartCA TH bằng chuỗi SAD (v2/signatures/confirm).
    /// </summary>
    Task<SmartCaSignStatusResultDto> ConfirmIntegratedSignAsync(
        string citizenIdOrTaxCode,
        string userPassword,
        string transactionId,
        string sad,
        CancellationToken cancellationToken = default);
}
