using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class VerifySimOwnershipCommandHandler : IRequestHandler<VerifySimOwnershipCommand, SimOwnershipVerificationResultDto>
{
    private readonly IOtpService _otpService;
    private readonly ITelcoVerificationService _telcoService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VerifySimOwnershipCommandHandler> _logger;

    public VerifySimOwnershipCommandHandler(
        IOtpService otpService,
        ITelcoVerificationService telcoService,
        IUnitOfWork unitOfWork,
        ILogger<VerifySimOwnershipCommandHandler> logger)
    {
        _otpService = otpService;
        _telcoService = telcoService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SimOwnershipVerificationResultDto> Handle(VerifySimOwnershipCommand request, CancellationToken cancellationToken)
    {
        var normalizedPhone = NormalizePhone(request.PhoneNumber);

        // 1. Kiểm tra UserAccount và điều kiện KYC
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Tài khoản người dùng không tồn tại.");

        if (user.UserProfile == null || user.UserProfile.VerificationStatus != VerificationStatus.VERIFIED)
        {
            throw new UserProfileNotVerifiedException();
        }

        // 2. Tìm PhonePrefix & SimCarrier trong Database
        var prefix = await MatchPhonePrefixAsync(normalizedPhone, cancellationToken)
            ?? throw new UnsupportedCarrierException(normalizedPhone[..Math.Min(4, normalizedPhone.Length)]);

        var carrier = prefix.SimCarrier
            ?? throw new SimCarrierNotFoundException(prefix.SimCarrierId.ToString());

        // 3. Lấy hoặc Tạo mới bản ghi SimNumber trong kho (UNVERIFIED, Price = 0, FormatRhythmId = null)
        var simNumber = await _unitOfWork.SimNumbers.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
        if (simNumber == null)
        {
            simNumber = new SimNumber
            {
                Id = Guid.NewGuid(),
                PhoneNumber = normalizedPhone,
                PhonePrefixId = prefix.Id,
                FormatRhythmId = null,
                Price = 0m,
                SimStatus = SimStatus.UNVERIFIED,
                IsActive = true
            };
            await _unitOfWork.SimNumbers.AddAsync(simNumber, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 4. Kiểm tra quyền sở hữu hiện tại (Chống chiếm đoạt SIM đang ACTIVE của người khác)
        var existingActiveOwnership = await _unitOfWork.SimOwnerships.GetActiveOwnershipBySimNumberIdAsync(simNumber.Id, cancellationToken);
        if (existingActiveOwnership != null && existingActiveOwnership.UserAccountId != request.UserId)
        {
            throw new SimAlreadyOwnedException(normalizedPhone);
        }

        var existingUserOwnership = await _unitOfWork.SimOwnerships.GetLatestOwnershipByUserAndSimNumberIdAsync(request.UserId, simNumber.Id, cancellationToken);

        // 5. Xác thực mã OTP qua OtpService (Pha 1 - Proof of Possession)
        var otpResult = await _otpService.VerifyPhoneOtpAsync(
            normalizedPhone, request.OtpCode, OtpPurpose.SimOwnershipVerification, consumeOnSuccess: true);

        if (!otpResult.IsValid)
        {
            // Ghi nhận bản ghi SimOwnership (UNVERIFIED) và SimVerification (REJECTED) khi OTP sai/hết hạn
            await RecordFailedVerificationAsync(
                simNumber,
                existingUserOwnership,
                request.UserId,
                $"Xác thực OTP thất bại: {otpResult.Message}",
                cancellationToken);

            throw new OtpVerificationFailedException(otpResult.Message);
        }

        // 6. Gọi Cổng viễn thông Nhà mạng Giả lập (Pha 2 - Proof of Legal Ownership - có delay mô phỏng)
        var telcoResult = await _telcoService.VerifyOwnershipAsync(
            carrier.CarrierCode,
            normalizedPhone,
            user.UserProfile.IdentityNumber,
            user.UserProfile.FullName,
            cancellationToken);

        // 7. Xử lý Kết quả từ Nhà mạng khi Thất bại (Sai CCCD, Khóa thuê bao...)
        if (!telcoResult.IsSuccess)
        {
            await RecordFailedVerificationAsync(
                simNumber,
                existingUserOwnership,
                request.UserId,
                telcoResult.ErrorMessage ?? "Thông tin không trùng khớp với dữ liệu nhà mạng.",
                cancellationToken);

            throw new SimOwnershipVerificationFailedException(telcoResult.ErrorMessage ?? "Thông tin không trùng khớp với dữ liệu nhà mạng.");
        }

        // 8. Xác thực Thành công Toàn diện: Cập nhật SimNumber (AVAILABLE), SimOwnership (ACTIVE) & SimVerification (VERIFIED)
        simNumber.SimStatus = SimStatus.AVAILABLE;
        simNumber.Price = 0m;
        simNumber.FormatRhythmId = null;
        _unitOfWork.SimNumbers.Update(simNumber);

        var ownership = await GetOrCreateUserOwnershipAsync(
            request.UserId,
            simNumber.Id,
            OwnershipStatus.ACTIVE,
            existingUserOwnership,
            cancellationToken);

        var verification = new SimVerification
        {
            Id = Guid.NewGuid(),
            SimOwnershipId = ownership.Id,
            VerificationStatus = VerificationStatus.VERIFIED,
            RequestedAt = DateTime.UtcNow,
            VerifiedAt = DateTime.UtcNow,
            RejectionReason = null,
            IsActive = true
        };

        await _unitOfWork.SimVerifications.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Người dùng {UserId} đã xác thực thành công quyền sở hữu SIM {Phone}", request.UserId, normalizedPhone);

        return new SimOwnershipVerificationResultDto
        {
            IsSuccess = true,
            SimNumberId = simNumber.Id,
            SimOwnershipId = ownership.Id,
            SimVerificationId = verification.Id,
            PhoneNumber = normalizedPhone,
            Price = simNumber.Price,
            FormatRhythmId = simNumber.FormatRhythmId,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            SimStatus = simNumber.SimStatus,
            OwnershipStatus = ownership.OwnershipStatus,
            VerificationStatus = verification.VerificationStatus,
            VerifiedAt = verification.VerifiedAt ?? DateTime.UtcNow,
            Message = "Xác thực quyền sở hữu SIM chính chủ thành công."
        };
    }

    private async Task RecordFailedVerificationAsync(
        SimNumber simNumber,
        SimOwnership? existingOwnership,
        Guid userId,
        string rejectionReason,
        CancellationToken ct)
    {
        simNumber.SimStatus = SimStatus.UNVERIFIED;
        _unitOfWork.SimNumbers.Update(simNumber);

        var ownership = await GetOrCreateUserOwnershipAsync(
            userId,
            simNumber.Id,
            OwnershipStatus.UNVERIFIED,
            existingOwnership,
            ct);

        var failedVerification = new SimVerification
        {
            Id = Guid.NewGuid(),
            SimOwnershipId = ownership.Id,
            VerificationStatus = VerificationStatus.REJECTED,
            RequestedAt = DateTime.UtcNow,
            VerifiedAt = DateTime.UtcNow,
            RejectionReason = rejectionReason,
            IsActive = true
        };

        await _unitOfWork.SimVerifications.AddAsync(failedVerification, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<SimOwnership> GetOrCreateUserOwnershipAsync(
        Guid userId,
        Guid simNumberId,
        OwnershipStatus status,
        SimOwnership? existingOwnership,
        CancellationToken ct)
    {
        if (existingOwnership == null)
        {
            var ownership = new SimOwnership
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                SimNumberId = simNumberId,
                OwnershipStatus = status,
                AcquistionType = AcquistionType.INITIAL_VERIFICATION,
                IsActive = true
            };
            await _unitOfWork.SimOwnerships.AddAsync(ownership, ct);
            return ownership;
        }

        existingOwnership.OwnershipStatus = status;
        _unitOfWork.SimOwnerships.Update(existingOwnership);
        return existingOwnership;
    }

    private async Task<PhonePrefix?> MatchPhonePrefixAsync(string normalizedPhone, CancellationToken ct)
    {
        var allPrefixes = await _unitOfWork.PhonePrefixes.GetAllAsync(null, ct);
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
