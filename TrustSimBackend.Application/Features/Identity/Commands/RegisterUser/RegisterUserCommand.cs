using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs;

namespace TrustSimBackend.Application.Features.Identity.Commands.RegisterUser;

/// <summary>
/// Command yêu cầu đăng ký tài khoản người dùng mới (bắt buộc CCCD/CMND).
/// </summary>
public record RegisterUserCommand(
    string UserName,
    string Email,
    string Password,
    string FullName,
    string PhoneNumber,
    string IdentityNumber
) : IRequest<ApiResponse<AuthResponseDto>>;
