using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý việc sinh mã OTP và gửi qua SpeedSMS.
/// </summary>
public class SendPhoneOtpCommandHandler : IRequestHandler<SendPhoneOtpCommand, SendOtpResponseDto>
{
    private readonly IOtpService _otpService;

    public SendPhoneOtpCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public async Task<SendOtpResponseDto> Handle(SendPhoneOtpCommand request, CancellationToken cancellationToken)
    {
        return await _otpService.GenerateAndSendPhoneOtpAsync(request.PhoneNumber, request.Purpose, cancellationToken);
    }
}
