using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Application.Features.Auction.Factory;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.CreateAuction;

/// <summary>
/// Command tạo mới một phiên đấu giá (User - Seller).
/// Factory + Strategy Pattern được áp dụng trong Handler để khởi tạo đúng loại phiên.
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

    // Rule — dùng cho cả 2 loại (Strategy tự lọc theo loại hình)
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

        // NOTE: Validation chi tiết theo loại hình (English/Dutch) được xử lý bởi IAuctionStrategy.ValidateRule()
        // trong AuctionFactory — Strategy Pattern xử lý, không cần lặp lại ở đây.
    }
}

public class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, ApiResponse<AuctionDetailDto>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly ISimRepository _simRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuctionFactory _auctionFactory;

    public CreateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        ISimRepository simRepository,
        IUserAccountRepository userAccountRepository,
        IUnitOfWork unitOfWork,
        AuctionFactory auctionFactory)
    {
        _auctionRepository = auctionRepository;
        _simRepository = simRepository;
        _userAccountRepository = userAccountRepository;
        _unitOfWork = unitOfWork;
        _auctionFactory = auctionFactory;
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

        // 5. ★ Factory Pattern: Giao toàn bộ logic khởi tạo Auction + AuctionRule cho Factory.
        //    Factory nội bộ sẽ dùng Strategy Pattern để validate rule và build entity đúng loại.
        var auction = _auctionFactory.Create(request, auctionCode, request.AuthorId);

        // 6. Lưu vào DB
        await _auctionRepository.AddAsync(auction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Load lại chi tiết để trả về DTO
        var created = await _auctionRepository.GetDetailByIdAsync(auction.Id, cancellationToken);

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
