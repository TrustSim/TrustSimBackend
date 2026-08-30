using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.CreateAuction;

/// <summary>
/// Command tạo mới một phiên đấu giá (User - Seller).
/// Hệ thống sẽ tự động tạo AuctionCode và đặt trạng thái SCHEDULE khi submit.
/// </summary>
public record CreateAuctionCommand(
    Guid SimNumberId,
    AuctionType AuctionType,
    decimal StartingPrice,
    decimal ReservePrice,
    DateTime StartAt,
    DateTime EndAt,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt,

    // Rule
    decimal MinimumBidIncrement,
    decimal DepositAmount,
    int MaximumBidCountPerParticipant,
    int MaximumBidCountPerMinute,
    float MinimumBidIntervalSeconds,
    float ExtensionThresholdSeconds,
    float ExtensionDurationSeconds,
    int MaximumExtensionCount,

    // Dutch Auction specific
    decimal DutchDecreaseAmount,
    float DutchDecreaseIntervalSeconds
) : IRequest<ApiResponse<AuctionDetailDto>>
{
    // Gán từ Controller qua ClaimsPrincipal
    public Guid AuthorId { get; set; }
}

public class CreateAuctionCommandValidator : AbstractValidator<CreateAuctionCommand>
{
    public CreateAuctionCommandValidator()
    {
        RuleFor(x => x.SimNumberId).NotEmpty().WithMessage("Vui lòng chọn số SIM để đấu giá.");

        RuleFor(x => x.StartingPrice)
            .GreaterThan(0).WithMessage("Giá khởi điểm phải lớn hơn 0.");

        RuleFor(x => x.ReservePrice)
            .GreaterThan(0).WithMessage("Giá sàn phải lớn hơn 0.")
            .LessThanOrEqualTo(x => x.StartingPrice).WithMessage("Giá sàn phải nhỏ hơn hoặc bằng giá khởi điểm.");

        RuleFor(x => x.StartAt)
            .GreaterThan(DateTime.UtcNow.AddMinutes(30))
            .WithMessage("Thời gian bắt đầu phải ít nhất 30 phút kể từ bây giờ.");

        RuleFor(x => x.EndAt)
            .GreaterThan(x => x.StartAt)
            .WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");

        RuleFor(x => x.RegistrationEndAt)
            .LessThanOrEqualTo(x => x.StartAt)
            .WithMessage("Thời gian đóng đăng ký phải trước hoặc bằng thời gian bắt đầu.");

        RuleFor(x => x.RegistrationStartAt)
            .LessThan(x => x.RegistrationEndAt)
            .WithMessage("Thời gian mở đăng ký phải trước thời gian đóng đăng ký.");

        RuleFor(x => x.DepositAmount)
            .GreaterThan(0).WithMessage("Số tiền đặt cọc phải lớn hơn 0.");

        RuleFor(x => x.MaximumBidCountPerParticipant)
            .GreaterThanOrEqualTo(1).WithMessage("Số lượt đặt giá tối đa ít nhất là 1.");

        RuleFor(x => x.MaximumBidCountPerMinute)
            .GreaterThanOrEqualTo(1).WithMessage("Tần suất đặt giá tối thiểu là 1 lần/phút.");

        // Validation cho English Auction
        When(x => x.AuctionType == AuctionType.ENGLISH, () =>
        {
            RuleFor(x => x.MinimumBidIncrement)
                .GreaterThan(0)
                .WithMessage("Đấu giá kiểu English yêu cầu mức tăng giá tối thiểu lớn hơn 0.");
        });

        // Validation cho Dutch Auction
        When(x => x.AuctionType == AuctionType.DUTCH, () =>
        {
            RuleFor(x => x.DutchDecreaseAmount)
                .GreaterThan(0)
                .WithMessage("Đấu giá kiểu Dutch yêu cầu mức giảm giá mỗi chu kỳ lớn hơn 0.");

            RuleFor(x => x.DutchDecreaseIntervalSeconds)
                .GreaterThan(0)
                .WithMessage("Đấu giá kiểu Dutch yêu cầu khoảng thời gian giảm giá lớn hơn 0 giây.");
        });
    }
}

