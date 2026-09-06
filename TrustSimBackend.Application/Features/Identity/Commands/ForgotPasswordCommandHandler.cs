using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý yêu cầu quên mật khẩu (Sinh mã OTP và gửi email xác thực).
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, string>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(
        IUserAccountRepository userAccountRepository,
        IEmailService emailService)
    {
        _userAccountRepository = userAccountRepository;
        _emailService = emailService;
    }

    public async Task<string> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userAccountRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new BadRequestException("Không tìm thấy tài khoản tương ứng với Email này.");
        }

        var resetCode = Random.Shared.Next(100000, 999999).ToString();

        // Gửi email chứa mã OTP xác thực đặt lại mật khẩu
        await _emailService.SendOtpEmailAsync(
            normalizedEmail,
            resetCode,
            "đặt lại mật khẩu",
            expiryMinutes: 5,
            cancellationToken: cancellationToken);

        return resetCode;
    }
}
