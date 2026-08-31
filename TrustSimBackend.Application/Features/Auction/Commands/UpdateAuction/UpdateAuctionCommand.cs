using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Application.Features.Auction.States;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.UpdateAuction;

/// <summary>
/// Command cập nhật phiên đấu giá.
/// ★ State Pattern: AuctionStateContext xác định trạng thái cho phép sửa hay không.
/// </summary>
public record UpdateAuctionCommand(
    Guid AuctionId,
    decimal StartingPrice,
    decimal ReservePrice,
    DateTime StartAt,
    DateTime EndAt,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt,
    decimal MinimumBidIncrement,
    decimal DepositAmount,
    int MaximumBidCountPerParticipant,
    int MaximumBidCountPerMinute,
    float MinimumBidIntervalSeconds,
    float ExtensionThresholdSeconds,
    float ExtensionDurationSeconds,
    int MaximumExtensionCount,
    decimal DutchDecreaseAmount,
    float DutchDecreaseIntervalSeconds
) : IRequest<ApiResponse<AuctionDetailDto>>
{
    public Guid RequesterId { get; set; }
    public bool IsAdmin { get; set; }
}

public class UpdateAuctionCommandValidator : AbstractValidator<UpdateAuctionCommand>
{
    public UpdateAuctionCommandValidator()
    {
        RuleFor(x => x.AuctionId).NotEmpty().WithMessage("ID phiên đấu giá không hợp lệ.");
        RuleFor(x => x.StartingPrice).GreaterThan(0).WithMessage("Giá khởi điểm phải lớn hơn 0.");
        RuleFor(x => x.ReservePrice)
            .GreaterThan(0).WithMessage("Giá sàn phải lớn hơn 0.")
            .LessThanOrEqualTo(x => x.StartingPrice).WithMessage("Giá sàn phải nhỏ hơn hoặc bằng giá khởi điểm.");
        RuleFor(x => x.StartAt)
            .GreaterThan(DateTime.UtcNow.AddMinutes(30))
            .WithMessage("Thời gian bắt đầu phải ít nhất 30 phút kể từ bây giờ.");
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt).WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
        RuleFor(x => x.RegistrationEndAt)
            .LessThanOrEqualTo(x => x.StartAt)
            .WithMessage("Thời gian đóng đăng ký phải trước hoặc bằng thời gian bắt đầu.");
    }
}

public class UpdateAuctionCommandHandler : IRequestHandler<UpdateAuctionCommand, ApiResponse<AuctionDetailDto>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAuctionCommandHandler(IAuctionRepository auctionRepository, IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AuctionDetailDto>> Handle(UpdateAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetDetailByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        // Kiểm tra quyền
        if (!request.IsAdmin && auction.AuthorId != request.RequesterId)
            throw new ForbiddenAccessException("Bạn không có quyền chỉnh sửa phiên đấu giá này.");

        // ★ State Pattern: Kiểm tra trạng thái có cho phép sửa không
        var state = AuctionStateContext.GetState(auction.AuctionStatus);
        if (!state.CanEdit(auction))
            throw new BadRequestException(
                $"Không thể chỉnh sửa phiên đấu giá đang ở trạng thái '{auction.AuctionStatus}'. Chỉ có thể sửa khi phiên đang SCHEDULE.");

        // Cập nhật các trường
        auction.StartingPrice = request.StartingPrice;
        auction.CurrentPrice = request.StartingPrice;
        auction.ReservePrice = request.ReservePrice;
        auction.StartAt = request.StartAt;
        auction.EndAt = request.EndAt;
        auction.RegistrationStartAt = request.RegistrationStartAt;
        auction.RegistrationEndAt = request.RegistrationEndAt;

        // Cập nhật AuctionRule
        if (auction.AuctionRule != null)
        {
            auction.AuctionRule.DepositAmount = request.DepositAmount;
            auction.AuctionRule.MaximumBidCountPerParticipant = request.MaximumBidCountPerParticipant;
            auction.AuctionRule.MaximumBidCountPerMinute = request.MaximumBidCountPerMinute;
            auction.AuctionRule.MinimumBidIntervalSeconds = request.MinimumBidIntervalSeconds;
            auction.AuctionRule.ExtensionThresholdSeconds = request.ExtensionThresholdSeconds;
            auction.AuctionRule.ExtensionDurationSeconds = request.ExtensionDurationSeconds;
            auction.AuctionRule.MaximumExtensionCount = request.MaximumExtensionCount;

            if (auction.AuctionType == AuctionType.ENGLISH)
                auction.AuctionRule.MinimumBidIncrement = request.MinimumBidIncrement;

            if (auction.AuctionType == AuctionType.DUTCH)
            {
                auction.AuctionRule.DutchDecreaseAmount = request.DutchDecreaseAmount;
                auction.AuctionRule.DutchDecreaseIntervalSeconds = request.DutchDecreaseIntervalSeconds;
            }
        }

        _auctionRepository.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _auctionRepository.GetDetailByIdAsync(request.AuctionId, cancellationToken);

        return ApiResponse<AuctionDetailDto>.SuccessResult(
            MapToDetailDto(updated!),
            "Cập nhật phiên đấu giá thành công.");
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