public class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, ApiResponse<AuctionDetailDto>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly ISimRepository _simRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        ISimRepository simRepository,
        IUserAccountRepository userAccountRepository,
        IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _simRepository = simRepository;
        _userAccountRepository = userAccountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AuctionDetailDto>> Handle(CreateAuctionCommand request, CancellationToken cancellationToken)
    {
        // 1. Xác thực người dùng
        var user = await _userAccountRepository.GetByIdAsync(request.AuthorId, cancellationToken);
        if (user == null || !user.IsActive)
            throw new NotFoundException("Tài khoản người dùng không tồn tại hoặc đã bị khóa.");

        // 2. Kiểm tra SIM tồn tại và thuộc sở hữu của người tạo
        var simOwnership = await _simRepository.GetActiveOwnershipAsync(request.SimNumberId, cancellationToken);
        if (simOwnership == null)
            throw new NotFoundException("Không tìm thấy số SIM hoặc SIM chưa được đăng ký quyền sở hữu.");

        if (simOwnership.UserAccountId != request.AuthorId)
            throw new ForbiddenAccessException("Bạn không có quyền đấu giá số SIM này.");
        // 3. Kiểm tra SIM có đang trong phiên đấu giá nào khác không
        var hasActiveAuction = await _auctionRepository.ExistsActiveAuctionForSimAsync(request.SimNumberId, cancellationToken);
        if (hasActiveAuction)
            throw new BadRequestException("Số SIM này đang có phiên đấu giá đang chạy hoặc đang được lên lịch. Vui lòng đợi phiên kết thúc.");

        // 4. Tạo AuctionCode duy nhất
        var auctionCode = await _auctionRepository.GenerateUniqueAuctionCodeAsync(cancellationToken);

        // 5. Tạo thực thể Auction (Factory logic: khởi tạo theo loại hình)
        var auctionId = Guid.NewGuid();
        var auction = new Domain.Entities.Auctions.Auction
        {
            Id = auctionId,
            AuctionCode = auctionCode,
            AuctionType = request.AuctionType,
            AuctionStatus = AuctionStatus.SCHEDULE, // Tạo trực tiếp ở SCHEDULE
            AuthorId = request.AuthorId,
            SimNumberId = request.SimNumberId,
            StartingPrice = request.StartingPrice,
            CurrentPrice = request.StartingPrice,
            ReservePrice = request.ReservePrice,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            RegistrationStartAt = request.RegistrationStartAt,
            RegistrationEndAt = request.RegistrationEndAt,
            LockVersion = 1,
            IsActive = true
        };

        // 6. Tạo AuctionRule gắn với phiên đấu giá (Strategy: Dutch vs English params)
        var rule = new AuctionRule
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            MinimumBidIncrement = request.AuctionType == AuctionType.ENGLISH ? request.MinimumBidIncrement : 0,
            DepositAmount = request.DepositAmount,
            MaximumBidCountPerParticipant = request.MaximumBidCountPerParticipant,
            MaximumBidCountPerMinute = request.MaximumBidCountPerMinute,
            MinimumBidIntervalSeconds = request.MinimumBidIntervalSeconds,
            ExtensionThresholdSeconds = request.ExtensionThresholdSeconds,
            ExtensionDurationSeconds = request.ExtensionDurationSeconds,
            MaximumExtensionCount = request.MaximumExtensionCount,
            DutchDecreaseAmount = request.AuctionType == AuctionType.DUTCH ? request.DutchDecreaseAmount : 0,
            DutchDecreaseIntervalSeconds = request.AuctionType == AuctionType.DUTCH ? request.DutchDecreaseIntervalSeconds : 0,
            IsActive = true
        };
        auction.AuctionRule = rule;

        // 7. Lưu vào DB
        await _auctionRepository.AddAsync(auction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Load lại chi tiết để trả về DTO
        var created = await _auctionRepository.GetDetailByIdAsync(auctionId, cancellationToken);

        return ApiResponse<AuctionDetailDto>.SuccessResult(
            MapToDetailDto(created!),
            $"Phiên đấu giá '{auctionCode}' đã được tạo thành công và đã được lên lịch.");
    }

    private static AuctionDetailDto MapToDetailDto(Domain.Entities.Auctions.Auction a)
    {
        return new AuctionDetailDto
        {
            AuctionId = a.Id,
            AuctionCode = a.AuctionCode,
            AuctionType = a.AuctionType,
            AuctionStatus = a.AuctionStatus,
            SimNumberId = a.SimNumberId,
            PhoneNumber = a.SimNumber?.PhoneNumber ?? string.Empty,
            CarrierName = a.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName,
            FormatRhythmName = a.SimNumber?.FormatRhythm?.FormatRhythmName,
            StartingPrice = a.StartingPrice,
            CurrentPrice = a.CurrentPrice,
            ReservePrice = a.ReservePrice,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            RegistrationStartAt = a.RegistrationStartAt,
            RegistrationEndAt = a.RegistrationEndAt,
            AuthorId = a.AuthorId,
            AuthorName = a.Author?.UserProfile?.FullName ?? a.Author?.UserName,
            AuthorEmail = a.Author?.UserEmail,
            Categories = a.SimNumber?.SimCategoryMappings.Select(m => m.SimCategory?.CategoryName ?? string.Empty).ToList() ?? new(),
            Rule = a.AuctionRule == null ? null : new AuctionRuleDto
            {
                MinimumBidIncrement = a.AuctionRule.MinimumBidIncrement,
                DepositAmount = a.AuctionRule.DepositAmount,
                MaximumBidCountPerParticipant = a.AuctionRule.MaximumBidCountPerParticipant,
                MaximumBidCountPerMinute = a.AuctionRule.MaximumBidCountPerMinute,
                MinimumBidIntervalSeconds = a.AuctionRule.MinimumBidIntervalSeconds,
                ExtensionThresholdSeconds = a.AuctionRule.ExtensionThresholdSeconds,
                ExtensionDurationSeconds = a.AuctionRule.ExtensionDurationSeconds,
                MaximumExtensionCount = a.AuctionRule.MaximumExtensionCount,
                DutchDecreaseAmount = a.AuctionRule.DutchDecreaseAmount,
                DutchDecreaseIntervalSeconds = a.AuctionRule.DutchDecreaseIntervalSeconds,
            },
            CreatedDate = a.CreatedDate,
            UpdatedDate = a.UpdatedDate
        };
    }
}
