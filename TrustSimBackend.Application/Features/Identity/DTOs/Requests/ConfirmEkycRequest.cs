namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// DTO gửi ý định xác nhận thông tin định danh eKYC từ người dùng (Pha 2).
/// Chỉ chứa duy nhất khóa VerificationId, hoàn toàn không chứa bất kỳ trường dữ liệu cá nhân nào
/// để triệt tiêu 100% lỗ hổng tự ý can thiệp hoặc chỉnh sửa dữ liệu client-side.
/// </summary>
public class ConfirmEkycRequest
{
    /// <summary>Khóa định danh (VerificationId) của hồ sơ eKYC từ kết quả phân tích AI Pha 1.</summary>
    public Guid VerificationId { get; set; }
}
