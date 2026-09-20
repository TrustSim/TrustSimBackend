using MediatR;
using Microsoft.AspNetCore.Identity;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ đặt lại mật khẩu mới:
/// 1. Xác thực mã OTP qua OtpService.
/// 2. Đặt lại mật khẩu mới cho tài khoản qua ASP.NET Core Identity UserManager.
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly IOtpService _otpService;

    public ResetPasswordCommandHandler(
        UserManager<UserAccount> userManager,
        IOtpService otpService)
    {
        _userManager = userManager;
        _otpService = otpService;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Xác thực mã OTP quên mật khẩu
        var otpResult = await _otpService.VerifyEmailOtpAsync(
            normalizedEmail,
            request.ResetCode.Trim(),
            "ForgotPassword",
            consumeOnSuccess: true);

        if (!otpResult.IsValid)
        {
            throw new BadRequestException(otpResult.Message ?? "Mã xác thực OTP không hợp lệ hoặc đã hết hạn.");
        }

        // 2. Tìm tài khoản người dùng
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || !user.IsActive)
        {
            throw new NotFoundException($"Không tìm thấy tài khoản tương ứng với Email '{normalizedEmail}'.");
        }

        // 3. Đặt lại mật khẩu qua Identity
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new BadRequestException($"Đặt lại mật khẩu không thành công: {errors}");
        }

        return true;
    }
}
