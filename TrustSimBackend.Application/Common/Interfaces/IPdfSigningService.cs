namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Kết quả niêm phong số PAdES X.509 trên file PDF.
/// </summary>
public record PadesSignResult(
    byte[] SignedPdfBytes,
    string Sha256Hash,
    string CertificateSerial,
    DateTime SignedAt,
    DateTime? TsaTimestamp,
    string? TsaAuthority);

/// <summary>
/// Interface dịch vụ ký số PAdES niêm phong hợp đồng số bằng Chứng thư số Sàn và RFC 3161 TSA.
/// </summary>
public interface IPdfSigningService
{
    /// <summary>
    /// Ký số PAdES-B-LTA Detached Signature lên tài liệu PDF, tích hợp dấu thời gian RFC 3161.
    /// </summary>
    Task<PadesSignResult> SignPdfPadesAsync(
        byte[] sourcePdfBytes,
        string signatureReason,
        string signatureLocation,
        IReadOnlyList<SignatureStampBoxDto> stampBoxes,
        CancellationToken cancellationToken = default);
}
