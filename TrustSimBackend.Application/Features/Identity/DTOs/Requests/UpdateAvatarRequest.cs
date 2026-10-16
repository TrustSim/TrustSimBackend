using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// DTO yêu cầu tải lên và cập nhật ảnh đại diện người dùng.
/// </summary>
public class UpdateAvatarRequest
{
    /// <summary>
    /// Tệp hình ảnh avatar mới (định dạng JPG, PNG, WEBP, tối đa 5MB).
    /// </summary>
    public IFormFile File { get; set; } = null!;
}
