using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command xác thực mã OTP email để hoàn tất đăng ký tài khoản (Bước 2: tạo tài khoản trong DB).
/// </summary>
public record VerifyEmailCommand(
    string Email,
    string OtpCode
) : IRequest<AuthResponseDto>;
