using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler kiểm tra tính hợp lệ của mã OTP.
/// </summary>
public class VerifyPhoneOtpCommandHandler : IRequestHandler<VerifyPhoneOtpCommand, VerifyOtpResponseDto>
{
    private readonly IOtpService _otpService;

    public VerifyPhoneOtpCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public Task<VerifyOtpResponseDto> Handle(VerifyPhoneOtpCommand request, CancellationToken cancellationToken)
    {
        return _otpService.VerifyPhoneOtpAsync(request.PhoneNumber, request.OtpCode, request.Purpose, consumeOnSuccess: true);
    }
}
