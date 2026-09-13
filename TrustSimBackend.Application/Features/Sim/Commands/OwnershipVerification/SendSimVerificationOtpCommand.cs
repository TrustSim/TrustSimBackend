using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.OwnershipVerification;

/// <summary>
/// Command gửi mã OTP xác thực qua SMS tới số SIM để chứng minh người dùng đang cầm giữ SIM vật lý (Pha 1 - Proof of Possession).
/// </summary>
public record SendSimVerificationOtpCommand(string PhoneNumber, Guid UserId) : IRequest<SendOtpResponseDto>;

public class SendSimVerificationOtpCommandHandler : IRequestHandler<SendSimVerificationOtpCommand, SendOtpResponseDto>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ISimOwnershipRepository _simOwnershipRepository;
    private readonly IPhonePrefixRepository _phonePrefixRepository;
    private readonly IOtpService _otpService;

    public SendSimVerificationOtpCommandHandler(
        IUserAccountRepository userAccountRepository,
        ISimOwnershipRepository simOwnershipRepository,
        IPhonePrefixRepository phonePrefixRepository,
        IOtpService otpService)
    {
        _userAccountRepository = userAccountRepository;
        _simOwnershipRepository = simOwnershipRepository;
        _phonePrefixRepository = phonePrefixRepository;
        _otpService = otpService;
    }

    public async Task<SendOtpResponseDto> Handle(SendSimVerificationOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = NormalizePhone(request.PhoneNumber);

        // 1. Kiểm tra UserAccount và điều kiện KYC Profile
        var user = await _userAccountRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản người dùng không tồn tại.");

        if (user.UserProfile == null || user.UserProfile.VerificationStatus != VerificationStatus.VERIFIED)
        {
            throw new UserProfileNotVerifiedException();
        }

        // 2. Tra cứu nhà mạng tương ứng với đầu số trong Database
        var prefix = await MatchPhonePrefixAsync(normalizedPhone, cancellationToken)
            ?? throw new UnsupportedCarrierException(normalizedPhone[..Math.Min(4, normalizedPhone.Length)]);

        // 3. Kiểm tra số SIM có đang bị sở hữu ACTIVE bởi người khác không
        var isOwnedByOther = await _simOwnershipRepository.HasActiveOwnershipAsync(normalizedPhone, excludeUserId: request.UserId, cancellationToken);
        if (isOwnedByOther)
        {
            throw new SimAlreadyOwnedException(normalizedPhone);
        }

        // 4. Sinh & Gửi mã OTP qua SMS (Proof of Possession)
        return await _otpService.GenerateAndSendPhoneOtpAsync(normalizedPhone, OtpPurpose.SimOwnershipVerification, cancellationToken);
    }

    private async Task<Domain.Entities.Sim.PhonePrefix?> MatchPhonePrefixAsync(string normalizedPhone, CancellationToken ct)
    {
        var allPrefixes = await _phonePrefixRepository.GetAllAsync(null, ct);
        return allPrefixes
            .OrderByDescending(p => p.PrefixValue.Length)
            .FirstOrDefault(p => normalizedPhone.StartsWith(p.PrefixValue) 
                              || (normalizedPhone.StartsWith("84") && normalizedPhone[2..].StartsWith(p.PrefixValue.TrimStart('0'))));
    }

    private static string NormalizePhone(string phone)
    {
        var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
        if (clean.StartsWith("+84")) clean = "0" + clean[3..];
        else if (clean.StartsWith("84")) clean = "0" + clean[2..];
        return clean;
    }
}
