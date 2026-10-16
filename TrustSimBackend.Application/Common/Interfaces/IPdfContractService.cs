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

    /// <summary>
    /// Sinh chuỗi HTML xem trước Biên bản nghiệm thu và thanh lý hợp đồng (Văn bản 3).
    /// </summary>
    Task<string> GetLiquidationPreviewHtmlAsync(Guid tradeId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Sinh file PDF Biên bản nghiệm thu và thanh lý hợp đồng chuyển nhượng SIM (Văn bản 3).
    /// </summary>
    Task<byte[]> GenerateLiquidationMinutePdfAsync(Guid tradeId, CancellationToken ct = default);

    /// <summary>
    /// Sinh file PDF Phiếu xác nhận kết quả đấu giá và chốt giá trúng điện tử (Văn bản 1).
    /// </summary>
    Task<byte[]> GenerateWinningNoticePdfAsync(Guid tradeId, CancellationToken ct = default);

    /// <summary>
    /// Đảm bảo Phiếu xác nhận kết quả đấu giá (Văn bản 1) đã được sinh và tải lên Supabase Storage CDN.
    /// Nếu đã có, trả về ngay URL hiện tại. Nếu chưa, tự động tạo PDF, upload lên Supabase và lưu URL vào database.
    /// </summary>
    Task<string> EnsureWinningNoticeUploadedAsync(Guid tradeId, CancellationToken ct = default);
}
