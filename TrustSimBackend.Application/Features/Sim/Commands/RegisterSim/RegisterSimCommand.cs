using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.RegisterSim;

/// <summary>
/// Command đăng ký số SIM mới vào hệ thống cho Người dùng (Seller).
/// </summary>
public record RegisterSimCommand(
    string PhoneNumber,
    Guid FormatRhythmId,
    List<Guid>? CategoryIds = null,
    Guid? PhonePrefixId = null
) : IRequest<ApiResponse<SimDetailDto>>
{
    // Được gán từ Current User (ClaimsPrincipal trong Controller)
    public Guid UserAccountId { get; set; }
}

public class RegisterSimCommandValidator : AbstractValidator<RegisterSimCommand>
{
    public RegisterSimCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại phải bao gồm đúng 10 chữ số và bắt đầu bằng số 0 (VD: 0909123456).");

        RuleFor(x => x.FormatRhythmId)
            .NotEmpty().WithMessage("Vui lòng chọn kiểu nhịp định dạng số SIM.");
    }
}

public class RegisterSimCommandHandler : IRequestHandler<RegisterSimCommand, ApiResponse<SimDetailDto>>
{
    private readonly ISimRepository _simRepository;
    private readonly ISimMasterDataRepository _masterDataRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterSimCommandHandler(
        ISimRepository simRepository,
        ISimMasterDataRepository masterDataRepository,
        IUserAccountRepository userAccountRepository,
        IUnitOfWork unitOfWork)
    {
        _simRepository = simRepository;
        _masterDataRepository = masterDataRepository;
        _userAccountRepository = userAccountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimDetailDto>> Handle(RegisterSimCommand request, CancellationToken cancellationToken)
    {
        var cleanPhone = request.PhoneNumber.Trim();

        // 1. Kiểm tra tài khoản người dùng có tồn tại và đang hoạt động không
        var user = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new NotFoundException("Tài khoản người dùng không tồn tại hoặc đã bị khóa.");
        }

        // 2. Kiểm tra số điện thoại đã tồn tại trên sàn chưa
        var existingSim = await _simRepository.GetByPhoneNumberAsync(cleanPhone, cancellationToken);
        if (existingSim != null)
        {
            var hasActiveOwner = existingSim.SimOwnerships.Any(o => o.OwnershipStatus == OwnershipStatus.ACTIVE);
            if (hasActiveOwner)
            {
                throw new BadRequestException($"Số SIM '{cleanPhone}' đã được đăng ký và đang thuộc sở hữu của một tài khoản trên hệ thống.");
            }
        }

        // 3. Xác định đầu số (PhonePrefix) và nhà mạng
        Guid prefixId;
        if (request.PhonePrefixId.HasValue)
        {
            var prefix = await _masterDataRepository.GetPhonePrefixByIdAsync(request.PhonePrefixId.Value, cancellationToken);
            if (prefix == null)
            {
                throw new BadRequestException("Đầu số điện thoại đã chọn không hợp lệ.");
            }
            prefixId = prefix.Id;
        }
        else
        {
            // Tự động nhận diện đầu số từ 3 ký tự đầu (VD: "090", "098", "088")
            var prefix3 = cleanPhone.Substring(0, 3);
            var autoPrefix = await _masterDataRepository.GetPhonePrefixByValueAsync(prefix3, cancellationToken);
            if (autoPrefix == null)
            {
                throw new BadRequestException($"Không tìm thấy nhà mạng phù hợp với đầu số '{prefix3}'. Vui lòng chỉ định đầu số cụ thể.");
            }
            prefixId = autoPrefix.Id;
        }

        // 4. Kiểm tra kiểu nhịp định dạng (FormatRhythm)
        var rhythm = await _masterDataRepository.GetFormatRhythmByIdAsync(request.FormatRhythmId, cancellationToken);
        if (rhythm == null)
        {
            throw new BadRequestException("Kiểu nhịp định dạng số SIM đã chọn không tồn tại.");
        }

        // 5. Tạo thực thể SimNumber
        var simNumberId = Guid.NewGuid();
        var simNumber = new SimNumber
        {
            Id = simNumberId,
            PhoneNumber = cleanPhone,
            PhonePrefixId = prefixId,
            FormatRhythmId = request.FormatRhythmId,
            SimStatus = SimStatus.AVAILABLE,
            IsActive = true
        };

        // 6. Gán các danh mục (Categories) nếu có
        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            foreach (var categoryId in request.CategoryIds.Distinct())
            {
                var category = await _masterDataRepository.GetCategoryByIdAsync(categoryId, cancellationToken);
                if (category != null)
                {
                    simNumber.SimCategoryMappings.Add(new SimCategoryMapping
                    {
                        Id = Guid.NewGuid(),
                        SimNumberId = simNumberId,
                        SimCategoryId = category.Id,
                        ScoreContribution = 1.0f,
                        IsActive = true
                    });
                }
            }
        }

