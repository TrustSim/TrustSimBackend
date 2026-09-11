using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// DTO tiếp nhận dữ liệu tải lên ảnh eKYC từ Client.
/// </summary>
public class SubmitEkycRequest
{
    /// <summary>Tệp ảnh mặt trước CMND / CCCD / Hộ chiếu.</summary>
    public IFormFile FrontCardImage { get; set; } = null!;

    /// <summary>Tệp ảnh mặt sau CMND / CCCD (tùy chọn với Hộ chiếu).</summary>
    public IFormFile? BackCardImage { get; set; }

    /// <summary>Tệp ảnh chụp chân dung selfie trực tiếp của người dùng.</summary>
    public IFormFile SelfieImage { get; set; } = null!;
}
