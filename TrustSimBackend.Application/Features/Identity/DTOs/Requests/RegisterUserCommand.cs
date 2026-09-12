using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command yêu cầu đăng ký tài khoản người dùng mới (bắt buộc CCCD/CMND).
/// </summary>
public record RegisterUserCommand(
    string UserName,
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string IdentityNumber,
    DateTime? DateOfBirth = null,
    string? Address = null
) : IRequest<AuthResponseDto>;