        // 7. Tạo bản ghi quyền sở hữu (SimOwnership)
        var ownershipId = Guid.NewGuid();
        var ownership = new SimOwnership
        {
            Id = ownershipId,
            SimNumberId = simNumberId,
            UserAccountId = request.UserAccountId,
            OwnershipStatus = OwnershipStatus.ACTIVE,
            AcquistionType = AcquistionType.INITIAL_VERIFICATION,
            IsActive = true
        };

        // 8. Tạo yêu cầu xác minh ban đầu (SimVerification)
        var verification = new SimVerification
        {
            Id = Guid.NewGuid(),
            SimOwnershipId = ownershipId,
            VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
            RequestedAt = DateTime.UtcNow,
            IsActive = true
        };
        ownership.SimVerifications.Add(verification);
        simNumber.SimOwnerships.Add(ownership);

        // 9. Lưu vào cơ sở dữ liệu
        await _simRepository.AddAsync(simNumber, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 10. Lấy lại chi tiết đầy đủ để trả về DTO
        var createdDetail = await _simRepository.GetDetailByIdAsync(simNumberId, cancellationToken);

        var activeOwnership = createdDetail?.SimOwnerships.FirstOrDefault(o => o.OwnershipStatus == OwnershipStatus.ACTIVE);
        var latestVerification = activeOwnership?.SimVerifications.OrderByDescending(v => v.RequestedAt).FirstOrDefault();

        var resultDto = new SimDetailDto
        {
            Id = simNumber.Id,
            PhoneNumber = simNumber.PhoneNumber,
            SimStatus = simNumber.SimStatus,
            PhonePrefixId = simNumber.PhonePrefixId,
            PrefixValue = createdDetail?.PhonePrefix?.PrefixValue ?? string.Empty,
            CarrierId = createdDetail?.PhonePrefix?.SimCarrierId ?? Guid.Empty,
            CarrierName = createdDetail?.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            CarrierCode = createdDetail?.PhonePrefix?.SimCarrier?.CarrierCode ?? string.Empty,
            CarrierLogo = createdDetail?.PhonePrefix?.SimCarrier?.LogoImg ?? string.Empty,
            FormatRhythmId = simNumber.FormatRhythmId,
            FormatRhythmName = createdDetail?.FormatRhythm?.FormatRhythmName ?? string.Empty,
            FormatRhythmValue = createdDetail?.FormatRhythm?.Value ?? string.Empty,
            Categories = createdDetail?.SimCategoryMappings.Select(m => new SimCategoryDto
            {
                Id = m.SimCategory!.Id,
                CategoryCode = m.SimCategory.CategoryCode,
                CategoryName = m.SimCategory.CategoryName,
                Description = m.SimCategory.Description,
                ValuationMultiplier = m.SimCategory.ValuationMultiplier,
                IsActive = m.SimCategory.IsActive
            }).ToList() ?? new List<SimCategoryDto>(),
            OwnerId = user.Id,
            OwnerUserName = user.UserName,
            OwnerFullName = user.UserProfile?.FullName,
            OwnerEmail = user.UserEmail,
            OwnerPhoneNumber = user.UserProfile?.PhoneNumber,
            OwnershipStatus = activeOwnership?.OwnershipStatus,
            VerificationStatus = latestVerification?.VerificationStatus,
            CreatedDate = simNumber.CreatedDate
        };

        return ApiResponse<SimDetailDto>.SuccessResult(resultDto, "Đăng ký SIM vào hệ thống thành công. SIM đang ở trạng thái chờ xác minh.");
    }
}
