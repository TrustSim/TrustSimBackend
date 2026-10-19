using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command xác nhận cập nhật thông tin định danh vào UserProfile sau khi đối soát dữ liệu eKYC.
/// Toàn bộ thông tin định danh được trích xuất an toàn từ OcrRawData đã lưu ở Pha 1 trên backend.
/// </summary>
public record ConfirmEkycCommand(
    Guid VerificationId,
    Guid UserId = default
) : IRequest<UserProfileDto>;
