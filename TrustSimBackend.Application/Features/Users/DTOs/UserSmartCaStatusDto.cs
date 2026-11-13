namespace TrustSimBackend.Application.Features.Users.DTOs;

/// <summary>
/// DTO chứa thông tin trạng thái chứng thư số VNPT SmartCA của người dùng.
/// </summary>
public record UserSmartCaStatusDto(
    bool IsEkycVerified,
    string? CitizenId,
    string? FullName,
    bool HasSmartCaCertificate,
    SmartCaActiveCertificateDto? ActiveCertificate = null);

/// <summary>
/// Chi tiết chứng thư số đang hoạt động của người dùng trên VNPT SmartCA.
/// </summary>
public record SmartCaActiveCertificateDto(
    string SerialNumber,
    string ServiceName,
    string CertStatus,
    DateTime? ValidFrom,
    DateTime? ValidTo);
