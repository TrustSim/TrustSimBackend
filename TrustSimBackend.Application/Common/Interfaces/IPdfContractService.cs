namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// DTO chứa thông tin hộp dấu xác thực điện tử vẽ lên trang cuối của file PDF.
/// </summary>
public record SignatureStampBoxDto(
    string SignerRole,
    string FullName,
    string IdentityNumberMasked,
    DateTime SignedAt,
    string IpAddress,
    string AuthMethod,
    bool IsAutoEnforced = false);

/// <summary>
/// Interface dịch vụ sinh nội dung hợp đồng chuyển nhượng SIM (HTML Preview và mảng byte PDF).
/// </summary>
public interface IPdfContractService
{
    /// <summary>
    /// Sinh chuỗi HTML xem trước hợp đồng (Preview) phục vụ hiển thị trên giao diện Web.
    /// </summary>
    Task<string> GetContractPreviewHtmlAsync(Guid tradeId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Sinh mảng byte PDF nguyên bản từ template hợp đồng 3 bên.
    /// </summary>
    Task<byte[]> GenerateRawPdfBytesAsync(Guid tradeId, bool isAutoEnforced, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách các hộp dấu xác thực điện tử của các bên tham gia để vẽ lên PDF.
    /// </summary>
    Task<IReadOnlyList<SignatureStampBoxDto>> GetStampBoxesAsync(Guid tradeId, bool isAutoEnforced, CancellationToken ct = default);
}
