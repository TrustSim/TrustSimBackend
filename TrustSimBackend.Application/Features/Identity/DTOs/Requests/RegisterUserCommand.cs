using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu đăng ký tài khoản người dùng mới (Bước 1: gửi thông tin, lưu tạm và gửi Email OTP).
/// </summary>
public record RegisterUserCommand(
    string UserName,
    string Email,
    string Password
) : IRequest<RegisterInitiatedDto>;

