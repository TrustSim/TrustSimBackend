using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý việc sinh mã OTP và gửi qua Email (dùng IEmailService / MailKit).
/// </summary>
public class SendEmailOtpCommandHandler : IRequestHandler<SendEmailOtpCommand, SendOtpResponseDto>
{
    private readonly IOtpService _otpService;

    public SendEmailOtpCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public async Task<SendOtpResponseDto> Handle(SendEmailOtpCommand request, CancellationToken cancellationToken)
    {
        return await _otpService.GenerateAndSendEmailOtpAsync(
            request.Email,
            request.Purpose,
            request.ExpiryMinutes,
            cancellationToken);
    }
}
