using MediatR;
using Microsoft.AspNetCore.Identity;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý yêu cầu quên mật khẩu:
/// 1. Kiểm tra tài khoản tồn tại qua UserManager.
/// 2. Sinh mã OTP bảo mật và gửi email xác thực qua OtpService.
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, string>
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly IOtpService _otpService;

    public ForgotPasswordCommandHandler(
        UserManager<UserAccount> userManager,
        IOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<string> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user == null || !user.IsActive)
        {
            throw new BadRequestException("Không tìm thấy tài khoản tương ứng với Email này hoặc tài khoản đã bị khóa.");
        }

        // Sinh mã OTP và gửi qua Email với mục đích ForgotPassword
        var sendResult = await _otpService.GenerateAndSendEmailOtpAsync(
            normalizedEmail,
            "ForgotPassword",
            expiryMinutes: 5,
            cancellationToken: cancellationToken);

        if (!sendResult.IsSuccess)
        {
            throw new BadRequestException(sendResult.Message);
        }

        return sendResult.Message;
    }
}
