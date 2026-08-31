using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.States;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.CancelAuction;

/// <summary>
/// Command hủy phiên đấu giá.
/// ★ State Pattern: Giao toàn bộ kiểm tra tính hợp lệ cho IAuctionState.Cancel().
/// - ScheduledAuctionState.Cancel(): cho phép cả Author và Admin
/// - ActiveAuctionState.Cancel(): chỉ cho phép Admin
/// - EndedAuctionState/CancelledAuctionState.Cancel(): throw BadRequestException
/// </summary>
public record CancelAuctionCommand(
    Guid AuctionId,
    string? CancellationReason
) : IRequest<ApiResponse<object>>
{
    public Guid RequesterId { get; set; }
    public bool IsAdmin { get; set; }
}

public class CancelAuctionCommandValidator : AbstractValidator<CancelAuctionCommand>
{
    public CancelAuctionCommandValidator()
    {
        RuleFor(x => x.AuctionId).NotEmpty().WithMessage("ID phiên đấu giá không hợp lệ.");
        RuleFor(x => x.CancellationReason).MaximumLength(500).WithMessage("Lý do hủy không quá 500 ký tự.");
    }
}

public class CancelAuctionCommandHandler : IRequestHandler<CancelAuctionCommand, ApiResponse<object>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAuctionCommandHandler(IAuctionRepository auctionRepository, IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<object>> Handle(CancelAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        // Kiểm tra quyền sở hữu (Author hoặc Admin)
        if (!request.IsAdmin && auction.AuthorId != request.RequesterId)
            throw new ForbiddenAccessException("Bạn không có quyền hủy phiên đấu giá này.");

        // ★ State Pattern: Ủy thác toàn bộ kiểm tra hợp lệ + chuyển trạng thái cho State Object.
        // State sẽ tự throw exception nếu không hợp lệ (VD: END/CANCELLED không thể hủy, ACTIVE chỉ Admin được hủy).
        var state = AuctionStateContext.GetState(auction.AuctionStatus);
        state.Cancel(auction, request.CancellationReason, request.IsAdmin);

        _auctionRepository.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.SuccessResult("Phiên đấu giá đã được hủy thành công.");
    }
}
